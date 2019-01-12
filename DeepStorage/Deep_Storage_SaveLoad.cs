using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;
using Harmony;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler

//SNAP - bug: check to see if there are other items in the same square (what if the building gets destroyed?)
// BUG: loading saved game where deep storage unit has moved/been destroyed... ... ...TODO
//   need to find a way to just put the darn things there regardless of whether or not they have a DSU there...

namespace LWM.DeepStorage
{
    //***********************************************************//
    //                    Saving and Loading
    //
    // Saving:
    //   Problem: stone chunks and slag are saved "compressed," 'cause
    //   they are all basically the same.  But...if there are more than
    //   one in a DeepStorage unit, then they shouldn't be compressed.
    // So I need to fix that...TODO:
    //
    // Loading:
    //   Problem: when loading, the game will try to load all the things
    //    in the Deep Storage unit and then will realize it cannot place
    //    them because there is already something there.  So things end
    //    up exploded all over the place.  Not pretty.
    //   Why?  Because items get loaded AFTER buildings.  Well, crap.
    //    Spawn() can't tell the items are in Deep Storage, because the
    //    storage building hasn't spawned yet.
    //   So, we take advantage of ("hack") a version compatibility 
    //    feature and spawn items in Deep Storage units after buildings.
    //   I hope this doesn't break anything.
    //
    //**********************************************************//
    [HarmonyPatch(typeof(CompressibilityDeciderUtility), "IsSaveCompressible")]
    class Patch_IsSaveCompressible {
        static void Postfix(ref bool __result, Thing t)
        {
            if (__result == false) return;
            SlotGroup slotGroup = t.Position.GetSlotGroup(t.Map);
            if (slotGroup == null || !(slotGroup?.parent is ThingWithComps) ||
                (slotGroup.parent as ThingWithComps).TryGetComp<CompDeepStorage>() == null)
            {
                return;
            }
            // Hey, we're in deep storage!
            __result = false;
            return;
        }
    }


    //bug working set:
    //  Possible idea:
    //  patch TryFindPlaceSpotNear...actually, that's a really good idea?

    // Or...patch CheckSpawnBackCompatibleThingAfterLoading.  I say "patch", I mean "hack."
    //TODO XXX
    [HarmonyPatch(typeof(Verse.Map), "FinalizeLoading")]
    class Patch_Map_Load
    {
        public static List<Thing> pileOfThings = new List<Thing>();
        static void Prefix()
        {
            pileOfThings.Clear();
        }
    }

    [HarmonyPatch(typeof(Verse.BackCompatibility), "CheckSpawnBackCompatibleThingAfterLoading")]
    class Hack_BackCompatibility
    {
        static void Postfix(ref bool __result, Thing thing, Map map)
        {
            if (__result == true) { return; } // I mean...don't break things.
            // Check to see if something already exists in thing's future location
            // If so...we'll spawn it after buildings.  I did say "hack."
            Thing t = map.thingGrid.ThingAt(thing.Position, ThingCategory.Item);
            if (t != null)
            {
                //                Log.Error(thing.ToString()+" found a thing " + t.ToString() + " at "+thing.Position.ToString());
                // Reflection equivalency of:
                // BackCompatibility.tmpThingsToSpawnLater.Add(thing);
                //                ((List<Thing>)typeof(BackCompatibility).GetField("tmpThingsToSpawnLater",
                //                                                                 BindingFlags.Static | BindingFlags.NonPublic|BindingFlags.GetField).
                //                              GetValue(null)).Add(thing);

                Patch_Map_Load.pileOfThings.Add(thing);

                // Why not add it to the list?  b/c these items are added in placemode.Near or whatever it is.
                //                List<Thing> list = (List<Thing>)typeof(BackCompatibility).GetField("tmpThingsToSpawnLater",
                //                                                                      BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.GetField).GetValue(null);
                //                if (list == null) { Log.Error("Shouldn't be null"); }
                //                list.Add(thing);

                __result = true; // keeps it from being spawned...
                return;
            }
        }
    }

    [HarmonyPatch(typeof(Verse.BackCompatibility), "PostCheckSpawnBackCompatibleThingAfterLoading")]
    class Patch_PCSBCTAL
    {
        static public void Prefix(Map map)
        {
            foreach (Thing thing in Patch_Map_Load.pileOfThings)
            {
                GenSpawn.Spawn(thing, thing.Position, map, thing.Rotation, WipeMode.FullRefund, true);
            }
        }
    }


    //// save bug: ////



}
