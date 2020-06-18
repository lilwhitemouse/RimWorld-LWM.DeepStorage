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
    [StaticConstructorOnStartup]
    public static class Patch_Notify_LostThing
    {
        static Patch_Notify_LostThing()
        {
            MethodInfo original = typeof(Building_Storage).GetMethod(nameof(Building_Storage.Notify_LostThing));
            MethodInfo postfix = typeof(Patch_Notify_LostThing).GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static);
            Utils.HarmonyInstance.Patch(original, postfix: new HarmonyMethod(postfix));
        }

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
