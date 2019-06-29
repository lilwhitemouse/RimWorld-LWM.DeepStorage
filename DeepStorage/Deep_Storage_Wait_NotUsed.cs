using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;
using Harmony;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
                              // XXX:
using UnityEngine;
/*******************************************************************************************
           IMPORTANT NOTE:   This file IS NOT USED
           
           It is kept here as a set of notes for a possible alternative approach to
           modding the job
           
           See instead DeepStorage_Pause.cs
 *******************************************************************************************/


// NOTE: PlaceCarriedThingInCellFacing not actually used?
//  You know...I could have just written my own job definition and inserted it via
//     HaulToCellStorageJob, you know?



namespace LWM.DeepStorage
{
    public class Deep_Storage_Wait
    {
        public Deep_Storage_Wait()
        {
        }
    }

    [HarmonyPatch(typeof(Toils_Haul), "PlaceHauledThingInCell")]
    public class Patch_PlaceHauledThingInCell_Toil {
        public static void Postfix(Toil __result, TargetIndex cellInd) {
            // make the "initAction" a final action:
            //TODO::::  Make a wrapper around old initAction that doesn't put 
            //  stuff down if failure happens?

//            __result.preInitActions.Add(delegate () { __result.actor.jobs.debugLog = true; });
//            __result.AddFinishAction(__result.initAction);
            // replace it with a test for Deep Storage
            //   and if it IS deep storage, set up wait:
            Action placeStuff = __result.initAction;
            __result.AddPreInitAction(delegate() {
                __result.tickAction = null;
                Pawn actor = __result.actor;
                Job curJob = actor.jobs.curJob;
                IntVec3 cell = curJob.GetTarget(cellInd).Cell;
                if (actor.carryTracker.CarriedThing == null)
                {
                    Log.Error(actor + " tried to place hauled thing in cell but is not hauling anything?", false);
                    return;
                }
                SlotGroup slotGroup = actor.Map.haulDestinationManager.SlotGroupAt(cell);
                ThingComp comp;
                if (slotGroup == null || !(slotGroup?.parent is ThingWithComps) ||
                    (comp = ((ThingWithComps)slotGroup.parent).TryGetComp<CompDeepStorage>()) == null)
                {
                    __result.initAction = placeStuff;
                    return;
                }
                int timeStoringTakes = ((LWM.DeepStorage.Properties)comp.props).timeStoringTakes;
                if (timeStoringTakes <= 0) { // just like vanilla
                    __result.initAction = placeStuff;
                    return;
                }

                __result.initAction = null;
                __result.tickAction = delegate () {
                    if (actor.jobs.curDriver.ticksLeftThisToil == 1) // last tick is 1, not 0
                    {
                        placeStuff();
                    }
                };

                actor.jobs.curDriver.ticksLeftThisToil = timeStoringTakes;

                __result.WithProgressBar(TargetIndex.B, () => 1f -
                          (float)__result.actor.jobs.curDriver.ticksLeftThisToil / timeStoringTakes, true);
                Thing t = actor.CurJob.GetTarget(TargetIndex.A).Thing;

                // Add some end conditions:
                if (t != null)
                    __result.FailOn(() => !slotGroup.parent.Accepts(t));



            }); // added pre-init action!

            // HMM...let's get rid of stuff:
            __result.initAction=null;
            __result.atomicWithPrevious = false;
            __result.defaultCompleteMode = ToilCompleteMode.Delay;
            __result.defaultDuration = 0; // changed by initAction, if needed
            __result.FailOnBurningImmobile(TargetIndex.B);

            // TODO: FailOn&c, EndOn&c
            //  Other fails.....
            //    No longer allowed in that area?
            //    No longer accepts what we're puttin there?
            //    Um?
        } // end Postfix
    }

