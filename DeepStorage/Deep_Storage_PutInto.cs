using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using UnityEngine;

using static LWM.DeepStorage.Utils.DBF;





namespace LWM.DeepStorage
{
    /*  Patch TryPlaceDirect(...) and Spawn(...)  */


    /**********************************************************************
     * Verse/GenPlace.cs:  TryPlaceDirect(...)
     * This handles putting something down on the ground or in a slotGroup (storage)
     * 
     * We patch the original by waiting until it's dropped/placed whatever it can 
     * and then seeing if it was putting stuff in a Deep Storage unit.  If it was,
     * then it might not have put down everything it can!  So we check and if 
     * there is anything else to place, we check for room, etc.
     * 
     * We also check to make sure the stacks are tidy - otherwise it may be impossible
     * for pawns to clean up stacks ("merge" jobs), where the pawn picks up from the 
     * small top stack to merge with a lower stack...but then puts it right back down
     * on top - oops.
     * 
     * After the unpatched version runs, it returns false iff:
     *   1)  a flag is thrown - because something is trying to place more than a stacklimit?
     *       (so, for example, trying to place 100 wood, when the stacklimit is 75)
     *       This probably only happens if a pawn somehow ends up carrying a large number
     *       of things, like if they butcher a big animal and have a lot of meat.
     *   2)  Only some of the stack (perhaps 0) could actually be placed...
     * In case 2, if we were placing something in Deep Storage, there might still
     * be room, so we try again.
     * 
     * We patch via prefix to catch case 1) above.
     * We patch via postfix to catch case 2).
     * We also patch via postfix to make sure all of the stacks are tidy
     **************************************/
    // TODO: can we traspile this?  No?
    // todo1.4
    // todo1.4 - what about weight?  Okay, will have to revisit this in its entirety :(
    //[HarmonyPatch(typeof(Verse.GenPlace), "TryPlaceDirect")]
    class Patch_TryPlaceDirect {
        static void Prefix(ref Thing __state, Thing thing, IntVec3 loc)
        {
            // TryPlaceDirect changes what the variable "thing" points to
            //   We do actually need the original value of "thing":
            //  #HarmonyGotcha
            __state = thing;
            Utils.Err(TryPlaceDirect, "LWM:TryPlaceDirect: going to place " + thing.stackCount + thing + " at " + loc);
            #if false
            Map map; // please add parameter in Previx() line
            List<Thing> thingList = loc.GetThingList(map);
            int i=0;
            while (i < thingList.Count)
            {
                Thing thing3 = thingList[i];
                Log.Warning("Things here: "+thingList[i].stackCount+thingList[i]);
                if (!thing3.CanStackWith(thing))
                {
                    Log.Warning("Cannot stack");
                    i++;
                }
                else
                {
                    int stackCount = thing.stackCount;
                    if (thing3.TryAbsorbStack(thing, true))
                    {
                        Log.Error("Thing3 absorbed the stack!!!!");
                        return;
                    }
                    Log.Error("Tried to absorb, but failed.  ResultingThing is null??");
                }
            }
            Log.Error("...everything passed");
            #endif
        }
        static void Postfix(ref bool __result, Thing __state, IntVec3 loc, Map map,
                            ref Thing resultingThing, Action<Thing, int> placedAction) {
            Thing thing = __state;
            // First check if we're dropping off in Deep Storage:
            SlotGroup slotGroup = loc.GetSlotGroup(map);

            if (slotGroup == null || !(slotGroup?.parent is ThingWithComps) ||
                ((ThingWithComps)slotGroup.parent).TryGetComp<CompDeepStorage>() == null) {
                Utils.Warn(TryPlaceDirect, "  (placed "+__state+" NOT in Deep Storage: with result " + __result + ")");
                return;
            }

            if (resultingThing != null) {
                // Two cases here if resultingThing exists:
                //   1. There's a new object on the map
                //        (so resultingThing is the original thing, we don't have anyting to put down)
                //   2. Everything we put down was able to stack with something on the map
                //        (so nothing else to put down!)
                //   (case three - something weird going on possibly involving carrying?)
                //        (all bets are off anyway >_< )
                // Probably, the pawn put down what they were carrying, and all is good.
                Utils.Warn(TryPlaceDirect, "  successfully placed " + resultingThing.stackCount + resultingThing.ToString()+" ("+__result+")");
                Utils.TidyStacksOf(resultingThing);
                return;
            }
            // Ok, so we still have something we want to place, and it goes in Deep Storage
            //   (We will still return __result=false if we can't place everything in the desired space.)

            Utils.Err(TryPlaceDirect, "LWM:TryPlaceDirect tried to place " + thing.ToString() + " in " + slotGroup.ToString());

            // Let's see if there's still room in the Deep Storage area the pawn is using:
            List<Thing> list = map.thingGrid.ThingsListAt(loc);
            int maxNumberStacks = ((ThingWithComps)slotGroup.parent).GetComp<CompDeepStorage>().MaxNumberStacks;
            // We know there was at least one thing here, and it either doesn't stack with our thing
            //   or its stack is full.
            // So, we go thru the items that are there
            int thingsHere = 0;
            for (int i = 0; i < list.Count; i++) {
                Thing thing2 = list[i];
                if (!thing2.def.EverStorable(false)) {
                    //not an object we count
                    continue;
                }
                thingsHere++;
                Utils.Warn(TryPlaceDirect, "  Currently there are "+thingsHere+
                           " things here, max: "+maxNumberStacks);
                if (thingsHere==1) {
                    //  (should be safe because 1st haulable stack would already have been
                    //   tested by default vanilla function)
                    continue;
                }
                //unfortunately, we have to duplicate some of the original code:
                if (!thing2.CanStackWith(thing)) {
                    Utils.Warn(TryPlaceDirect, "...ignoring \"other\" stack " + thing2.ToString());
                    continue;  // am carrying wood, but this is sheep.  Or rock, whatever.
                }
                // Okay, can stack.
                if (thing2.stackCount >= thing2.def.stackLimit) {
                    Utils.Warn(TryPlaceDirect, "...ignoring full stack " + thing2.ToString());
                    continue; // stack is full.
                }
                // Put some down in the non-full stack!
                var origStackCount = thing.stackCount;
                if (thing2.TryAbsorbStack(thing, true)) {
                    // the "thing2" stack could hold everything we wanted to put down!
                    Utils.Warn(TryPlaceDirect, "... Object " + thing2.ToString() + " absorbed ALL of " + thing.ToString());
                    resultingThing = thing2;
                    // Okay, I like this notation:  only call if non-null:
                    placedAction?.Invoke(thing2, origStackCount);
                    Utils.TidyStacksOf(thing2);
                    __result = true; // Could put down everything!
                    return;
                }
                Utils.Warn(TryPlaceDirect, "... Object " + thing2.ToString() + " absorbed SOME of " + thing.ToString());
                // Since we tried to put some down in that stack, do we do placedAction?
                if (placedAction != null && origStackCount != thing.stackCount) {
                    placedAction(thing2, origStackCount - thing.stackCount);
                }
                // ...but there's still more to place, so keep going:
            } // end loop of objects in this location
            if (thingsHere >= maxNumberStacks) { // Ran out of room in the storage object but still want to put stuff down ;_;
                Utils.Warn(TryPlaceDirect, "...But ran out of stack space here: all " + maxNumberStacks + " filled");
                __result = false;
                return; // no need to TidyStacks here - they have to be all full
            }
            // if we reach here, we found an empty space,
            //   so put something down!
            Utils.Warn(TryPlaceDirect, "...Found empty stack space at " + loc.ToString() +
                       ". Trying to create from " + thing.ToString());
            // In some circumstances (e.g., butchering a large animal), a pawn can be putting
            //   down way more than stackLimit of something.  We deal with that scenario here
            //////////////////////// NOTE:  POSSIBLE PROBLEM: ////////////////////
            //   resultingThing is set to only the last thing here - it's possible we miss something
            //   important by placing as much as we can at once:
            while (thing.stackCount > thing.def.stackLimit) {// put down part of a carried load?
                Thing thing2 = thing.SplitOff(thing.def.stackLimit);
                resultingThing = GenSpawn.Spawn(thing2, loc, map);
                Utils.Warn(TryPlaceDirect, "...put down one full stack ("
                           + resultingThing.ToString() + "); " + thing.stackCount + " left");
                placedAction?.Invoke(thing2, thing2.stackCount);
                thingsHere++;
                if (thingsHere >= maxNumberStacks) { // Oh dear.  There was still at least SOME left...
                    Utils.Warn(TryPlaceDirect, "...But ran out of stack space here: all "
                               + maxNumberStacks + " filled but still have " + thing.stackCount + " left");
                    __result = false; // couldn't put things down
                    return; // no need to TidyStacks - they all have to be full
                }
            }
            // Either Spawn the thing (or Spawn the final part of the thing):
            resultingThing = GenSpawn.Spawn(thing, loc, map);
            __result = true; // nothing unplaced!

            Utils.Warn(TryPlaceDirect, "...created " + resultingThing.ToString());
            placedAction?.Invoke(thing, thing.stackCount);// Okay, I like this notation
            Utils.TidyStacksOf(resultingThing);
            return; // with __result=!flag (probably "true");
        } // end TryPlaceDirect's Postfix
    } // end patching TryPlaceDirect



