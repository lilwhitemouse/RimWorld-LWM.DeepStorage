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
            if (__instance.TryGetComp<CompCachedDeepStorage>() is CompCachedDeepStorage comp)
            {
                Utils.Mess(Utils.DBF.Cache, $"Place {newItem.LabelCap} in {__instance.LabelCapNoCount}");
                comp.CellStorages.Add(newItem);
            }
        }
    }
}
