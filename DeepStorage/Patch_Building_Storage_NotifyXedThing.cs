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
#if DEBUGLWM
namespace LWM.DeepStorage
{/*
    [HarmonyPatch(typeof(Building_Storage), "Notify_ReceivedThing")]
    public static class Patch_Building_Storage_NotifyReceivedThing
    {
        static void Postfix(Thing newItem, Building_Storage __instance)
        {
            Log.Message("LWM.DeepStorage: " + __instance + " RECEIVED " + newItem);
        }
    }

    [HarmonyPatch(typeof(Building_Storage), "Notify_LostThing")]
    public static class Patch_Building_Storage_NotifyLostThing
    {
        static void Postfix(Thing newItem, Building_Storage __instance)
        {
            Log.Message("LWM.DeepStorage: " + __instance + " LOST " + newItem);
        }
    }*/
}
#endif
