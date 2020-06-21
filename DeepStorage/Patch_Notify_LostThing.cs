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
    [HarmonyPatch(typeof(Building_Storage), nameof(Building_Storage.Notify_LostThing))]
    public class Patch_Notify_LostThing
    {
        public static void Postfix(Building_Storage __instance, Thing newItem)
        {
            Log.ErrorOnce("LWM Patched", Rand.Int);
            if (__instance.TryGetComp<CompDeepStorage>() is CompDeepStorage comp)
            {
                comp.StorageBuilding.Remove(newItem);
            }
        }
    }
}
