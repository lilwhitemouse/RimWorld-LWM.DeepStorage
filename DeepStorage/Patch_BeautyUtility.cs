using System.Collections.Generic;
using RimWorld;
using Verse;
using HarmonyLib;

//using static LWM.DeepStorage.Utils.DBF; // debug trace

/// <summary>
///   A Harmony Patch so that a Building_Storage's Beauty value is counted even if
///   the building's ignoreStoredThingsBeauty is 'true'.
///   This will allow beautiful storage items to actually make things prettier....and ugly ones to make things uglier.
/// </summary>


namespace LWM.DeepStorage
{
    [HarmonyPatch(typeof(RimWorld.BeautyUtility), "CellBeauty")]
    class PatchBeautyUtilityCellBeauty {
        static bool Prefix(ref float __result, IntVec3 c, Map map, List<Thing> countedThings = null) {
            SlotGroup slotgroup=c.GetSlotGroup(map);
            if (slotgroup==null) return true;
            if (!(slotgroup.parent is Building_Storage)) return true;
            if (!slotgroup.parent.IgnoreStoredThingsBeauty) return true;
            Building_Storage storage=slotgroup.parent as Building_Storage;
            if (countedThings != null) {
                // Ignoring all the other things here because that's best:
                // What if a pretty rug were here, but also a shelf?  Suddenly,
                // no beauty from the rug, but also no counting its beauty
                // elsewhere in the room!
                if (countedThings.Contains(storage)) {
                    __result=map.terrainGrid.TerrainAt(c).GetStatValueAbstract(StatDefOf.Beauty, null);
                    return false;
                }
                countedThings.Add(storage);
            }
            __result=storage.GetStatValue(StatDefOf.Beauty, true)+
                map.terrainGrid.TerrainAt(c).GetStatValueAbstract(StatDefOf.Beauty, null);
            return false;
        }
    }
}
