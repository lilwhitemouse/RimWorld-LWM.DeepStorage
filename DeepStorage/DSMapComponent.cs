using System;
using System.Collections;
using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Linq;
using UnityEngine;
using static LWM.DeepStorage.Utils.DBF; // trace utils
namespace LWM.DeepStorage
{
    /* Cache:
     * When does it need to Dirty()?
     * Building_Storage spawns    Done....in Display   Done...sortof
     * Building_Storage despawns        file. TODO     Piggybacking off Display
     * Item spawns       - calls Notify_ReceivedThing  Done
     * Item despawns     - calls Notify_ThingLost      Done
     * Item moves        - set_Position                Done
     * Item stackcount changes (because stack split    Done
     *      off from it) -
     */    
    public class DSMapComponent : MapComponent
    {
        private Dictionary<IntVec3, CellCache> cache;
        private Dictionary<ThingWithComps, CompDeepStorage> settingsForBlueprintsAndFrames;
        public DSMapComponent(Map map) : base(map)
        {
            cache = new Dictionary<IntVec3, CellCache>();
            settingsForBlueprintsAndFrames = new Dictionary<ThingWithComps, CompDeepStorage>();
        }
        /************************ settings for blueprints and frames *************************/
        /* RW version 1.4 introduced several new features for storage, including:
         *   Storage settings that can be set while a blueprint       
         *   Storage groups
         *   Storage groups that can be set while a blueprint
         * We desire to allow the same for comp settings for specific units/groups
         * 
         * RW handles storage settings by giving the Group storage settings and then calling
         *    on the group.
         * 
         * I think it will work better for us to         
         */

        /************************************  Cache  ***************************************/

        public void DirtyCache(IntVec3 cell)
        {
            Utils.Mess(Utils.DBF.Cache, "Cache dirtied: " + cell + (cache.ContainsKey(cell)?"":" (none)"));
            cache.Remove(cell);
        }
        public void DirtyEntireCache()
        {
            Utils.Mess(Utils.DBF.Cache, "Entire cache cleared!");
            cache.Clear();
        }
        [Multiplayer.API.SyncMethod]
        public void UpdateCache(IntVec3 cell, CompDeepStorage cds)
        {
            cache[cell] = new CellCache(map, cell, cds);
        }

        public bool CanStoreItemAt(Thing item, IntVec3 cell)
        {
            CompDeepStorage cds = map.edificeGrid[cell]?.GetComp<CompDeepStorage>();
            if (cds == null)
            {
                Log.Warning("LWM.DeepStorage: Tried to check for capacity at " + cell + " for " + item +
             " but there is no CompDeepStorage??");
                return false;
            }
            return this.CanStoreItemAt(cds, item, cell);
        }
        public bool CanStoreItemAt(CompDeepStorage cds, Thing item, IntVec3 cell)
        {
            if (!cache.ContainsKey(cell))
            {
                UpdateCache(cell, cds);
            }
            // Still won't contain cache data if MultiPlayer is active ><
            if (cache.ContainsKey(cell)) return cache[cell].CanStoreAny(item, cds);
            return cds.CapacityToStoreThingAtDirect(item, map, cell) > 0; //TODO
        }

        public int CapacityToStoreItemAt(Thing item, IntVec3 cell)
        {
            CompDeepStorage cds = map.edificeGrid[cell]?.GetComp<CompDeepStorage>();
            if (cds == null)
            {
                Log.Warning("LWM.DeepStorage: Tried to check the capacity at " + cell + " for " + item +
             " but there is no CompDeepStorage??");
                return 0;
            }
            return this.CapacityToStoreItemAt(cds, item, cell);
        }
        public int CapacityToStoreItemAt(CompDeepStorage cds, Thing item, IntVec3 cell)
        {
            Utils.Warn(Utils.DBF.Cache, "Capacity request at " + cell + " for " + item);
            if (!cache.ContainsKey(cell))
            {
                UpdateCache(cell, cds);
            }
            // Still won't contain cache data if MultiPlayer is active ><
            if (cache.ContainsKey(cell)) return cache[cell].CanStoreThisMany(item, cds);
            return cds.CapacityToStoreThingAtDirect(item, map, cell);
        }

