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
    [HarmonyPatch(typeof(ListerMergeables), nameof(ListerMergeables.Notify_ThingStackChanged))]
    public class Patch_ListerMergeables_Notify_ThingStackChanged
    {
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
