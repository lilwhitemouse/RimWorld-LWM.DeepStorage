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
    /*************************************************
     * Along with Patch_GenThing_ItemCenterAt, this patch
     * restores the old 1.3-style graphics.
     * Graphic's Print(...) function contains the following
     * code, which shrinks the item when it's stored:
     * if (thing.MultipleItemsPerCellDrawn())
     *      {
     *          vector *= 0.8f;
     *      }
     * We want to completely remove that, if useBoringOldStackingGraphic
     * The IL code we want to remove (because of course, Transpiler):
     *  IL_0072: ldarg.2                                                                          ->+0
     *  IL_0073: call bool RimWorld.GenLabel::MultipleItemsPerCellDrawn(class Verse.Thing)        ->+1
     *  IL_0078: brfalse.s IL_0086                                                                ->+2
     *  IL_007a: ldloc.1
     *  IL_007b: ldc.r4 0.8
     *  IL_0080: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Multiply(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32)
     *  IL_0085: stloc.1                                                                          ->+6
     */
    [HarmonyPatch(typeof(Graphic), "Print")]
    public static class Patch_Graphic_Print
    {
        public static bool Prepare()
        {
            return Settings.useBoringOldStackingGraphic;
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<Label> startLabels;
            var codes = instructions.ToList();
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldarg_2 &&
                    codes[i + 1].opcode == OpCodes.Call &&
                    (MethodInfo)codes[i + 1].operand == AccessTools.Method(typeof(GenLabel), "MultipleItemsPerCellDrawn"))
                {
                    startLabels = codes[i].labels;
                    i += 7; // skip all 7 instructions above!
                    if (codes[i].labels == null) codes[i].labels = new List<Label>();
                    codes[i].labels.AddRange(startLabels);
                }
                yield return codes[i]; // and done!
            }
        }
    }
}
