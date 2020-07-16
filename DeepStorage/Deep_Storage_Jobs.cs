using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

using static LWM.DeepStorage.Utils.DBF; // trace utils

//BUG/TODO: number to haul will not work correctly for stackable things if a unit has a maximum weight limit.
// TODO: I should really just skip the whole count logic and grab the number for the job from
//       CompDeepStorage's CapacityAt()
//  You know...I could have just written my own job definition and inserted it via
//     HaulToCellStorageJob?  Why not do it?  Because if anyone else's mod adds
//     HaulToCellStorageJobs, I'd miss them.  Maybe xpath?
//  The only benefit over what I have now is that I could have the pawn say
//    "storing" while it's doing its wait cycle.  Might TODO that some day?


namespace LWM.DeepStorage
{
    /**********************************************************************
     * Verse/AI/HaulAIUtility.cs:  HaulToCellStorageJob(...)
     * The important part here is that HaulToCellStorageJob counts how many
     *   of a stackable thing to carry to a slotGroup (storage)
     *
     * We patch with Harmony Transpiler to replace
     *   Thing someThing=p.Map.thingGrid.ThingAt(someCell, t.def);
     * with
     *   if (inDeepStorage) {
     *     someThing=NullOrLastThingAt(Map, somecell, t.def);
     *   } else {
     *     someThing=p.Map.thingGrid.ThingAt(someCell, t.def);
     *   }
     **************************************/
    [HarmonyPatch(typeof(Verse.AI.HaulAIUtility), "HaulToCellStorageJob")]
    class Patch_HaulToCellStorageJob {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            // this #if false block prints out the full transpiled IL code
            #if false        /////////////////////////////////////
            Log.Error("Debug information for HaulToCellStorageJob transpiler:");
            var l=XTranspiler(instructions ,generator).ToList();//
            string s="Code:";                                   //
            int i=0;                                            //
            foreach (var c in l) {                              //
                if (c.opcode==OpCodes.Stloc_2 ||                //
                    c.opcode==OpCodes.Stloc_S) {//     ^        //
                    Log.Warning(""+i+": "+c);   //     |        //
                } else {                        // Transpiler   //
                    Log.Message(""+i+": "+c);   //  Debugging   //
                }                               //     |        //
                s+="\n"+i+": "+c;               //     v        //
                i++;                   // (left here just in    //
                yield return c;        //  case I ever need     //
            }                          //  to use it again)     //
            Log.Error(s);                                       //
        }                                                       //
                                                                //
        public static IEnumerable<CodeInstruction> XTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            #endif // Can I even do this? YES!  //////////////////
            List<CodeInstruction> code=instructions.ToList();
            // a function call we need to check against a few times:
            var callThingAt=AccessTools.Method(typeof(ThingGrid), "ThingAt", new Type[] {typeof(IntVec3),typeof(ThingDef)});
            var inDeepStorage=generator.DeclareLocal(typeof(bool)); // new local variable

