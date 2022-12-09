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
     * When does it need to Diry()?
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
        public DSMapComponent(Map map) : base(map)
        {
            cache = new Dictionary<IntVec3, CellCache>();
        }

        public void DirtyCache(IntVec3 cell)
        {
            Utils.Mess(Utils.DBF.Cache, "Cache dirtied: " + cell);
            cache.Remove(cell);
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
                cache[cell] = new CellCache(map, cell, cds);
            }
            return cache[cell].CanStoreAny(item, cds);
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
                cache[cell] = new CellCache(map, cell, cds);
            }
            return cache[cell].CanStoreThisMany(item, cds);
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
                float newMassOneItem;
                if (cds.limitingTotalFactorForCell > 0 || cds.limitingFactorForItem > 0)
                {
                    newMassOneItem = t.GetStatValue(cds.stat);
                    if (cds.limitingFactorForItem > 0 && newMassOneItem > cds.limitingFactorForItem)
                        return false;
                    if (cds.limitingTotalFactorForCell > 0 && (massStored + newMassOneItem > cds.limitingTotalFactorForCell))
                        return false;
                }
                if (emptyStacks > 0) return true;
                foreach (Thing oldT in notFull) if (t.CanStackWith(oldT)) return true;
                return false;
            }
            public int CanStoreThisMany(Thing t, CompDeepStorage cds)
            {
                float newMassOneItem;
                int maxNumberFromMass = int.MaxValue;
                if (cds.limitingFactorForItem > 0f || cds.limitingTotalFactorForCell > 0f)
                {
                    newMassOneItem = t.GetStatValue(cds.stat);
                    if (cds.limitingFactorForItem > 0f && (newMassOneItem > cds.limitingFactorForItem))
                        return 0;
                    if (cds.limitingTotalFactorForCell > 0f)
                    {
                        float availableMass = cds.limitingTotalFactorForCell - massStored;
                        if (availableMass < newMassOneItem) return 0;
                        maxNumberFromMass = (int)Math.Floor(availableMass / newMassOneItem);
                        Log.Message("Meh: " + t + ". availableMas: " + availableMass + ". newMassOneItem: " + newMassOneItem +
                                    "avail/newMass: " + ((int)(availableMass / newMassOneItem)) + ". Floor: " + maxNumberFromMass);
                    }
                }
                //TODO: fuck, minNumberStacks........
                int count = emptyStacks * t.def.stackLimit;
                if (count < 0) count = 0;
                foreach (var otherT in notFull)
                {
                    if (t.CanStackWith(otherT))
                    {
                        count += (otherT.def.stackLimit - otherT.stackCount);
                    }
                }
                Utils.Mess(Utils.DBF.Cache, "Cache Can Store: mass limit: " + maxNumberFromMass + ", count limit: " + count);
                return Math.Min(maxNumberFromMass, count);
            }
        }
    }
}
