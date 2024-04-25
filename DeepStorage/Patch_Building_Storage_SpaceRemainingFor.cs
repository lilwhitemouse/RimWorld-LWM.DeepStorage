using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Linq;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using UnityEngine;

namespace LWM.DeepStorage // Our cache is likely to be slightly faster than vanilla, especially for giant storage people
{
    [HarmonyPatch(typeof(RimWorld.Building_Storage), "SpaceRemainingFor")]
    static class Patch_Building_Storage_SpaceRemainingFor
    {
        static bool Prepare()
        {
            if (Settings.multiplayerIsActive) return false; // TODO: Don't patch this for MP until cache is MP safe
            return true;
        }
        static bool Prefix (Building_Storage __instance, ref int __result)
        {
            var cds = __instance.GetComp<CompDeepStorage>();
            if (cds == null) return true;
            __result = 0;
            var mc = __instance.Map.GetComponent<MapComponentDS>();
            foreach (var c in __instance.AllSlotCells()) __result += mc.EmptyStacksAt(cds, c);
            return false;
        }
    }
}
