using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DeepStorage.Cache
{
    [HarmonyPatch(typeof(Thing), nameof(Thing.TryAbsorbStack))]
    public class Patch_Thing_TryAbsorbStack
    {
        // Originally, TryAbsorbStack() doesn't call the notify method for "other".
        // This fixes an issue when a non-full thing in cell storage tries to absorb another thing
        // in storage when Update() is called on the cell storage.
        public static void Postfix(Thing other) {
            if (other.Spawned)
                other.Map.listerMergeables.Notify_ThingStackChanged(other);
        }
    }
}