            bool checkedInDeepStorage=false;
            for (int i=0;i<code.Count;i++) {
                // Logic to test if we are in Deep Storage:
                // Add this code to the beginning of the function (right after slotgroup is figured out)
                if (code[i].opcode==OpCodes.Stloc_1 &&
                    checkedInDeepStorage==false) { // slotGroup
                    //Log.Warning("About to return Stloc_1: i is " + i);
                    yield return code[i++]; // the Stloc.1 we just found
                    if ( HarmonyLib.AccessTools.Method("LWM.DeepStorage.Utils:CanStoreMoreThanOneThingIn")==null) {
                        Log.Error("Failure, cannot find CSMTOTI");
                    }
                    yield return new CodeInstruction(OpCodes.Ldloc_1); // slotGroup
                    yield return new CodeInstruction(OpCodes.Call,
                                                     HarmonyLib.AccessTools.Method("LWM.DeepStorage.Utils:CanStoreMoreThanOneThingIn"));
                    yield return new CodeInstruction(OpCodes.Stloc, inDeepStorage);
                    checkedInDeepStorage=true;
                }
                /* Replace
                 *   Thing someThing=p.Map.thingGrid.ThingAt(someCell, t.def);
                 * with
                 *   if (inDeepStorage) {
                 *     someThing=NullOrLastThingAt(Map, somecell, t.def);
                 *   } else {
                 *     someThing=p.Map.thingGrid.ThingAt(someCell, t.def);
                 *   }
                 *
                 * This is tricky.
                 *   No, really.
                 * Luckily, the result of ThingAt is always StLoc'd somewhere, LdLoc'd, and then BrFalsed.
                 *   So it's easy to find.
                 * Note I could do more slick optimazations here, but I'm going to stick with what I have.
                 */
                //Log.Error("About to return +" + code[i] + ", i is " + i);
                if (code[i].opcode==OpCodes.Ldarg_0 && // Pawn p
                    // Next codeinstruction we want: callvirt Verse.Map get_Map()
                    code[i+1].opcode==OpCodes.Callvirt &&
                    // not   .operand==Harmony.AccessTools.Method(typeof(Pawn), "get_Map") :p b/c virtual
                    (MethodInfo)code[i+1].operand==HarmonyLib.AccessTools.Method(typeof(Thing), "get_Map") && // p.Map
                    code[i+2].opcode==OpCodes.Ldfld &&
                    (FieldInfo)code[i+2].operand==HarmonyLib.AccessTools.Field(typeof(Map), "thingGrid")) { // p.Map.thingGrid...
                    //Log.Error("--------------found thingGrid call");
                    ////////////// Put our branch here ////////////////
                    // Steal any labels lying around:
                    var c= new CodeInstruction(OpCodes.Ldloc, inDeepStorage);
                    c.labels=code[i].labels;
                    code[i]=new CodeInstruction(OpCodes.Ldarg_0);
                    yield return c;
                    Label vanillaThingAt = generator.DefineLabel();
                    yield return new CodeInstruction(OpCodes.Brfalse,vanillaThingAt);
                    // 2 things to do:
                    // Put all our NullOrLastThingAt call in...correctly,
                    // Add the vanillaThingAt Label to code[i] ^.^

                    // To make our NullOrLastThingAt call, we copy most everything
                    //   before and afer the ThingAt call.
                    //   (replacing ThingAt with our NullOrLastThingAt)
                    yield return new CodeInstruction(OpCodes.Ldarg_0);   // p
                    yield return new CodeInstruction(code[i+1]);         // p.Map
                    // follow code for ThingAt(someCell, t.def);
                    int j=i+3;
                    for (; j<code.Count;j++) { // this loads the cell and the def
                        if (code[j].opcode==OpCodes.Callvirt &&
                            (MethodInfo)code[j].operand==callThingAt) {
                            // skip ThingAt()
                            //Log.Error("-------------------skipping back to do the ThingAt mirror");
                            break;
                        }
                        yield return new CodeInstruction(code[j]);
                    }
                    // insert our call
                    yield return new CodeInstruction(OpCodes.Callvirt,
                           AccessTools.Method("LWM.DeepStorage.Patch_HaulToCellStorageJob:NullOrLastThingAt"));
                    for (j++;j<code.Count;j++) { // skip the ThingAt call, keep going to branch on false:
                        yield return new CodeInstruction(code[j]); // this also stores the result in the proper place
                        //Log.Warning("Just going ahead after ThingAt: " + code[j] + ".  j is " + j);
                        // Fun fact: version 1.1 changed Brfalse to Brfalse_S  :p
                        if (code[j].opcode==OpCodes.Brfalse || code[j].opcode==OpCodes.Brfalse_S) break;
                    }
                    // Okay, so it's true.  Now we skip over the original test:
                    Label backToNormal=generator.DefineLabel();
                    yield return new CodeInstruction(OpCodes.Br, backToNormal);
                    // We now return to our regularly scheduled call (if not inDeepStorage):
                    code[i].labels.Add(vanillaThingAt);
                    // now fast foward thru until we hit that Brfalse command - we need to pick up after that!
                    for (; i<code.Count;i++) {
                        yield return code[i];
                        if (code[i].opcode==OpCodes.Brfalse || code[i].opcode==OpCodes.Brfalse_S) break;
                    }
                    i++; // move on past that Brfalse
                    //Log.Error("Just jumped past brFalse, i is " + i);
                    //Log.Error(" Code is " + code[i]);
                    code[i].labels.Add(backToNormal);
                } // end test for p.Map.thingGrid etc.
                yield return code[i];
                #if DEBUG
                if (code[i].opcode==OpCodes.Stloc_3 && code[i-1].opcode==OpCodes.Add) {
                    //Log.Error("adding trace line "+i);
                    yield return new CodeInstruction(OpCodes.Ldc_I4, i);
                    yield return new CodeInstruction(OpCodes.Ldloc_3);
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Job), "count"));
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 4);
                    yield return new CodeInstruction(OpCodes.Ldloc_2);
                    yield return new CodeInstruction(OpCodes.Ldloc, 7);
                    yield return new CodeInstruction(OpCodes.Call,
                                    AccessTools.Method("LWM.DeepStorage.Patch_HaulToCellStorageJob:LogNum"));
                }
                #endif
            }
        } // end Transpiler

        // TODO: move this logic to DeepStorage.cs
        public static Thing NullOrLastThingAt(Map map, IntVec3 c, ThingDef def) {
            CompDeepStorage cds=(c.GetSlotGroup(map).parent as ThingWithComps).GetComp<CompDeepStorage>();
            var l=map.thingGrid.ThingsListAtFast(c); // we know it's a slotgroup, so it's valid :p
            var freeSlots=cds.maxNumberStacks;
            Utils.Err(HaulToCellStorageJob, "  testing for def "+def+" at "+c+"; "+freeSlots+" slots.");
            Thing lastThing=null;
            for (int i=0; i<l.Count;i++) {
                if (!l[i].def.EverStorable(false)) continue;
                freeSlots--;
                Utils.Mess(HaulToCellStorageJob, "  Checking item "+l[i]+"; now have "+freeSlots+" left.");
                if (!(l[i].def == def)) continue; // possible problem if defs are same but cannot stack?
                if (lastThing == null) lastThing=l[i];
                else {
                    if (l[i].stackCount <= lastThing.stackCount)
                        lastThing=l[i];
                }
            }
            if (freeSlots > 0) {
                Utils.Err(HaulToCellStorageJob, "  Final count of free slots: "+freeSlots);
                return null;
            }
            Utils.Warn(HaulToCellStorageJob, "  Final item count: "+((lastThing==null)?
                                               "NULL":lastThing.stackCount.ToString()));
            return lastThing; // if this is also null, we have a problem :p
        }

        // Use a new logic for CompCachedDeepStorage to get the maximum job.count.
        public static bool Prefix(Pawn p, Thing t, IntVec3 storeCell, ref Job __result) {
            if (Utils.GetDeepStorageOnCell(storeCell, p.Map, out CompDeepStorage comp)
                && comp is CompCachedDeepStorage compCached)
            {
                int capacity = compCached.CapacityToStoreThingAt(t, p.Map, storeCell);
                float carryingCapacity = p.carryTracker.AvailableStackSpace(t.def);

                Job job = JobMaker.MakeJob(JobDefOf.HaulToCell, t, storeCell);
                job.count = Mathf.Min(capacity, Mathf.FloorToInt(carryingCapacity));
                job.haulOpportunisticDuplicates = true;
                job.haulMode = HaulMode.ToCellStorage;

                __result = job;
                return false;
            }

            return true;
        }

