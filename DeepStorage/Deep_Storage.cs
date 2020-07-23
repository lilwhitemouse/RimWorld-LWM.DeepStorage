using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Linq;
//using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using UnityEngine;
using static LWM.DeepStorage.Utils.DBF; // trace utils
using System.Security.Policy;
using HarmonyLib;
using System.Runtime.CompilerServices;

namespace LWM.DeepStorage
{
    ///////////////////////////////////////////////////////////////////////
    /// Deep Storage:
    /// 
    /// So many things need to be added or patched to allow more than one object 
    ///    in Deep Storage units:
    /// 1. Deep_Storage.cs:
    ///    Adds CompProperties LWM.DeepStorage.Properties
    ///      and assosciated ThingComp CompDeepStorage
    ///    also some utility functions
    /// 2. Deep_Storage_CanCarryItemsTo.cs
    ///    Patches RimWorld.StoreUtility's NoStorageBlockersIn - no longer block
    ///    if the storage unit isn't full yet
    ///    Also, disallow pets from hauling to Deep Storage Units
    /// 3. Deep_Storage_Jobs.cs
    ///    Verse.AI.HaulAIUtility's HaulToCellStorageJob - let pawns know how many
    ///    of stackable things (e.g., wood) they can carry to deep storage
    /// 4. Deep_Storage_Pause.cs
    ///    Verse/AI/Toils_Haul.cs's Toil PlaceHauledThingInCell(...) - 
    ///    change the toil to having a bit of a wait while putting into a DSU
    /// 5. Deep_Storage_PutInto.cs
    ///    Verse.GenPlace's TryPlaceDirect - allow putting down stuff
    ///      in deep storage, even if there's already stuff there.
    ///      also, tidy up stackable stuff
    ///    Verse.GenSpawn's Spawn - allow putting 2 or more items in the same place
    /// 6. Deep_Storage_TakeFrom.cs
    ///    Verse.Ai.Toil_Haul's StartCarryThing - the delegate() function, so
    ///    Pawns picking up 50 of something from Deep Storage pick up more than
    ///    just the 7 in the top stack. (otherwise, they have reserved the spot
    ///    so can't get anything else lying there)
    /// 7. Deep_Storage_SaveLoad.cs
    ///    And then there is the loading, which requires 3 more patches to make sure
    ///    items can spawn on top of each other...and not pretty patches.  Why?  B/c
    ///    buildings spawn after items do...so checking if an item is in DeepStorage
    ///    isn't possible during game load. TODO: Bug still exists here
    /// 8. Deep_Storage_UI.cs
    ///    Because no one wants to click 9 times to finally select the building.
    /// 9. ModCompatibility.cs
    ///    Makes Deep Storage play well with:
    ///      RimWorld Search Agency (Hauling Hysteresis)
    ///////////////////////////////////////////////////////////////////////

    /***********************************************************************/

    public class Utils {
        static public bool[] showDebug ={
            true,  // "Testing" will always be true
            
            false, // No Storage Blockers In
            false, // Haul To Cell Storage Job
            false, // Try Place Direct
            false, // Spawn (edit directly?)
            false, // Tidy Stacks Of
            false, // Deep_Storage_Job
            false, // Place Hauled Thing In Cell (wait functionaliy)
            false, // ShouldRemoveFromStorage
            false, // CheckCapacity
            false, // RightClickMenu // Patch_FloatMenuMakerMap_RightClick
            true, // Settings
            false, // Cache
        };

        public enum DBF // DeBugFlag
        {
            Testing, NoStorageBlockerseIn, HaulToCellStorageJob, TryPlaceDirect, Spawn, TidyStacksOf,
            Deep_Storage_Job, PlaceHauledThingInCell, ShouldRemoveFromStorage, CheckCapacity,
            RightClickMenu, Settings, Cache
        }

