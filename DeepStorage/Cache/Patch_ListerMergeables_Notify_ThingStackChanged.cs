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
    /// Handles the split off and stack merge of spawned things on map.
    /// </summary>
    [HarmonyPatch(typeof(ListerMergeables), nameof(ListerMergeables.Notify_ThingStackChanged))]
    public class Patch_ListerMergeables_Notify_ThingStackChanged
    {
        public static void Postfix(Thing t)
        {
            if (t.GetSlotGroup() is SlotGroup slotGroup && slotGroup.IsCachedDeepStorage(out CompCachedDeepStorage comp))
            {
                    comp.CellStorages.Update(t);
            }
        }
    }
}
