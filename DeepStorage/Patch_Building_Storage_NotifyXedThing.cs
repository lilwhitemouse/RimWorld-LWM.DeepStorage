using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Linq;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using UnityEngine;
using static LWM.DeepStorage.Utils.DBF; // trace utils
namespace LWM.DeepStorage
{
    [HarmonyPatch(typeof(Building_Storage), "Notify_ReceivedThing")]
    public static class Patch_Building_Storage_Notify_ReceivedThing
    {
        static void Postfix(Thing newItem, Building_Storage __instance)
        {
            __instance.Map.GetComponent<DSMapComponent>().DirtyCache(newItem.Position);
        }
    }

    [HarmonyPatch(typeof(Building_Storage), "Notify_LostThing")]
    public static class Patch_Building_Storage_Notify_LostThing
    {
        static void Postfix(Thing newItem, Building_Storage __instance)
        {
            // Irritatingly, Notify_LostThing doesn't tell us which 
            //   cell the thing was previously stored in...
            // If some mod does something really weird with position,
            //   but is a good pupper and calls Notify_LostThing...
            // I hate to clear the whole cache, but:
            var comp = __instance.Map.GetComponent<DSMapComponent>();
            foreach (var c in __instance.AllSlotCells())
                comp.DirtyCache(c);
        }
    }
}