        // Nifty! Won't even be compiled into assembly if not DEBUG
        [System.Diagnostics.Conditional("DEBUG")]
        public static void Warn(DBF l, string s) {
            if (showDebug[(int)l])
                Log.Warning("LWM." + l.ToString() + ": " + s);
        }
        [System.Diagnostics.Conditional("DEBUG")]
        public static void Err(DBF l, string s) {
            if (showDebug[(int)l])
                Log.Error("LWM." + l.ToString() + ": " + s);
        }
        [System.Diagnostics.Conditional("DEBUG")]
        public static void Mess(DBF l, string s) {
            if (showDebug[(int)l])
                Log.Message("LWM."+l.ToString()+": "+s);
        }

        // This gets checked a lot.  Sometimes the test is done in-place (if will 
        //   need to use the slotGroup later, for example), but when using Harmony 
        //   Transpiler, tests are easier via function call
        // Most of the bulk here is debugging stuff
        public static bool CanStoreMoreThanOneThingAt(Map map, IntVec3 loc, Thing thing) {
            if (!GetDeepStorageOnCell(loc, map, out CompDeepStorage comp))
            {
                return false;
                #pragma warning disable CS0162 // Unreachable code detected
                SlotGroup slotGroup = loc.GetSlotGroup(map);
                Log.Warning("CanStoreMoreThanOneThingAt: " + loc + "? false");
                return false;
                if (slotGroup == null) Log.Warning("  null slotGroup");
                else if (slotGroup.parent == null) Log.Warning("  null slotGroup.parent");
                else if (!(slotGroup.parent is ThingWithComps)) Log.Warning("  slotGroup.parent is not ThingWithComps");
                else Log.Warning("  no CompDeepStorage");
                Log.Warning("Just for the record, " + (Scribe.mode == LoadSaveMode.LoadingVars) +
                            (Scribe.mode == LoadSaveMode.PostLoadInit) +
                            Scribe.mode);
                List<Thing> l = map.thingGrid.ThingsListAt(loc);
                foreach (Thing t in l)
                {
                    Log.Error("Did find a " + t.ToString() + " here at " + loc.ToString());
                }
                return false;
            }
            //            Log.Warning("CanStoreMoreThanOneThingAt: " + loc.ToString() + "? true");

            if (comp is CompCachedDeepStorage compCached) {
                bool result = compCached.StorageSettings.AllowedToAccept(thing)
                              && compCached.StackableAt(thing, loc, map);
                return result;
            }

            return true;
            Log.Warning("CanStoreMoreThanOneThingAt: " + loc.ToString() + "? true!");
            List<Thing> lx = map.thingGrid.ThingsListAt(loc);
            foreach (Thing t in lx)
            {
                Log.Error("Did find a " + t.ToString() + " here at " + loc.ToString());
            }
            return true;
            #pragma warning restore CS0162 // Unreachable code detected
        }
        public static bool CanStoreMoreThanOneThingIn(SlotGroup slotGroup) {
            if (slotGroup == null || !(slotGroup?.parent is ThingWithComps) ||
                (slotGroup.parent as ThingWithComps).TryGetComp<CompDeepStorage>() == null)
            {
                return false;
            }
            return true;
        }
        // Sometimes it's very important to not have 3 stacks of Brick with
        //   5 in one stack, 72 in another, and 45 in the last.
        // This will likely get called any time Pawns put something into Deep Storage
        //   - tidying is part of the time cost of using it!
        // Note that this ignores all other stacks (e.g., Wheat, Wood, &c);
        //   if that's ever needed, will have to add it.
        public static void TidyStacksOf(Thing thing)
        {
            if (thing == null || !thing.Spawned || thing.Destroyed || thing.Map == null
                || thing.Position == IntVec3.Invalid) {// just in case
                #if DEBUG
                if (Utils.showDebug[(int)DBF.TidyStacksOf]) {
                    if (!thing.Spawned) Log.Warning("Cannot tidy stack of unspawned " + thing.ToString());
                    else if (thing.Destroyed) Log.Warning("Cannot tidy stack of destroyed " + thing.ToString());
                    else if (thing.Map == null) Log.Warning("Cannot tidy stack on null map for " + thing.ToString());
                    else if (thing.Position == IntVec3.Invalid) Log.Warning("Cannot tidy invalid position for " + thing.ToString());
                }
                #endif
                return;
            }
            // If stack limit is 1, it cannot stack
            var stackLimit = thing.def.stackLimit;
            if (stackLimit <= 1) { return; }
            // Get everything and start tidying!
            var thingStacks = thing.Map.thingGrid.ThingsAt(thing.Position)
                                   .Where(t => t.def == thing.def).ToList();
            int extra = 0; // "extra" Sheep we pick up from wherever
            for (int i = 0; i < thingStacks.Count; i++)
            {
                if (thingStacks[i] == null || thingStacks[i].Destroyed) { break; } // maybe we emptied it already
                if (thingStacks[i].stackCount == stackLimit) { continue; }
                if (thingStacks[i].stackCount > stackLimit)
                {
                    // should never happen?
                    Log.Warning("LWM.DeepStorage found " + thingStacks[i].stackCount + " of " +
                                thingStacks[i].ToString() + " when the stackLimit is " +
                                stackLimit + ". This should never happen?");
                    extra = thingStacks[i].stackCount - stackLimit;
                    thingStacks[i].stackCount = stackLimit;
                    continue;
                }
                // If we get to here, we have something like
                //   74 sheep, and the stacklimit is 75
                if (extra > 0)
                { // maybe there were some extra lying around?
                    int x = Math.Min(stackLimit - thingStacks[i].stackCount, extra);
                    thingStacks[i].stackCount += x;
                    extra -= x;
                    if (thingStacks[i].stackCount == stackLimit) { continue; }
                }
                // Now try to pick up any extra sheep from remaining stacks of sheep
                // (All stacks ab)ve here already have full stacks)
                // Start looking at the end:
                for (int j = thingStacks.Count - 1; j > i; j--)
                {
                    if (thingStacks[j] == null) { continue; } //maybe already empty
                    if (thingStacks[j].stackCount <= (stackLimit - thingStacks[i].stackCount))
                    {
                        // can absorb all
                        thingStacks[i].TryAbsorbStack(thingStacks[j], true);
                        if (thingStacks[i].stackCount >= stackLimit) { break; }
                    }
                    else
                    {
                        // more than enough
                        int x = stackLimit - thingStacks[i].stackCount;
                        thingStacks[i].stackCount = stackLimit;
                        thingStacks[j].stackCount -= x;
                        break;
                    }
                } // continue on big loop if we filled it...
                if (thingStacks[i].stackCount < stackLimit)
                {
                    break; // we can't clean up any more...continue looking for unfull stacks (probably this one)
                }
            } // end loop thru ThingsAt the location
            while (extra > 0)
            {
                //weird?  Shouldn't happen?
                int x = Math.Min(stackLimit, extra);
                extra -= x;
                Thing t = GenSpawn.Spawn(thing.def, thing.Position, thing.Map);
                t.stackCount = x;
            }
        } // end TidyStacksOf(thing)

        public static HashSet<Thing> TopThingInDeepStorage = new HashSet<Thing>(); // for display

        public static bool HasDeepStorageComp(SlotGroup slotGroup, out CompDeepStorage compDeepStorage) {
            if (slotGroup?.parent is ThingWithComps thingWithComps
                && (compDeepStorage = thingWithComps.TryGetComp<CompDeepStorage>()) != null)
            {
                return true;
            }

            compDeepStorage = null;
            return false;
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetDeepStorageOnCell(IntVec3 cell, Map map, out CompDeepStorage compDeepStorage) {
            if (HasDeepStorageComp(map?.haulDestinationManager?.SlotGroupAt(cell), out compDeepStorage))
            {
                return true;
            }

            compDeepStorage = null;
            return false;
        }

        public static bool GetCacheDeepStorageOnCell(IntVec3 cell, Map map, out CompCachedDeepStorage compCached) {
            if (GetDeepStorageOnCell(cell, map, out CompDeepStorage comp)) {
                if (comp is CompCachedDeepStorage temp) {
                    compCached = temp;
                    return true;
                }
            }

            compCached = null;
            return false;
        }
     } // End Utils class
} // close LWM.DeepStorage namespace.  Thank you for reading!  =^.^= 