    ////////ALL THIS NOT ACTUALLY USED - XXX //////////////
    /**********************************************************************
     * Verse/AI/Toils_Haul.cs:  PlaceHauledThingInCell(..)
     * The Haul job has a bunch of sub-jobs ("toils") and this one puts 
     * down whatever was being carried.
     * 
     * We want a pawn to have to spend time to put something into Deep Storage
     * 
     * We insert code into the PlaceHauledThingInCell toil to manipulate the 
     * toil system and insert a "wait" toil before the placement toil.  We 
     * insert a check into the initAction of the toil.  The check will:
     *   * Look in the JobDriver's toils
     *   * If there's already a Deep Storage Wait toil there, then it's
     *     already happened, so move on
     *   * If not:
     *     * Break into the toil system and insert a Wait before the 
     *       PlaceHauledThingInCell toil
     *     * change the toil index to point to the wait toil
     *     * abort the current toil.
     * 
     *  The PlaceHauledThingInCell toil, of course, has everything in a 
     *  delegate() function that we need to find access to.
     * 
     *  Doing it this way still seems simpler than playing with IL jump
     *  tables to insert something into the IEnumerable list..
     * 
     * #DeepMagic #YouHaveBeenWarned #NotAsDeepAsItCouldHaveBeen
     **************************************/
    //[HarmonyPatch(typeof(Toils_Haul))]
    class Patch_PlaceHauledThingInCell_Toil_Delegate {
        static MethodBase TargetMethod () {
            Type predicateClass = typeof(Verse.AI.Toils_Haul).GetNestedTypes(Harmony.AccessTools.all)
                                      .FirstOrDefault(t => t.FullName.Contains("c__AnonStorey6"));
            if (predicateClass == null)
            {
                Log.Error("LWM.Deep_Storage: Could not find Verse.AI.Toils_Haul:c__AnonStorey6");
                return null;
            }
            Log.Error("cuessessss!");
            // This fails, by the way:  predicateClass.GetMethod(alreadyFoundMethod.Name).
            //   No idea why.
            // Is this matching <>m__0?  Or is it returning the first one, which is what
            //   we want anyway?  Who knows!  But this works.  #DeepMagic
            var m = predicateClass.GetMethods(AccessTools.all)
                                 .FirstOrDefault(t => t.Name.Contains("m__"));
            if (m == null)
            {
                Log.Error("LWM.Deep_Storage: Cound not find Verse.AI.Toils_Haul:c__AnonStorey0<>m__0");
            }
            Log.Error("cuessessss!  Done.");
            return m;
        }


        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            int i = 0; // We will use more than one for loop to get through the original code
            bool foundMagicString = false; // make sure we're on the right track by finding error messages

            // We insert out check right at the beginning.  Ish.
            //   The only thing we wait for is slotGroup to be declared, which we need for our test.
            for (; i < code.Count; i++)
            {
                //Log.Warning("Instruction " + i + ": " + code[i].ToString()+" - "+code[i].operand?.GetType());
                yield return code[i];
                if (code[i].opcode == OpCodes.Ldstr &&
                   (string)code[i].operand == " tried to place hauled thing in cell but is not hauling anything.")
                {
                    foundMagicString = true;
                }
                // stloc.s 4 - store the slotGroup!
                if (foundMagicString && code[i].opcode == OpCodes.Stloc_S &&
                    code[i].operand != null &&
                    ((LocalBuilder)code[i].operand).LocalIndex == 4) // ...really? *really*??
                {
                    if (!((code[i + 1].opcode == OpCodes.Ldloc_S) && // sanity check
                          code[i+1].operand != null && 
                          ((LocalBuilder)code[i + 1].operand).LocalIndex == 4))
                    {
                        throw new Exception("LWM.Deep_Storage: Patching PlacedHauledThingInCell found Stloc_S 4 but not Ldloc_S 4 - version may be incorrect.  Aborting.");
                    }
                    i++;
                    break; // done with this for loop
                }
            }
            if (!foundMagicString) // sanity check
                throw new Exception("LWM.Deep_Storage: Patching PlacedHauledThingInCell could not find anchor string - version may be incorrect.  Aborting.");

            // This is what we want to say:
            // if (LWM.DeepStorage.Wait_Utils.InsertingDeepStorageToil(pawn, slotGroup)) {
            //    return; // Because insertion happened and we need to quit right now
            // }
            yield return new CodeInstruction(OpCodes.Ldloc_1);    // the pawn
            yield return new CodeInstruction(OpCodes.Ldloc_S, 4); // the slotGroup
            yield return new CodeInstruction(OpCodes.Call, Harmony.AccessTools
                         .Method("LWM.DeepStorage.Wait_Utils:InsertingDeepStorageToil"));
            Label label = new Label(); // WRONG: Use ILGenerator's DefineLabel
            yield return new CodeInstruction(OpCodes.Brfalse, label);
            yield return new CodeInstruction(OpCodes.Ret);
            code[i].labels.Add(label);
            for (; i < code.Count; i++) { // okay, done here!
                if (i<100)Log.Warning("Opcode " + i + ": " + code[i].ToString());
                yield return code[i];
            }
            yield break;
        } // end transpiler




    }



    public static class Wait_Utils
    {
        static public bool InsertingDeepStorageToil(Pawn pawn, SlotGroup slotGroup) {
            ThingComp comp;
            if (slotGroup == null || !(slotGroup?.parent is ThingWithComps) ||
                (comp=((ThingWithComps)slotGroup.parent).TryGetComp<CompDeepStorage>()) == null)
            {
                Log.Warning("No Deep Storage Wait Toil Insertion");
                return false;
            }

            Log.Error("InsertingDeepStorageToil called!");




            return true;
        }

        static public Toil DeepStorageWait() {
            Toil toil = new Toil();
            toil.initAction = delegate () {
                Pawn actor = toil.actor;
                actor.pather.StopDead();
                actor.jobs.curDriver.ticksLeftThisToil = 1000;
                // reserve things
            };
            toil.WithProgressBar(TargetIndex.None, delegate
            {
                return 1f - (float)(toil.actor.jobs.curDriver.ticksLeftThisToil / 1000);

            });
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.AddFinishAction(delegate () {
                // release reservations
            });
            //toil.FailOn&c...

            return toil;
        }

    }


}
