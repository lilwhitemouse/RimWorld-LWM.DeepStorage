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
    /// <summary>
    /// Handles the splitoff and stack merge of spawned things on map.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Patch_ListerMergeables_Notify_ThingStackChanged
    {
        static Patch_ListerMergeables_Notify_ThingStackChanged()
        {
            MethodInfo original = typeof(ListerMergeables).GetMethod(nameof(ListerMergeables.Notify_ThingStackChanged));
            MethodInfo postfix = typeof(Patch_ListerMergeables_Notify_ThingStackChanged).GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static);
            Utils.HarmonyInstance.Patch(original, postfix: new HarmonyMethod(postfix));
        }

        public static void Postfix(Thing t)
        {
            if (t.GetSlotGroup()?.parent is Building_Storage storage)
            {
                if (storage.GetComp<CompDeepStorage>() is CompDeepStorage comp)
                {
                    comp.StorageBuilding.Update(t);
                }
            }
        }
    }
}
