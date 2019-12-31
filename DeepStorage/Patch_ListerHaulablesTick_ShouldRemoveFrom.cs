using System;
using RimWorld;
using Verse;
using Verse.AI;
using Harmony;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static LWM.DeepStorage.Utils.DBF; // trace utils

/* ListerHaulables should check if items in DSU are haulable
 * This is important in cases where a user changes what is 
 * allowed in a DSU
 */

namespace LWM.DeepStorage
{
    /********************
     * ListerHaulables' ListerHaulablesTick():
     *   ListerHaulablesTick goes thru some fancy mechanics to check every
     *   cell and catch anything that's in the wrong place.  If an item
     *   is in DS, only the first item gets checked, because the code
     *   looks like this:
     * for (int j = 0; j < thingList.Count; j++) {
     *   if (thingList[j].def.EverHaulable) {
     *     this.Check(thingList[j]);
     *     break;   // <---skips any of the rest of the EverHaulable items!
     *   }
     * }
     * So, we remove the break statement.
     * The IL code is fairly straightfowrad, with a jump out of the loop right
     *   after the Check(...) call.  So, we look for a "br" command that's
     *   right after that Check, and we don't return it.
     *
     * Note that it is safe for this patch to be applied more than once.
     */
    [HarmonyPatch(typeof(RimWorld.ListerHaulables), "ListerHaulablesTick")]
    static class Patch_ListerHaulablesTick {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            List<CodeInstruction> code=instructions.ToList();
            var check=typeof(ListerHaulables).GetMethod("Check", BindingFlags.NonPublic|BindingFlags.Instance);
            for (int i=0; i<code.Count; i++) {
                if (code[i].opcode!=OpCodes.Br ||
                    code[i-1].opcode!=OpCodes.Call ||
                    code[i-1].operand!=check) {
                    yield return code[i];
                //} else {
                //    Log.Warning("Found the 'break;' code! Skipping...");
                }
            }
            yield break;
        }
    }
}
