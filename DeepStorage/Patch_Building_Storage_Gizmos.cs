using System;
using RimWorld;
using Verse;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using System.Collections.Generic;
using System.Linq;

namespace LWM.DeepStorage
{
    /***************************************************
     * Patch Building_Storage's GetGizmos()
     * 
     * Problem: If you have 345435 items in storage, your screen will fill up
     *   with those cute little bracket markers "Select stored item" thingys.
     *   Which is not great.  They are fantastic gizmos, but for enthusiastic
     *   hoarders, they are a problem.
     * 
     * Goal: Patch the Gizmos to only show, say 10 or something.
     * 
     * Complication: GetGizmos is an IEnumerable. Patching them sucks...
     * 
     * Realistic Goal: only show those Gizmos if there are less than, say, 10
     *   Heck, it's easy enough to make it a mod setting.  Only show if there
     *   are less than 5, 10, 0, etc.  0 can be "don't show them at all" - an
     *   easy first step!    
     * 
     * Solution: Transpile.  The code to produce "select stored item" gizmos
     *   is inside an `if (Find.Selector.NumSelected == 1)` block.  And that
     *   `NumSelected`? That is in ONE PLACE in the code and is easy to find
     *   AND has a branch to skip those gizmos!
     *     
     */
    [HarmonyPatch]
    public static class Patch_Building_Storage_Gizmos
    {
        // -1 for "don't change anything."  "0 for don't display anything"
        //  X for "show if <= X items in this storage building
        static public int cutoffBuildingStorageGizmos = cutoffDefault; // -1 means don't change anything
        public const int cutoffDefault = 12;
        static bool Prepare(Harmony instance)
        {
            return cutoffBuildingStorageGizmos >= 0; // 0 means we transpile away, higher number means we need to check
        }
        static MethodBase TargetMethod()//The target method is found using the custom logic defined here
        {
            // So IEnumerables suck.
            // There is a hidden IL class inside BuildingStorage that GetGizmos uses for the IEnumerable
            //   In the IL it's listed as <GetGizmos>d__43, and the method we want to patch is from that
            //   class.  It's called MoveNext.  If we are lucky, we can do it ALL in one go.
            var method = typeof(RimWorld.Building_Storage).GetNestedType("<GetGizmos>d__43", AccessTools.all)
                    .GetMethod("MoveNext", AccessTools.all);
            if (method == null) Log.Error("LWM.DeepStorage: Transpiler could not find \"<GetGizmos>d__43\" :( ");
            return method;
            /* Another way to go about it, if we ever need it:
             *   (above HAS failed before (perhaps in earlier versions of Harmony?)
            var predicateClass = typeof(RimWorld.Building_Storage).GetNestedTypes(HarmonyLib.AccessTools.all)
               .FirstOrDefault(t => t.FullName.Contains("<GetGizmos>d__43"));
            var m = predicateClass.GetMethods(AccessTools.all)
                                 .FirstOrDefault(t => t.Name.Contains("MoveNext"));
              */
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructionsEnumerable)
        {
            var get_Selector = typeof(Verse.Find).GetMethod("get_Selector");
            var get_NumSelected = typeof(RimWorld.Selector).GetMethod("get_NumSelected");
            Label skipGizmosLabel;
            bool found1 = false;
            var instructions = instructionsEnumerable.ToList();
            for (int i = 0; i < instructions.Count; i++)
            {
                /* We are looking for
                 * Find.get_Selector(); // static
                 * [Selector].get_NumSelected()
                 * load 1
                 * branch if not equal (to skip this set of Gizmos!)
                 *   // this branch has the imporant label we want!!                
                 */
                if (instructions[i].opcode == OpCodes.Call // from the IL code
                    && instructions[i].OperandIs(get_Selector)
                    && instructions[i + 1].opcode == OpCodes.Callvirt
                    && instructions[i + 1].OperandIs(get_NumSelected))
                {
                    skipGizmosLabel = (Label)instructions[i + 3].operand;
                    if (skipGizmosLabel == null)
                    {
                        Log.Error("Transpiler failed to find label");
                        yield return instructions[i];
                        continue;
                    }
                    found1 = true;
                    if (cutoffBuildingStorageGizmos < 1) // we want to skip this ENTIRE SECTION
                    {
                        yield return new CodeInstruction(OpCodes.Br, skipGizmosLabel);
                        i = i + 3; // now pointing at the branch not equal
                        continue;  // we returned something replacing those 4 instructions
                    }
                    //Log.Message("About to Return " + i + ": " + instructions[i].opcode + " " + instructions[i].operand);
                    yield return instructions[i]; // Find.Selector
                    i++;
                    //Log.Message("About to Return " + i + ": " + instructions[i].opcode + " " + instructions[i].operand);
                    yield return instructions[i]; // .NumSelected
                    i++;
                    //Log.Message("About to Return " + i + ": " + instructions[i].opcode + " " + instructions[i].operand);
                    yield return instructions[i]; // load 1
                    i++; // we are now at the "branch not equal"
                    //Log.Message("About to Return " + i + ": " + instructions[i].opcode + " " + instructions[i].operand);
                    yield return instructions[i]; // skip gizmos if more than one building selected
                    /* Now we add our own test:
                     *     if (this.slotGroup.HeldThings.Count > cutoff) branch skipGizmosLabel
                     * Easy, right? Ha.
                     * So.....how about this?
                     *     if (IsOverCutoff(this.slotGroup.HeldThings.GetEnumerator)) branch skipGizmosLabel
                     * That's relatively easy.  Why?  Because the next 5 IL fields give us that
                     * Enumerator! ...Unless someone else patches the exact same spot and changes
                     * those IL codes :laughs::cries::facepalm:&c                    
                     * So how about we just call those instructions directly:
                     */
                    yield return new CodeInstruction(OpCodes.Ldloc_2); // storage building in question
                    yield return new CodeInstruction(OpCodes.Ldfld, typeof(Building_Storage)
                                        .GetField("slotGroup", AccessTools.all)); // .slotGroup
                    yield return new CodeInstruction(OpCodes.Callvirt, typeof(SlotGroup)
                                        .GetMethod("get_HeldThings", AccessTools.all)); // .HeldThings
                    //Log.Warning("Call over threshold");
                    yield return new CodeInstruction(OpCodes.Call,
                                 typeof(Patch_Building_Storage_Gizmos).GetMethod("IsOverThreshold", AccessTools.all));
                    //Log.Warning("Call skip true");
                    yield return new CodeInstruction(OpCodes.Brtrue, skipGizmosLabel);
                }
                else // just a regular instruction
                {
                    // NOTE to anyone doing transpilers: this is an excellent way to debug them
                    //Log.Message("About to return " + i + ": " + instructions[i].opcode + " " + instructions[i].operand);
                    yield return instructions[i];
                }
            } // end instructions for loop
            if (!found1) Log.Warning("LWM.DeepStorage: failed to Transpile Gizmos");
            yield break;
        }
        // Simple and quick way to count if something is over the cuttoff threshold
        static bool IsOverThreshold(IEnumerable<Verse.Thing> things)
        {
            var thingsEnumerator = things.GetEnumerator();
            for (int i = 0; i <= cutoffBuildingStorageGizmos; i++)
            {
                if (thingsEnumerator.MoveNext())
                    continue;
                return false;
            }
            return true;
        }
        // Simple and quick way to count if something is over the cuttoff threshold
        static bool IsOverThresholdX(IEnumerator<Verse.Thing> thingsEnumerator)
        {
            for (int i=0; i<=cutoffBuildingStorageGizmos; i++)
            {
                if (thingsEnumerator.MoveNext())
                    continue;
                return false;
            }
            return true;
        }
    }
}
