using System;
using HarmonyLib;
using Verse;

namespace LWM.DeepStorage
{
#if DEBUGLWM
    [HarmonyPatch(typeof(Verse.Building), "get_MaxItemsInCell")]
    public static class Patch_Building_MaxItemsInCell
    {
        public static bool Prefix(Building __instance, ref int __result)
        {
            var cds = __instance.GetComp<CompDeepStorage>();
            __result = cds?.MaxNumberStacks ?? __instance.def.building.maxItemsInCell;
            return false;
        }
    }
#endif
}
