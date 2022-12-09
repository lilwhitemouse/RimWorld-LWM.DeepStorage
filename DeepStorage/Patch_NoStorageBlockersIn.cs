using System;  // for delegate(), Type, Func<>() stuff
using System.Collections.Generic;
using System.Reflection.Emit;
using RimWorld;
using Verse;
using HarmonyLib;
using static LWM.DeepStorage.Utils.DBF; // debug trace

namespace LWM.DeepStorage
{
    [HarmonyPatch(typeof(RimWorld.StoreUtility), "NoStorageBlockersIn")]
    public static class Patch_NoStorageBlockersIn
    {
        public static bool Prefix(IntVec3 c, Map map, Thing thing, ref bool __result)
        {
            Utils.Err(NoStorageBlockerseIn, "Looking for blockers for " + thing + " at " + c);
            // Check if storage location is in a deep-storage uber-storage building:
            CompDeepStorage cds = map?.edificeGrid[c]?.GetComp<CompDeepStorage>();
            if (cds == null) return true; // normal NoStorageBlockersIn() will handle it
            //  (We return false from this Patch function to skip original method)
            __result = cds.StackableAt(thing, c, map);
            Utils.Warn(NoStorageBlockerseIn, "Final result for " + thing + " at " + c + ": " + __result);
            return false;
        }
    }
}
