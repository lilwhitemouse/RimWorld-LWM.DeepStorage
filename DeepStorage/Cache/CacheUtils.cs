using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace LWM.DeepStorage
{
    public static class CacheUtils
    {
        public static bool IsCachedDeepStorage(this SlotGroup slotGroup, out CompCachedDeepStorage comp)
        {
            if (slotGroup?.parent is Building_Storage building)
            {
                comp = building.TryGetComp<CompCachedDeepStorage>();
                return comp != null;
            }

            comp = null;
            return false;
        }

        [Conditional("DEBUG")]
        public static void PrintStates(this Deep_Storage_Cell_Storage_Model cellStorage)
        {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine($"Storage at {cellStorage.Cell} has:");
                stringBuilder.AppendLine($"Stack: {cellStorage.Count}");
                stringBuilder.AppendLine($"TotalWeight: {cellStorage.TotalWeight}");
                stringBuilder.AppendLine($"NonFullThings:");
                foreach (KeyValuePair<Thing, Thing> nonFullThing in cellStorage.NonFullThings) {
                    stringBuilder.AppendLine($"{nonFullThing.Value}: {nonFullThing.Value.stackCount}");
                }

                Log.Warning($"{stringBuilder}");
            }
    }
}
