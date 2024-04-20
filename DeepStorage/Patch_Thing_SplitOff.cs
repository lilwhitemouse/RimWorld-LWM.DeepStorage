using System;
using HarmonyLib;
using RimWorld;
using Verse;
namespace LWM.DeepStorage
{
    [HarmonyPatch(typeof(Verse.Thing), "SplitOff")]
    public static class Patch_Thing_SplitOff
    {
        static void Prefix(Thing __instance)
        {
            if (__instance.Spawned && __instance.def.category == ThingCategory.Item)
                __instance.Map.GetComponent<MapComponentDS>().DirtyCache(__instance.Position);
        }
    }
}