        class CellCache
        {
            private float massStored = 0f;
            private int emptyStacks = 0;
            private List<Thing> notFull = new List<Thing>();
            public CellCache(Map map, IntVec3 cell, CompDeepStorage cds) : base()
            {
                this.Update(map, cell, cds);
            }
            public void Update(Map map, IntVec3 cell)
            {
                var cds = map.edificeGrid[cell]?.TryGetComp<CompDeepStorage>();
                if (cds == null) return;
                Update(map, cell, cds);
            }
            public void Update(Map map, IntVec3 cell, CompDeepStorage cds)
            {
                massStored = 0;
                emptyStacks = cds.MaxNumberStacks;
                notFull.Clear();
                foreach (Thing t in map.thingGrid.ThingsAt(cell).Where(t=>t.def.EverStorable(false))) {
                    massStored += t.GetStatValue(cds.stat) * t.stackCount;
                    if (t.stackCount < t.def.stackLimit) notFull.Add(t);
                    emptyStacks--;
                    // A cheap way to make listerHaulables recheck item:
                    // Better would be CheckAdd(), which is private and I don't have that here
                    if (massStored > cds.limitingTotalFactorForCell || emptyStacks < 0) 
                             map.listerHaulables.Notify_Spawned(t);
                }
                Utils.Mess(Utils.DBF.Cache,"Cache created for " + cell + ": mass: " 
                           + massStored + " emptyStacks: " + emptyStacks);
            }
            public bool CanStoreAny(Thing t, CompDeepStorage cds)
            {
                float newMassOneItem = 0f;
                if (cds.limitingTotalFactorForCell > 0 || cds.limitingFactorForItem > 0)
                {
                    newMassOneItem = t.GetStatValue(cds.stat);
                    if (cds.limitingFactorForItem > 0 && newMassOneItem > cds.limitingFactorForItem)
                    {
                        Utils.Mess(Utils.DBF.Cache, "Cache storage request for " + t + ": too heavy at " + newMassOneItem);
                        return false;
                    }
                    if (cds.limitingTotalFactorForCell > 0 && (massStored + newMassOneItem > cds.limitingTotalFactorForCell))
                    {
                        // Over mass limit for this cell
                        // BUT....
                        // If there's a minimum storage amount, we have to respect that:
                        // TODO: Maybe cache a number of forced free stacks for a cell?
                        if (cds.MinNumberStacks > 1 && cds.MaxNumberStacks-emptyStacks < cds.MinNumberStacks)
                        {
                            Utils.Mess(Utils.DBF.Cache, "Cache storage request for " + t +
                                ": over total mass limit but MinNumberStacks not met yet, so passes!");
                            return true;
                        }
                        Utils.Mess(Utils.DBF.Cache, "Cache storage request for " + t + ": over total mass limit");
                        return false;
//TODO: oops, do this
                    }
                }
                if (emptyStacks > 0) return true;
                foreach (Thing oldT in notFull) if (t.CanStackWith(oldT)) return true;
                return false;
            }
            public int CanStoreThisMany(Thing t, CompDeepStorage cds)
            {
                int scrapsLeftOverFromNonFullStacks = 0;
                float newMassOneItem = 0f; // Honestly, I'm offended that I can't leave this unassigned and compile
                if (cds.limitingFactorForItem > 0f || cds.limitingTotalFactorForCell > 0f)
                {
                    newMassOneItem = t.GetStatValue(cds.stat);
                    if (cds.limitingFactorForItem > 0f && newMassOneItem > cds.limitingFactorForItem)
                    {
                        Utils.Mess(Utils.DBF.Cache, "Cache capacity request for " + t + ": too heavy at " 
                                   + newMassOneItem);
                        return 0;
                    }
                }
                // Scraps from unfilled stacaks:
                foreach (var otherT in notFull)
                {
                    if (t.CanStackWith(otherT))
                    {
                        scrapsLeftOverFromNonFullStacks += (otherT.def.stackLimit - otherT.stackCount);
                    }
                }
                if (cds.limitingTotalFactorForCell > 0f)
                {
                    float availableMass = cds.limitingTotalFactorForCell - massStored;
                    int maxNumberFromMass = ((int)(availableMass / newMassOneItem));
                    if (cds.MinNumberStacks > 1) {
                        int curNumberStacks = cds.MaxNumberStacks - this.emptyStacks;
                        if (curNumberStacks <= cds.MinNumberStacks)
                        {
                            int minNumber = ((cds.MinNumberStacks - curNumberStacks) * t.def.stackLimit)
                                 + scrapsLeftOverFromNonFullStacks;
                            if (minNumber >= maxNumberFromMass)
                            {
                                Utils.Mess(Utils.DBF.Cache, "Cache: minimum number " + minNumber +
                                       " returned (>=" + maxNumberFromMass + "by mass)");
                                return minNumber;
                            }
                        }
                    }
                    Utils.Mess(Utils.DBF.Cache, "Cache: maxNumber from mass: " + maxNumberFromMass + " (mass of " +
                               t + "=" + newMassOneItem + ") vs free " + (emptyStacks * t.def.stackLimit + scrapsLeftOverFromNonFullStacks));
                    return Math.Min(maxNumberFromMass, (emptyStacks * t.def.stackLimit + scrapsLeftOverFromNonFullStacks));
                } // end cds.limitingTotalFactorForCell > 0f
                Utils.Mess(Utils.DBF.Cache, "Cache: returning " + (emptyStacks * t.def.stackLimit + scrapsLeftOverFromNonFullStacks));
                return emptyStacks * t.def.stackLimit + scrapsLeftOverFromNonFullStacks;
            }
            public string Debug(CompDeepStorage cds)
            {
                return "Cache:\nStacks: " + (cds.MaxNumberStacks - emptyStacks) + "/" + cds.MaxNumberStacks + "\nTotal mass: "
                       + massStored + "/" + cds.limitingTotalFactorForCell;
            }
        } // end CellCache class
        public string Debug(IntVec3 cell)
        {
            var cds = map?.edificeGrid[cell]?.GetComp<CompDeepStorage>();
            if (cds == null) return "";
            if (!cache.ContainsKey(cell)) return "No Cache for this cell";
            return cache[cell].Debug(cds);
        }
    }
}
