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
     * Patch_GenThing_ItemCenterAt
     * 
     * Problem: If you have 10 of the same item in storage, vanilla will happily
     *   draw all ten of them in a line going off to the upper right out of the
     *   actual cell they are supposed to be in...
     * 
     * GenThing's ItemCenterAt is what decides *where* to draw items (that is, 
     *   things that can be in storage(?)). It does some fancy counting to see
     *   how many Items there are; if there are more than one, they must be in
     *   storage! If they are all the same ThingDef (but not weapons) they get
     *   spread out in a line from some point, some point + .11f,,.24f, & some
     *   point + .22f,,.48f.
     * 
     * We change this so that instead of adding some Px + .11f * numItemsBelow
     *   we add Px + (.22f / (totalNumItems -1)) * numItems Below
     *   and instead of adding Pz + .24f * numItemsBelow
     *   we add Pz + (.48f / (totalNumItems - 1) * numItemsBelow
     * 
     * We do this by looking for .11f and .24f and adding our code in there!
     *   Easy peasy lemon squeezy!
     *     
     */
    [HarmonyPatch(typeof(GenThing), "ItemCenterAt")]
    public static class Patch_GenThing_ItemCenterAt
    {
        public static bool useBoringOldStackingGraphic = false;
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool found1 = false;
            bool found2 = false;
            if (useBoringOldStackingGraphic) // If this branch of transpiler is selcted, 
                // we are rather more brutal in where we prune the code....
            {   // Basically, we want v1.3 behavior: all items simply drawn on top of each other
                var list = instructions.ToList();
                for (int i = 0; i< list.Count; i++)
                {
                    // Looking for: if (numberTotalItems <= 1) { ...do boring graphic placement... }
                    /* IL_00ad: ldloc.1
                     * IL_00ae: ldc.i4.1
                     * IL_00af: bgt.s IL_00cf
                     */                    
                    // Will replacae with: if (true)
                    if (list[i].opcode   == OpCodes.Ldloc_1 &&   // numberTotalItems
                        list[i+1].opcode == OpCodes.Ldc_I4_1 &&  // 1
                        list[i+2].opcode == OpCodes.Bgt_S)       // if (num > 1) jump to fancy logic we don't want
                    {
                        i++; // skip Ldloc_1
                        i++; // skip 1
                        i++; // skip that unwanted jump to fancy logic
                        for (int j=i; j<list.Count; j++)
                        {
                            yield return list[j];
                        }
                        yield break;
                    }
                    else
                    {
                        yield return list[i];
                    }
                }
                Log.Warning("LWM.DeepStorage: Transpiler failed to break jump to fancy stacking graphics");
                yield break; // Should never reach this, but if we do....???
            }
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldc_R4 // from the IL code
                    && instruction.OperandIs(.11f))
                {
                    if (found1)
                    {
                        Log.Error("LWM.DeepStorage tried to patch .11f but found it twice! Let them know!");
                        yield return instruction;
                    }
                    else
                    {
                        yield return new CodeInstruction(instruction.opcode, .22f); // .22f
                        yield return new CodeInstruction(OpCodes.Ldloc_1); // .22f, totalNumberItemsHere
                        yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                        yield return new CodeInstruction(OpCodes.Sub); // .22f, (totalNumItemsHere - 1)
                        // Don't know if this is actually necessary, but seems like should be?
                        yield return new CodeInstruction(OpCodes.Conv_R4); // .22f, (totalNumIH - 1)f
                        yield return new CodeInstruction(OpCodes.Div); // .22f/(totalNumItemsHere - 1) instead of .11f
                        found1 = true;
                    }
                }
                else if (instruction.opcode == OpCodes.Ldc_R4 // still from the IL code
                    && instruction.OperandIs(.24f))
                {
                    if (found2)
                    {
                        Log.Error("LWM.DeepStorage tried to patch .24f but found it twice! Let them know!");
                        yield return instruction;
                    }
                    else
                    {
                        yield return new CodeInstruction(instruction.opcode, .48f); // .48f
                        yield return new CodeInstruction(OpCodes.Ldloc_1); // .48f, totalNumberItemsHere
                        yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                        yield return new CodeInstruction(OpCodes.Sub); // .48f, (totalNumItemsHere - 1)
                        // Still don't know if this is actually necessary, but still seems like should be?
                        yield return new CodeInstruction(OpCodes.Conv_R4); // .48f, (totalNumIH - 1)f
                        yield return new CodeInstruction(OpCodes.Div); // .48f/(totalNumItemsHere - 1) instead of .11f
                        found2 = true;
                    }
                }
                else
                {
                    yield return instruction;
                }
            } // end instructions
            if (!found1) Log.Warning("LWM.DeepStorage: failed to Transpile .11f");
            if (!found2) Log.Warning("LWM.DeepStorage: failed to Transpile .24f");
            yield break;
        }
    }
}
