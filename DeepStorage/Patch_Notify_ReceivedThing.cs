using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LWM.DeepStorage
{
    [HarmonyPatch(typeof(Building_Storage), nameof(Building_Storage.Notify_ReceivedThing))]
    public class Patch_Notify_ReceivedThing
    {
        public static void Postfix(Building_Storage __instance, Thing newItem)
        {
            if (__instance.TryGetComp<CompDeepStorage>() is CompDeepStorage comp)
            {
                comp.StorageBuilding.Add(newItem);
            }
        }
    }
}
