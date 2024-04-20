using System;
using RimWorld;
using Verse;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using System.Collections.Generic;
using System.Linq;
using UnityEngine; // for icon for gizmo

namespace LWM.DeepStorage
{
    // MakeSolidThing does get called before Destroy, so we should be okay using Map
    [HarmonyPatch(typeof(RimWorld.Blueprint_Storage), "MakeSolidThing")]
    public static class Patch_Blueprint_Storage_MakeSolidThing
    {
        public static void Postfix(Thing __result, Blueprint_Storage __instance)
        {
            if (__instance.Map.GetComponent<MapComponentDS>().settingsForBlueprintsAndFrames.Remove(__instance,out CompDeepStorage comp))
            {
                Utils.Mess(Utils.DBF.StorageGroup, "Blueprint_Storage " + __instance + " transferring settings to " + __result);
                __instance.Map.GetComponent<MapComponentDS>().settingsForBlueprintsAndFrames[(ThingWithComps)__result] = comp;
            }
        }
    }
    [HarmonyPatch(typeof(RimWorld.Blueprint_Storage), "Destroy")]
    public static class Patch_Blueprint_Storage_Destroy
    {
        public static void Prefix(Blueprint_Storage __instance)
        {
            Utils.Mess(Utils.DBF.StorageGroup, "Blueprint_Storage " + __instance + " has been destroyed; "+
                       (__instance.Map.GetComponent<MapComponentDS>().settingsForBlueprintsAndFrames.ContainsKey(__instance) ?
                        "removing comp" : "no comp to remove"));
            __instance.Map.GetComponent<MapComponentDS>().settingsForBlueprintsAndFrames.Remove(__instance);
        }
    }
    [HarmonyPatch(typeof(RimWorld.Blueprint_Storage), "GetGizmos")]
    public static class Patch_Blueprint_storage_GetGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Blueprint_Storage __instance)
        {
            foreach (var g in __result) yield return g;
            foreach (var g in DSStorageGroupUtility.GetDSStorageGizmos())
            {
                yield return g;
            }
        }
    }
}
