using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;
using Harmony;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

using static LWM.DeepStorage.Utils.DBF; // trace utils

//BUG/TODO: number to haul will not work correctly for stackable things if a unit has a maximum weight limit.

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
     * We patch via prefix by first checking if the slotGroup in question is part of a
     * Deep Storage unit.  If it is, then we take over (and duplicate a little bit of code)
     * and do the more complicated calculation of how many to carry.
     * 
     * We run through the same idea as the original function with a bunch of loops thrown in
     **************************************/
    [HarmonyPatch(typeof(Verse.AI.HaulAIUtility), "HaulToCellStorageJob")]
    class Patch_HaulToCellStorageJob {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            List<CodeInstruction> code=instructions.ToList();
            // a function call we need to check against a few times:
            var callThingAt=AccessTools.Method(typeof(ThingGrid), "ThingAt", new Type[] {typeof(IntVec3),typeof(ThingDef)});
            var inDeepStorage=generator.DeclareLocal(typeof(bool));

            bool checkedInDeepStorage=false;
            for (int i=0;i<code.Count;i++) {
                if (code[i].opcode==OpCodes.Stloc_1 &&
                    checkedInDeepStorage==false) { // slotGroup
                    yield return code[i++]; //Stloc.1
                    if ( Harmony.AccessTools.Method("LWM.DeepStorage.Utils:CanStoreMoreThanOneThingIn")==null) {
                        Log.Error("Failure, cannot find CSMTOTI");
                    }
                    yield return new CodeInstruction(OpCodes.Ldloc_1); // slotGroup
                    yield return new CodeInstruction(OpCodes.Call,
                                                     Harmony.AccessTools.Method("LWM.DeepStorage.Utils:CanStoreMoreThanOneThingIn"));
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
                 * Luckily, the result of ThingAt is always StLoc'd somewhere, LdLoc'd, and then BrFalsed.
                 */
                if (code[i].opcode==OpCodes.Ldarg_0 && // Pawn p
                    // Next codeinstruction we want: callvirt Verse.Map get_Map()
                    code[i+1].opcode==OpCodes.Callvirt &&
                    // not   .operand==Harmony.AccessTools.Method(typeof(Pawn), "get_Map") :p
                    code[i+1].operand==Harmony.AccessTools.Method(typeof(Thing), "get_Map") && // p.Map
                    code[i+2].opcode==OpCodes.Ldfld &&
                    code[i+2].operand==Harmony.AccessTools.Field(typeof(Map), "thingGrid")) { // p.Map.thingGrid...
                    ////////////// Put our branch here ////////////////
                    yield return new CodeInstruction(OpCodes.Ldloc, inDeepStorage);
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
                            code[j].operand==callThingAt) {
                            // skip ThingAt()
                            break;
                        }
                        yield return new CodeInstruction(code[j]);
                    }
                    // insert our call
                    yield return new CodeInstruction(OpCodes.Callvirt,
                           AccessTools.Method("LWM.DeepStorage.Patch_HaulToCellStorageJob:NullOrLastThingAt"));
                    for (j++;j<code.Count;j++) { // skip the ThingAt call, keep going to branch on false:
                        yield return new CodeInstruction(code[j]); // this also stores the result in the proper place
                        if (code[j].opcode==OpCodes.Brfalse) break;
                    }
                    // We now return to our regularly scheduled call, if not inDeepStorage:
                    code[i].labels.Add(vanillaThingAt);
                } // end test for p.Map.thingGrid etc.
                yield return code[i];
            }
        } // end Transpiler

        // TODO: move this logic to DeepStorage.cs
        public static Thing NullOrLastThingAt(Map map, IntVec3 c, ThingDef def) {
            CompDeepStorage cds=(c.GetSlotGroup(map).parent as ThingWithComps).GetComp<CompDeepStorage>();
            var l=map.thingGrid.ThingsListAtFast(c); // we know it's a slotgroup, so it's valid :p
            var freeSlots=cds.maxNumberStacks;
            Thing lastThing=null;
            for (int i=0; i<l.Count;i++) {
                if (!l[i].def.EverStorable(false)) continue;
                freeSlots--;
                if (!(l[i].def == def)) continue;
                if (lastThing == null) lastThing=l[i];
                else {
                    if (l[i].stackCount <= lastThing.stackCount)
                        lastThing=l[i];
                }
            }
            if (freeSlots > 0) return null;
            return lastThing; // if this is also null, we have a problem :p
        }
    
        //  It might be possible to do this via Transpiler, but it's harder, so we do it this way.
        //      static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        public static bool PrefixXXX(out Job __result, Pawn p, Thing t, IntVec3 storeCell, bool fitInStoreCell) {
            Utils.Err(HaulToCellStorageJob, "Job request for " + t.stackCount + t.ToString() + " by pawn " + p.ToString() +
                      " to " + storeCell.ToString());
            SlotGroup slotGroup = p.Map.haulDestinationManager.SlotGroupAt(storeCell);
            if (slotGroup == null || !(slotGroup?.parent is ThingWithComps) ||
                (slotGroup.parent as ThingWithComps).TryGetComp<CompDeepStorage>() == null) {
                // Not going to Deep Storage
                __result = null;
                return true; // HaulToCellStorageJob will handle it
            }
            // Create our own job for hauling to Deep_Storage units...
            // Another opportunity to put our own JobDef here:
            //   new Job(DefDatabase<JobDef>.GetNamed("LWM_HaulToDeepStorage"), t, storeCell, map);, etc
            Job job = new Job(JobDefOf.HaulToCell, t, storeCell);
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
                // We look thru for stacks of storeable things; if they match our thing t, 
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
                } // if storeable
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
                    // We look thru for stacks of storeable things; if they match our thing t, 
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
                    } // if storeable
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
        }
    } // done patching HaulToCellStorageJob




}