    /**********************************************************************
     * Verse/GenSpawn.cs:  Spawn(...)
     * This is THE function that puts things on the map.
     * 
     * Prior to B0.19, Spawn would happily place one item on top of another
     * item.  That changed in B.019, and obviously, this is a problem for 
     * us.  Currently, there is a check during spawning, and if there is
     * another "item"-category object there, that item is bumped aside
     * (placed in "near" mode).
     *     
     * We want items to get placed on top of each other in Deep Storage, 
     * and on loading the game.  If things get bumped around during play,
     * that's (probably) up to the player to deal with.    
     * 
     * We patch Spawn(...) to change the test for "bumping" from
     *     if (newThing.def.category == ThingCategory.Item)
     * to
     *     if (newThing.def.category == ThingCategory.Item && 
     *         !respawningAfterLoad
     *         !LWM.DeepStorage.Utils.CanStoreMoreThanOneThingAt(Map,loc))
     * (I could probably manage without the Utils function, but this is
     *  much easier in terms of C# -> IL magic)
     **************************************/
    //todo1.4 - I think we don't need this aaaaany more :D
    //[HarmonyPatch(typeof(Verse.GenSpawn), "Spawn", new Type[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(Rot4), typeof(WipeMode), typeof(bool) })]
    class Patch_GenSpawn_Spawn {
        //        static void Prefix (Thing newThing) {
        //            Log.Warning("Spawn: " + newThing.ToString() + ".  Destroyed? " + newThing.Destroyed);
        //        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            // replace if (newThing.def.category == ThingCategory.Item)
            // with
            //         if (newThing.def.category == ThingCategory.Item && 
            //             !respawningAfterLoad &&
            //             !LWM.DeepStorage.Utils.CanStoreMoreThanOneThingAt(Map,loc))
            // The original branch in the code is at:
            //          /* 0x002B85A2 02           */ IL_015E: ldarg.0
            //          /* 0x002B85A3 7B93360004   */ IL_015F: ldfld     class Verse.ThingDef Verse.Thing::def
            //          /* 0x002B85A8 7BCB2C0004   */ IL_0164: ldfld valuetype Verse.ThingCategory Verse.ThingDef::category
            //          /* 0x002B85AD 18           */ IL_0169: ldc.i4.2
            //          /* 0x002B85AE 40BA000000   */ IL_016A: bne.un IL_0229
            //  We find that spot (it's the first time .def.category comes up)
            //  and then add a second check for CanStoreMoreTHanOneThingAt, also jumping to IL0229 
            //  if we fail(pass?).
            var code = new List<CodeInstruction>(instructions);
            var i = 0; // using two for loops
            for (; i < code.Count; i++) {
                yield return code[i];
                if (code[i].opcode == OpCodes.Ldarg_0) { // thing
                    if (code[i + 1]?.opcode == OpCodes.Ldfld && // thing.def
                        (FieldInfo)code[i + 1]?.operand == typeof(Verse.Thing).GetField("def")) {
                        i++;
                        yield return code[i];
                        if (code[i + 1]?.opcode == OpCodes.Ldfld && //thing.def.category!
                            (FieldInfo)code[i + 1]?.operand == typeof(Verse.ThingDef).GetField("category")) {
                            i++;
                            // 
                            yield return code[i++]; // the category
                            yield return code[i++]; // ldc.i4.2  ("item" category)
                            // i now points to the branch operation; we need the label
                            System.Reflection.Emit.Label branchLabel;
                            branchLabel = (Label)code[i].operand;
                            yield return code[i++]; // branch if not equal to branchLabel
                            CodeInstruction c;
                            // if (respawningAfterLoad) jump...
                            c = new CodeInstruction(OpCodes.Ldarg_S, 5); // 5 is respawningAfterLoad
                            yield return c;
                            c = new CodeInstruction(OpCodes.Brtrue,branchLabel);
                            yield return c;
                            // if (CanStoreMoreThanOneThingAt(map,loc)) jump...
                            c = new CodeInstruction(OpCodes.Ldarg_2); // map
                            yield return c;
                            c = new CodeInstruction(OpCodes.Ldarg_1); // loc
                            yield return c;
                            c = new CodeInstruction(OpCodes.Call, HarmonyLib.AccessTools.Method(
                                "LWM.DeepStorage.Utils:CanStoreMoreThanOneThingAt"));
                            yield return c; // Utils.CanStoreMoreThanOneThingAt(map, loc);
                            c = new CodeInstruction(OpCodes.Brtrue, branchLabel);
                            yield return c; // if CanStoreMoreThanOneThing, skip this section
                            break; // Done with this for loop                            
                        }
                    }
                }
            }
            // finish the rest of the function:
            for (; i < code.Count; i++) {
                yield return code[i];
            }
            yield break;
        }
    } // done with patching GenSpawn's Spawn(...)


}
