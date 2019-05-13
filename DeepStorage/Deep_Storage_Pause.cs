using System;
using RimWorld;
using Verse;
using Verse.AI;
using Harmony;
using static LWM.DeepStorage.Utils.DBF; // trace utils

// NOTE: PlaceCarriedThingInCellFacing not actually used?  So no need to patch it?

namespace LWM.DeepStorage
{
    /**********************************************************************
     * Verse/AI/Toils_Haul.cs: Toil PlaceHauledThingInCell(...)   
     * 
     * We want to add a wait while the pawn puts things into Deep Storage,
     *   so we patch the PlaceHauledThingInCell toil to add the wait.
     * 
     * The way the jobdriver and toils for hauling are set up, when
     * PlaceHauledThingInCell(...) is called to create the Toil, we
     * can't actually tell if the Thing is being carried to Deep Storage.
     * Annoying.  So we create a PreInitAction for the Toil that checks!
     * 
     * If we aren't going into DeepStorage, the initAction happens
     *   instantly, no ticks happen in waiting, and the entire
     *   placement happens at once.
     *
     * We always take the old initAction - which does the actual
     *   placing and set it to happen on ticksRemaining==1
     *   We do this regardless of the test in PreInitAction.
     *   Why?  Saved games.  PreInitAction doesn't get called if
     *   we are already in the middle of the toil when the game
     *   was saved.  But the ticks still happen, so we need
     *   an end condition.    
     * 
     * If the PreInitAction sees it IS going to DeepStorage, 
     *   it sets up the waiting.
     *  -remove the original initAction (still going to happen on tick 1)
     *  -add progress bar (doesn't happen on load, btw)
     *   (could change if it's ever important)
     *  -counts ticks, etc.
     *     
     * Also done, setting up fail conditions.    
     *   (still some TODO)
     * 
     * NOTE: Better approach would be to patch the toil creation
     *       in the JobDriver_HaulToCell, but that would involve
     *       Transpiler editing of an IEnumerable jump table.
     * 
     *       Not this week.
     */
    [HarmonyPatch(typeof(Toils_Haul), "PlaceHauledThingInCell")]
    public static class Patch_PlaceHauledThingInCell_Toil
    {
        public static void Postfix(Toil __result, TargetIndex cellInd)
        {
            Utils.Err(PlaceHauledThingInCell, "Starting new haul job, toils created");

            //TODO?  Make a wrapper around old initAction that doesn't put 
            //  stuff down if failure happens?

            Action placeStuff = __result.initAction;
            // NOTE: none of this PreInitAtion happens if the game is being loaded while storing is going on:
            //   This means, among other things, that pawns don't get progress bars on reload
            //   I could make it happen if it ever gets to be important...
            __result.AddPreInitAction(delegate ()
            {
                Pawn actor = __result.actor;
                Job curJob = actor.jobs.curJob;
                IntVec3 cell = curJob.GetTarget(cellInd).Cell;
                Utils.Err(PlaceHauledThingInCell, "PreInitAction called for " + actor+"'s haul job "+curJob.def.driverClass+" to "+cell);
                //                Log.Error("Place Hauled Thing in Cell:  Toil preInit!  Putting in "+cell.ToString());
                //                actor.jobs.debugLog = true;
                if (actor.carryTracker.CarriedThing == null)
                { // error as per original toil code
                    Log.Error(actor + " tried to place hauled thing in cell but is not hauling anything?", false);
                    return;
                }
                SlotGroup slotGroup = actor.Map.haulDestinationManager.SlotGroupAt(cell);
                CompDeepStorage cds;
                if (!(slotGroup?.parent is ThingWithComps) ||
                    (cds = (((ThingWithComps)slotGroup?.parent)?.GetComp<CompDeepStorage>()))==null)
                {
                    Utils.Warn(PlaceHauledThingInCell, "not going into Deep Storage");
                    // Pick Up & Haul reuses Toils, maybe I broke this one in an earlier run:
                    if (__result.initAction==null) {
                        Utils.Warn(PlaceHauledThingInCell, "  (restoring initAction)");
                        __result.initAction=placeStuff;
                        return;
                    }
                    return; // initAction still around, will handle
                }
                int timeStoringTakes = cds.timeStoringTakes(cell);
                if (timeStoringTakes <= 0)
                { // just like vanilla
                    Utils.Warn(PlaceHauledThingInCell, "Instantaneous storing time");
                    return;
                }
                // Remove the initAction so it doesn't happen before waiting starts:
                __result.initAction = null;
                if (actor.jobs.curDriver.ticksLeftThisToil < 1) // test is probably superfluous
                {
                    actor.jobs.curDriver.ticksLeftThisToil = timeStoringTakes;
                }
                // It'd be nice to have a progress bar for deep storage
                __result.WithProgressBar(TargetIndex.B, () => 1f -
                          (float)__result.actor.jobs.curDriver.ticksLeftThisToil / timeStoringTakes, true);

                /***** Add some end conditions: *****/

                Thing t = actor.CurJob.GetTarget(TargetIndex.A).Thing;
                if (t != null)
                    __result.FailOn(() => !slotGroup.parent.Accepts(t));

                // TODO: any other end conditions?  Fail conditions?
                // TODO: Any reservations?






            }); // added pre-init action!

            // The tickAction is only called if we are going into Deep Storage,
            //   otherwise the toil is over after initAction and no ticks happen.
            // This will still get called even on load/save, because ticks count down.
            __result.tickAction = delegate ()
            {
                Pawn pawn = __result.actor;
                Utils.Warn(PlaceHauledThingInCell, 
                           "  "+pawn+"'s ticks left: " + __result.actor.jobs.curDriver.ticksLeftThisToil);
                if (pawn.jobs.curDriver.ticksLeftThisToil <= 1) // last tick is 1, not 0
                {
                    Utils.Err(PlaceHauledThingInCell, "Hit 0 ticks");
                    placeStuff();
                }
            };

            // ToilCompleteMode.Delay is acceptable in vanilla case, as duration is 0:
            __result.defaultCompleteMode = ToilCompleteMode.Delay;
            __result.defaultDuration = 0; // changed by PreInitAction, if needed
            __result.FailOnBurningImmobile(TargetIndex.B);

            // todo reservations (possibly?)
            // also todo, undo reservations (possibly?) (as another final action?)

            // TODO: FailOn&c, EndOn&c
            //  Other fails.....
            //    No longer allowed in that area?  <--- Very definitely TODO
            //    Um?
        } // end Postfix
    }



}
