using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DeepStorage
{
    /// <summary>
    /// Handles the splitoff and stack merge of spawned things on map.
    /// </summary>
    [HarmonyPatch(typeof(ListerMergeables), "Notify_ThingStackChanged")]
    public static class Patch_ListerMergeables_Notify_ThingStackChanged
    {
        public static void Postfix(Thing t)
        {
            if (t.GetSlotGroup().parent is Deep_Storage_Building deepStorage)
            {
                deepStorage.Remove(t);
                deepStorage.Add(t);
            }
        }
    }
}
