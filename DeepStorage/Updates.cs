#if false
using System;
using Harmony;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using UnityEngine; // because graphics.

/********************* Updates.cs  ************************
 * Contains code to update BigShelf to LWM_BigShelf
 * After 2020 starts, this code will be removed, as 
 * will the BigShelf def.  This way, no risk of the
 * defName conflicting with anyone else, but we can
 * switch everyone to the new defName.
 */

namespace LWM.DeepStorage
{
    [HarmonyPatch(typeof(Verse.BackCompatibility), "BackCompatibleDefName")]
    public static class Patch_LWM_DS_BackCompatibleDefName {
        public static void Postfix(Type defType, string defName, ref string __result) {
            if (defType==typeof(ThingDef)) {
                if (defName=="BigShelf") __result="LWM_BigShelf";
            }
        }
        
    }
}
#endif