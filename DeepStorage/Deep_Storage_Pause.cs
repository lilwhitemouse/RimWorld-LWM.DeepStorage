using System;
using RimWorld;
using Verse;
using Verse.AI;
using HarmonyLib;
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
     * We always take the old initAction (placeStuff below), which 
     *   does the actual placing, and set it to always happen on 
     *   ticksRemaining==1.
     *   We do this regardless of the test in InitActon for whether
     *   we are going into DeepStorage
     *   Why?  Saved games.  InitAction may not get called if
     *   we are already in the middle of the toil when the game
     *   was saved.  But we still need to put down the stuff.
     * 
     * We change the initAction to do a test for DeepStorage.
     *   If is IS, it sets up the waiting.
     *  -remove the original initAction (still going to happen on tick 1)
     *       (placeStuff)
     *  -add progress bar (doesn't happen on load, btw)
     *   (could change if it's ever important)
     *  -counts ticks, etc.
     *   If it ISN'T, it just calls the original placeAction.
     * Also done, setting up fail conditions.    
     *   (still some TODO)
     * 
     * NOTE: Better approach might be to patch the toil creation  \
     * (not  in the JobDriver_HaulToCell, but that would involve   --- Probably not - not all mods use the same job
     * true) Transpiler editing of an IEnumerable jump table.     /
     * 
     */
    [HarmonyPatch(typeof(Toils_Haul), "PlaceHauledThingInCell")]
    public static class Patch_PlaceHauledThingInCell_Toil
    {
        public static void Postfix(Toil __result, TargetIndex cellInd)
        {
            Utils.Warn(PlaceHauledThingInCell, "Starting new haul job, toils created");

            //TODO?  Make a wrapper around old initAction that doesn't put 
            //  stuff down if failure happens?

            Action placeStuff = __result.initAction;
            // NOTE: none of this initAtion happens if the game is being loaded while storing is going on:
            //   This means, among other things, that pawns don't get progress bars on reload
            //   I could make it happen if it ever gets to be important...
            __result.initAction=delegate ()
            {
//                __result.defaultCompleteMode = ToilCompleteMode.Instant;
                Pawn actor = __result.actor;
                Job curJob = actor.jobs.curJob;
                IntVec3 cell = curJob.GetTarget(cellInd).Cell;
                Utils.Warn(PlaceHauledThingInCell, "initAction called for " + actor+"'s haul job "
                           +curJob.def.driverClass+" to "+cell+" (toil "+actor.jobs.curDriver.CurToilIndex+")");
                //                Log.Error("Place Hauled Thing in Cell:  Toil preInit!  Putting in "+cell.ToString());
                //                actor.jobs.debugLog = true;
                if (actor.carryTracker.CarriedThing == null)
                { // error as per original toil code (v1.0 or something)
                    Log.Error(actor + " tried to place hauled thing in cell but is not hauling anything?");
                    return;
                }
                SlotGroup slotGroup = actor.Map.haulDestinationManager.SlotGroupAt(cell);
                CompDeepStorage cds;
                if (!(slotGroup?.parent is ThingWithComps) ||
                    (cds = (((ThingWithComps)slotGroup?.parent)?.GetComp<CompDeepStorage>()))==null)
                {
                    Utils.Warn(PlaceHauledThingInCell, "not going into Deep Storage");
                    // Pick Up & Haul reuses Toils; I realized this meant I need to keep original placeStuff() around:
                    // Also, is it possible another mod does something weird with placeStuff?
                    //   this is a cheap "just to be on the safe side" check:
                    if (placeStuff!=null) placeStuff();
                    return;
                }
                int timeStoringTakes = cds.TimeStoringTakes(actor.Map,cell, actor);
                timeStoringTakes =(int)(timeStoringTakes*Settings.storingGlobalScale);
                if (timeStoringTakes <= 1
                    || !Settings.storingTakesTime ) //boo, hiss, but some ppl use it
                { // just like vanilla
                    Utils.Warn(PlaceHauledThingInCell, "Instantaneous storing time");
                    if (placeStuff!=null) placeStuff();
                    return;
                }
//                __result.defaultCompleteMode = ToilCompleteMode.Delay;
                if (actor.jobs.curDriver.ticksLeftThisToil < 1) // test is probably superfluous
                {
                    actor.jobs.curDriver.ticksLeftThisToil = timeStoringTakes;
                }
                Utils.Mess(PlaceHauledThingInCell, "  Storing time set to: "+actor.jobs.curDriver.ticksLeftThisToil);
                // It'd be nice to have a progress bar for deep storage
                __result.WithProgressBar(TargetIndex.B, () => 1f -
                          (float)__result.actor.jobs.curDriver.ticksLeftThisToil / timeStoringTakes, true);

                /***** Add some end conditions: *****/
                //TODO: .....is this even a good idea?  if the pawn is moving a lot of things at once
                //   (I'm looking at you Mehni's Pick Up and Haul), this could break the entire chain.
                // TODO: Replace this with a check MUCH earlier.  And one right before actual placement
                //    takes place.
/*                Thing t = actor.CurJob.GetTarget(TargetIndex.A).Thing;
                if (t != null)
                    __result.FailOn(() => !slotGroup.parent.Accepts(t));
                    */
                //TODO: Find a way to track the end condition? Remove it?
                //  Or just make it complicated :p
//                __result.FailOn(delegate() {});
                // TODO: any other end conditions?  Fail conditions?
                // TODO: Any reservations?






            }; // changed initAction!
            __result.defaultCompleteMode = ToilCompleteMode.Delay;
            // The tickAction is only called if we are going into Deep Storage,
            //   otherwise the toil is over after initAction and no ticks happen.
            // This will still get called even on load/save, because ticks count down.
            __result.tickAction = delegate ()
            {
                Pawn pawn = __result.actor;
                //Utils.Mess(PlaceHauledThingInCell, 
                //           "  "+pawn+"'s ticks left: " + __result.actor.jobs.curDriver.ticksLeftThisToil);
                if (pawn.jobs.curDriver.ticksLeftThisToil <= 1) // last tick is 1, not 0
                {
                    Utils.Warn(PlaceHauledThingInCell, "  "+pawn+" hit "+pawn.jobs.curDriver.ticksLeftThisToil+
                               " ticks; about to put down "+(pawn.carryTracker.CarriedThing!=null?
                                                             (""+pawn.carryTracker.CarriedThing.stackCount+
                                                              pawn.carryTracker.CarriedThing):
                                                             "NULL ITEM"));
                    if (placeStuff!=null) placeStuff();
                    return;
                }
                /*
                if (pawn.jobs.curDriver.ticksLeftThisToil%50==0) {
                    // TODO: fail conditions
                    ...etc...
                    pawn.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                }
                */
            };

            // ToilCompleteMode.Delay is acceptable in vanilla case, as duration is 0:

//            __result.defaultDuration = 0; // changed by PreInitAction, if needed
            // This is okay: it checks the current TargetIndex.B
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