#if DEBUG
        public static void LogNum(int line, int num, int count, float statValue, Thing t1, Thing t2) {
            Utils.Warn(HaulToCellStorageJob, "----"+line+": "+num+" vs "+count+" (total max: "+statValue+")");
            if (t1 != null) Utils.Mess(HaulToCellStorageJob, "        t1 is "+t1.stackCount+" "+t1);
            if (t2 != null) Utils.Mess(HaulToCellStorageJob, "        t2 is "+t2.stackCount+" "+t2);
        }

        public static void Postfix(Job __result) {
            Utils.Err(HaulToCellStorageJob, "Final Job size is "+__result.count);
        }

        /* Original patch:
         * We patch via prefix by first checking if the slotGroup in question is part of a
         * Deep Storage unit.  If it is, then we take over (and duplicate a little bit of code)
         * and do the more complicated calculation of how many to carry.
         *
         * We run through the same idea as the original function with a bunch of loops thrown in
         */

        //  It might be possible to do this via Transpiler, but it's harder, so we do it this way.
        //      static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        //public static bool Prefix(out Job __result, Pawn p, Thing t, IntVec3 storeCell, bool fitInStoreCell) {
        public static bool Prefix(Pawn p, Thing t, IntVec3 storeCell, bool fitInStoreCell) {
            Utils.Err(HaulToCellStorageJob, "Job request for " + t.stackCount + t.ToString() + " by pawn " + p.ToString() +
                      " to " + storeCell.ToString());
            return true;
/*            Job __result;
            SlotGroup slotGroup = p.Map.haulDestinationManager.SlotGroupAt(storeCell);
            if (slotGroup == null || !(slotGroup?.parent is ThingWithComps) ||
                (slotGroup.parent as ThingWithComps).TryGetComp<CompDeepStorage>() == null) {
                // Not going to Deep Storage
                __result = null;
                return true; // HaulToCellStorageJob will handle it
            }
            // Create our own job for hauling to Deep_Storage units...
            // Another opportunity to put our own JobDef here:
            //   new XJob(DefDatabase<JobDef>.GetNamed("LWM_HaulToDeepStorage"), t, storeCell, map);, etc
            Job job = new XJob(JobDefOf.HaulToCell, t, storeCell);
            job.haulOpportunisticDuplicates = true;
            job.haulMode = HaulMode.ToCellStorage;

            __result = job;

            if (t.def.stackLimit <= 1) {
                job.count = 1;
                Utils.Err(HaulToCellStorageJob, "haulMaxNumToCellJob: " + t.ToString() + ": stackLimit<=1 so job.count=1");
                return false;
            }
            // we have to count :p
            // Really???  statValue is the carrying capacity of the Pawn
            float statValue = p.GetStatValue(StatDefOf.CarryingCapacity, true);
            job.count = 0;

            var maxStacks = ((ThingWithComps)slotGroup.parent).GetComp<CompDeepStorage>().maxNumberStacks;
            Utils.Err(HaulToCellStorageJob, p.ToString() + " taking " + t.ToString() + ", count: " + t.stackCount);
            var howManyStacks = 0;
            //fill job.count with space in the direct storeCell:
            List<Thing> stuffHere = p.Map.thingGrid.ThingsListAt(storeCell);
            for (int i = 0; i < stuffHere.Count; i++) // thing list at storeCell
            {
                Thing thing2 = stuffHere[i];
                // We look thru for stacks of storable things; if they match our thing t,
                //   we see how many we can carry there!
                if (thing2.def.EverStorable(false)) {
                    Utils.Warn(HaulToCellStorageJob, "... already have a stack here of " + thing2.stackCount + " of " + thing2.ToString());
                    howManyStacks++;
                    if (thing2.def == t.def) {
                        job.count += thing2.def.stackLimit - thing2.stackCount;
                        Utils.Warn(HaulToCellStorageJob, "... count is now " + job.count);
                        if ((float)job.count >= statValue || job.count >= t.def.stackLimit) {
                            job.count = Math.Min(t.def.stackLimit, job.count);
                            Utils.Warn(HaulToCellStorageJob, "Final count: " + job.count + " (can carry: " + statValue + ")");
                            return false;
                        }
                    }
                } // if storable
            } // thing list at storeCell

            if (howManyStacks < maxStacks || job.count >= t.def.stackLimit) {
                // There's room for a whole stack right here!
                job.count = t.def.stackLimit;
                Utils.Warn(HaulToCellStorageJob, "Room for whole stack! " + howManyStacks + "/" + maxStacks);
                return false;
            }
            if (fitInStoreCell) { // don't look if pawn can put stuff anywhere else
                return false;
            }
            //  If !fitInStoreCell, we look in the other cells of the storagearea to see if we can put some there:
            //     (personally, I'd be okay if my pawns also tried NEARBY storage areas,
            //      but that's getting really complicted.)
            Utils.Warn(HaulToCellStorageJob, "Continuing to search for space in nearby cells...");
            List<IntVec3> cellsList = slotGroup.CellsList;
            for (int i = 0; i < cellsList.Count; i++) {
                IntVec3 c = cellsList[i];
                if (c == storeCell) {
                    continue;
                }
                if (!StoreUtility.IsGoodStoreCell(c, p.Map, t, p, p.Faction)) {
                    continue;
                }
                //repeat above counting of space in the current store cell:
                stuffHere = p.Map.thingGrid.ThingsListAt(c);
                howManyStacks = 0;
                for (int j = 0; j < stuffHere.Count; j++) {
                    Thing thing2 = stuffHere[j];
                    // We look thru for stacks of storable things; if they match our thing t,
                    //   we see how many we can carry there!
                    if (thing2.def.EverStorable(false)) {
                        Utils.Warn(HaulToCellStorageJob, "... already have a stack here of " + thing2.stackCount + " of " + thing2.ToString());
                        howManyStacks++;
                        if (thing2.def == t.def) {
                            job.count += thing2.def.stackLimit - thing2.stackCount;
                            Utils.Warn(HaulToCellStorageJob, "... count is now " + job.count);
                            if ((float)job.count >= statValue || job.count >= t.def.stackLimit) {
                                job.count = Math.Min(t.def.stackLimit, job.count);
                                Utils.Warn(HaulToCellStorageJob, "Final count: " + job.count + " (can carry: " + statValue + ")");
                                return false;
                            }
                        }
                    } // if storable
                } // done looking at all stuff in c
                // So...how many stacks WERE there in c?
                if (howManyStacks < maxStacks) {
                    // There's another stack's worth of room!
                    Utils.Warn(HaulToCellStorageJob, "Room for whole stack! " + howManyStacks + "/" + maxStacks);
                    job.count = t.def.stackLimit;
                    return false;
                }
            } // looking at all cells in storeage area
              // Nowhere else to look, no other way to store anything we pick up.
              // Count stays at job.count
            Utils.Warn(HaulToCellStorageJob, "Final count: " + job.count);
            return false;
            */
        }
#endif
    } // done patching HaulToCellStorageJob

}
