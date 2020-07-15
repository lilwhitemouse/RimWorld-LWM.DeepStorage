using System;
using RimWorld; // haha, needs all these "using" but RimWorld
using Verse;
using Verse.AI;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using System.Collections.Generic;
using System.Linq;
using UnityEngine;




namespace LWM.DeepStorage
{




    /**********************************************************************
     * Verse/AI/Toils_Haul.cs:  StartCarryThing(...)
     * The Haul job has a bunch of sub-jobs ("toils") and this one picks
     * up a thing from the ground.
     * 
     * The code returns a Toil object that has a delegate() initAction
     * 
     * We insert code into the delegate() function created for the Haul toil
     * to get around a problem that pawns have picking up from Deep Storage:
     * If they are picking up a stack of, say, 7 wood, and they need 25, 
     * they don't look for any more wood in the Deep Storage (probably because
     * it has been reserved....by our pawn? Or maybe just b/c the carry logic
     * doesn't look elsewhere?  Who knows.)  So we patch to check if the Thing 
     * being picked up is in Deep Storage, and if it is, see if its stackCount
     * can be adjusted to match the job from other stacks in the Deep Storage unit.
     * #DeepMagic #YouHaveBeenWarned
     **************************************/
    [HarmonyPatch]
    public static class Patch_StartCarryThing_Delegate {
        public static Type predicateClass;
        static MethodBase TargetMethod()//The target method is found using the custom logic defined here
        {
            //c__AnonStorey0 is the hidden IL class that is created for the delegate() function
            // created by StartCarryThing.  We need this class later on.
            //In version 1.1, it's c__DisplayClass1_0
            predicateClass = typeof(Verse.AI.Toils_Haul).GetNestedTypes(HarmonyLib.AccessTools.all)
               .FirstOrDefault(t => t.FullName.Contains("c__DisplayClass1_0"));
            if (predicateClass == null) {
                Log.Error("LWM.Deep_Storage: Could not find Verse.AI.Toils_Haul:c__AnonStorey0");
                return null;
            }
            // This had failed, by the way:  predicateClass.GetMethod(alreadyFoundMethod.Name).
            //   No idea why.
            // Is this matching <>m__0?  Or is it returning the first one, which is what
            //   we want anyway?  Who knows!  But this works.  #DeepMagic
            // Note: v1.0 uses m__; v1.1 uses b__, so b__0
            var m = predicateClass.GetMethods(AccessTools.all)
                                 .FirstOrDefault(t => t.Name.Contains("b__0"));
            if (m == null) {
                Log.Error("LWM.Deep_Storage: Could not find Verse.AI.Toils_Haul:c__AnonStorey0<>m__0");
            }
            return m;
        }
        // StartCarryThing's delegate starts with several (very reasonable) tests.
        //   We want to insert our function call right after the tests, but
        //   before the first bit that checks the stack size of the Thing being carried:
        //   if (failIfStackCountLessThanJobCount && thing.stackCount<curJob.count)
        //        {
        //            actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
        //            return;
        //        }
        //   If the test before this is passed, the code jumps to right where we want
        //     to insert.
        //   On failure, there is code with the unique string
        //      "StartCarryThing got availableStackSpace "
        //   So we look for that string, and then once we've found it, the next
        //     "throw" OpCode marks the end of the test.  That's where we insert our code:
        //
        //TODO:
        //  Better would be to find where AvailableStackSpace is stored, grab that ldloc and then
        //  the following branch has the logic needed!
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var code = new List<CodeInstruction>(instructions);
            // have we passed "StartCarryThing got availableStackSpace "?
            bool foundCorrectException = false;
            // We keep track of where every BrTrue jump goes:
            //    One of them will be where we insert our code:
            System.Reflection.Emit.Label branchLabel = new Label(); // Ugh.  This just gets thrown away
                                                                    // but I get uninitialized var errors otherwise
            int i = 0; // we do 2 for loops on the same i
            for (; i < code.Count - 1; i++) {
                // version 1.1 uses brtrue_S instead of brtrue.  How rude.
                if (code[i].opcode == OpCodes.Brtrue||code[i].opcode==OpCodes.Brtrue_S) { // keep track of where Branch on True are going
                    branchLabel = (Label)code[i].operand;
                } else if (!foundCorrectException && code[i].opcode == OpCodes.Ldstr &&
                      (string)code[i].operand == "StartCarryThing got availableStackSpace ") {
                    // We have passed the correct Branch on True command and 
                    // are in the correct error code - we will insert after the exception is thrown!
                    foundCorrectException = true;
                } else if (foundCorrectException && code[i].opcode == OpCodes.Throw) {
                    yield return code[i]; // the "throw"
                    i++; //advance past throw
                    // We steal the label from the next code and use it as the anchor the
                    //   Brtrue jump goes to:
                    if (!code[i].labels.Remove(branchLabel)) {
                        throw new SystemException("LWM.DeepStorage: Patching StartCarryThing failed: label failure");
                    }
                    // Ldarg_0 - this delegate() function on stack
                    var tmp = new CodeInstruction(OpCodes.Ldarg_0);
                    // It needs the jump label:
                    tmp.labels.Add(branchLabel);
                    yield return tmp;
                    // pop the delegate() function off the stack to load its "toil" field:
                    yield return new CodeInstruction(OpCodes.Ldfld,
                          HarmonyLib.AccessTools.Field(predicateClass, "toil"));
                    yield return new CodeInstruction(OpCodes.Ldloc_2); // This SHOULD be Thing thing.
                    // yield return new CodeInstruction(OpCodes.Ldloc_S, 4); // This SHOULD be num.
                    yield return new CodeInstruction(OpCodes.Ldloc_3); // this should be the v1.1 num
                    // Now call FillThisStackIfAble(toil, thing, num):
                    yield return new CodeInstruction(OpCodes.Call, HarmonyLib.AccessTools
                           .Method("LWM.DeepStorage.Patch_StartCarryThing_Delegate:FillThisStackIfAble"));
                    break; // exit this complicated for loop
                }
                // nothing special, return the instruction
                yield return code[i];
            }
            // Just return almost all the rest:
            // I know there's probably a snappy one-line C# way to do it, but this is fine:
            for (; i < code.Count - 1; i++) {
                yield return code[i];
            }
            var cleanUp = new CodeInstruction(OpCodes.Call, HarmonyLib.AccessTools
                  .Method("LWM.DeepStorage.Patch_StartCarryThing_Delegate:CleanUpStacks"))
            {
                // save the last instruction to swipe any labels/etc from the Return call
                labels = code[i].labels
            };
            yield return cleanUp;

