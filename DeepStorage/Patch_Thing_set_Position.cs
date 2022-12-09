using System;
using HarmonyLib;
using RimWorld;
using Verse;
namespace LWM.DeepStorage
{
    [HarmonyPatch(typeof(Verse.Thing), "set_Position")]
    public static class Patch_Thing_set_Position
    {
        static void Prefix(Thing __instance)
        {
            if (__instance.Spawned && __instance.def.category == ThingCategory.Item)
                __instance.Map.GetComponent<DSMapComponent>().DirtyCache(__instance.Position);
        }
        static void Postfix(Thing __instance)
        {
            if (__instance.Spawned && __instance.def.category == ThingCategory.Item)
                __instance.Map.GetComponent<DSMapComponent>().DirtyCache(__instance.Position);
        }
    }
}