            var retCode = new CodeInstruction(OpCodes.Ret)
            {
                operand = code[i].operand,
                blocks = code[i].blocks
            };
            yield return retCode;
            yield break;
        }
        // helper variables for cleanup after picking thigns up:
        public static Thing tmpThing;
        // Helper function to try to give "thing" a stack size of "job.count"
        //   So if a pawn wants to pick up sheep, and thing is a stack of 7 sheep,
        //   but the job size says to pick up 50 sheep...we look for other stacks
        //   of sheep in the same square to shift some of them to "thing"
        public static void FillThisStackIfAble(Toil toil, Thing thing, int carryCapacity) {
            //Log.Error("FillThisStack with " + thing.ToString() + "("+thing.stackCount+") able to carry " 
            //          + carryCapacity + " of job " + toil.actor.jobs.curJob.count);
            tmpThing = null;
            // We'd like thing to have at least num in our stack to pick up at once:
            int num = Mathf.Min(toil.actor.carryTracker.AvailableStackSpace(thing.def), toil.actor.jobs.curJob.count);
            if (thing.stackCount >= num) { return; }
            var slotGroup = thing.Map.haulDestinationManager.SlotGroupAt(thing.Position);
            if (slotGroup == null || !(slotGroup?.parent is ThingWithComps) ||
                ((ThingWithComps)slotGroup.parent).TryGetComp<CompDeepStorage>() == null) {
                //Giant piles on the ground can be messy, so I am okay with
                //  inefficiently picking stuff up from here.
                //  TODO: an option, perhaps, to avoid this return?
                return;
            }
            tmpThing = thing;
            var tmpMap = thing.Map;
            var tmpLoc = thing.Position;
            var thingsHere = tmpMap.thingGrid.ThingsListAt(tmpLoc);
            // Note:  foreach (Thing otherThing in thingsHere) { ... }
            //         throws an exception if a stack disappears...
            //         because the list changes.  Oops.
            for (int i = 0; i < thingsHere.Count; i++) {
                Thing otherThing = thingsHere[i];
                if (otherThing == thing) { continue; }
                if (!otherThing.CanStackWith(thing)) { continue; }
                if (otherThing.stackCount <= num - thing.stackCount) {
                    thing.TryAbsorbStack(otherThing, false); // false is respectStackLimit:
                                                             // If the pawn can carry it, I don't care.
                                                             // Maybe this is foolish?
                    if (thing.stackCount >= num) { return; }
                    continue;
                }
                otherThing.stackCount -= num - thing.stackCount;
                thing.stackCount = num;

                if (otherThing.Spawned)
                    otherThing.Map.listerMergeables.Notify_ThingStackChanged(otherThing);

                if (thing.Spawned)
                    thing.Map.listerMergeables.Notify_ThingStackChanged(thing);

                return;
            }
        }
        // Make sure stacks of type tmpDef (that is, what the pawn just picked up) are tidy
        // For the record, I don't think this is necessary, but I would rather be safe.
        // CleanUpStacks
        //   Tidy stacks of 74 sheep, 74 sheep, and 74 sheep into stacks of
        //   75 sheep, 75 sheep, and 72 sheep.
        public static void CleanUpStacks() {
            if (tmpThing == null) {
                return; // only do this if the pawn in messing around in Deep Storage
            }
            Utils.TidyStacksOf(tmpThing);
        } //end CleanUpStacks
    }
    //As an aside, pawns will sometimes still refuse to carry stacks.  This behavior
    //  happens with the default shelves, too.  This is not the mod to fix/change that.


}
