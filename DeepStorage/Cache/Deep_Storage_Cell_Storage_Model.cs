using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace LWM.DeepStorage
{
    /// <summary>
    /// The basic unit of any storage. All input used by methods in this class should be sanitized by
    /// its holding class. Checks to make:
    /// 1. stackCount on thing.
    /// 2. Whether thing and the storage on the same map.
    /// 3. If a thing is accepted by the Storage setting.
    /// </summary>
    public class Deep_Storage_Cell_Storage_Model : ICollection<Thing>, IExposable
    {
        private CompCachedDeepStorage _comp;

        private readonly int _minNumberStacks;

        private readonly int _maxNumberStacks;

        private readonly float _limitintTotalFactorForCell;

        private List<Thing> _serializationList = new List<Thing>();

        /// <summary>
        /// Indicates if the Add method is re-entry because of a previous call to Add.
        /// </summary>
        private bool _addReEntry;

        public Deep_Storage_Cell_Storage_Model() {
        }

        public Deep_Storage_Cell_Storage_Model(IntVec3 cell, CompCachedDeepStorage comp) {
            this.Cell = cell;
            _comp = comp;
            _minNumberStacks = _comp.minNumberStacks;
            _maxNumberStacks = _comp.maxNumberStacks;
            _limitintTotalFactorForCell = _comp.limitingTotalFactorForCell;
        }

        public Dictionary<ThingDef, Dictionary<Thing, float>> ThingCache { get; private set; }
            = new Dictionary<ThingDef, Dictionary<Thing, float>>();

        public IntVec3 Cell { get; private set; }

        /// <summary>
        /// Note: It needs to always keep the non-full things on top of the stack,
        /// because it determines the amount of items to haul in some hauling jobs.
        /// </summary>
        public Dictionary<Thing, Thing> NonFullThings { get; } =
            new Dictionary<Thing, Thing>(StackableThing_Comparer.Instance);

        public float TotalWeight { get; private set; } = 0;

        public int Count { get; private set; } = 0;

        public bool IsReadOnly => false;

        

        public void Add(Thing item) {
            float unitWeight = GetUnitWeight(item);
            Add(item, unitWeight);
        }

        public void Add(Thing item, float unitWeight) {
            float itemWeight = unitWeight * item.stackCount;
            TotalWeight += itemWeight;

            AddToNonFull(item);

            // Could be despawn when it is processed by AddToNonFull().
            if (!item.Spawned)
                return;

            AddToThingCache(item, unitWeight);
            this.Count++;
        }

        public bool TryAdd(Thing item) {
            float unitWeight = item.GetStatValue(StatDefOf.Mass);
            if (_addReEntry || CanAccept(item, unitWeight))
            {
                Add(item, unitWeight);
                _addReEntry = false;
                return true;
            }

            return false;
        }

        public void Clear() {
            ThingCache.Clear();
            NonFullThings.Clear();
            TotalWeight = 0;
        }

        public bool Contains(Thing item) {
            if (item is null)
                return false;

            return this.ThingCache.TryGetValue(item.def, out Dictionary<Thing, float> value)
                   && value.ContainsKey(item);
        }

        public void CopyTo(Thing[] array, int arrayIndex) {
            ThingCache.Values
                .SelectMany(pair => pair.Keys)
                .ToList()
                .CopyTo(array, arrayIndex);
        }

        public IEnumerator<Thing> GetEnumerator() {
            return ThingCache.Values
                .SelectMany(pair => pair.Keys)
                .GetEnumerator();
        }

        public bool Remove(Thing item) {
            if (item is null)
                return false;

            if (!ThingCache.TryGetValue(item.def, out Dictionary<Thing, float> things))
                return false;

            if (!things.Remove(item))
                return false;

            float itemWeight = item.GetStatValue(StatDefOf.Mass) * item.stackCount;
            TotalWeight -= itemWeight;
            Count--;

            RemoveFromNonFull(item);
            return true;
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        /// <summary>
        /// Update items in storage.
        /// </summary>
        /// <param name="item"> The updated item. </param>
        /// <summary>
        ///     There could be many reason why the stackCount of an item changes in storage,
        /// e.g., decay, partially taken out for construction of projects. Therefore, it is necessary
        /// to update the NonFullThings here.
        /// </summary>
        public void Update(Thing item) {
            // Do not update things that are not already in storage.
            if (!this.ThingCache.TryGetValue(item.def, out Dictionary<Thing, float> things))
                return;

            if (!things.ContainsKey(item))
                return;

            float newWeight = item.GetStatValue(StatDefOf.Mass) * item.stackCount;
            float oldWeight = things[item];
            float deltaWeight = newWeight - oldWeight;

            TotalWeight += deltaWeight;
            things[item] = newWeight;

            if (this.NonFullThings.TryGetValue(item, out Thing nonFullThing))
            {
                if (item.stackCount == item.def.stackLimit)
                {
                    if (nonFullThing == item)
                        NonFullThings.Remove(item);
                }
                else
                {
                    // Possible states:
                    // 1. Item is the same as the one stored in NonFullThings, no action required.
                    // 2. Item is not the same as the one in NonFullThings.

                    // State 2
                    if (nonFullThing != item)
                    {
                        // TryAbsorbStack() will trigger a re-entry to this method for nonFullThing.
                        // The other branches in this method will take care of the re-entry.
                        if (!nonFullThing.TryAbsorbStack(item, true))
                        {
                            NonFullThings[item] = item;
                        }
                    }
                }
            }
            else
            {
                if (item.stackCount != item.def.stackLimit)
                    this.NonFullThings[item] = item;
            }
        }

        public int SpareSpaceOnNonFull(Thing thing) {
            if (NonFullThings.TryGetValue(thing, out Thing value))
            {
                return thing.def.stackLimit - value.stackCount;
            }

            return 0;
        }

        public bool StackableOnNonFull(Thing thing) {
            return SpareSpaceOnNonFull(thing) >= thing.stackCount;
        }

        public bool CanAccept(Thing thing, float unitWeight) {
            float thingWeight = thing.stackCount * unitWeight;
            int thingStacks = Mathf.CeilToInt((float) thing.stackCount / thing.def.stackLimit);
            int stacksStoredHere = this.Count;

            bool gTMinStack = stacksStoredHere + thingStacks > _minNumberStacks;
            bool gTMaxStack = stacksStoredHere + thingStacks > _maxNumberStacks;
            bool gTCellFactor = _limitintTotalFactorForCell > 0f &&
                                thingWeight + this.TotalWeight > _limitintTotalFactorForCell;

            if (!gTCellFactor && (!gTMinStack || !gTMaxStack))
                return true;

            return StackableOnNonFull(thing);
        }

        #region Implementation of IExposable

        public void ExposeData() {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                _serializationList = ThingCache.Values.SelectMany(d => d.Keys).ToList();
            }
            else if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                PostLoadInit(_serializationList);
            }

            Scribe_Collections.Look(ref _serializationList, false, nameof(_serializationList), LookMode.Reference);
        }

        #endregion


        private static float GetUnitWeight(Thing thing) {
            return thing.GetStatValue(StatDefOf.Mass);
        }

        private void PostLoadInit(List<Thing> things) {
            this.ThingCache = new Dictionary<ThingDef, Dictionary<Thing, float>>();
            foreach (Thing thing in things)
            {
                if (thing.stackCount != thing.def.stackLimit)
                {
                    // Trying to find the item in NonFullThings.
                    if (!this.NonFullThings.TryGetValue(thing, out Thing nonFullThing))
                    {
                        // If not present in the cache, add it.
                        this.NonFullThings[thing] = thing;
                    }
                    else
                    {
                        try
                        {
                            if (!nonFullThing.TryAbsorbStack(thing, true))
                            {
                                // If nonFullThing can't absorb thing entirely, which suggests nonFulling reaches its stackLimit,
                                // assign thing to NonFullThings cache.
                                this.NonFullThings[thing] = thing;
                            }
                            else
                            {
                                // After merging, if it reaches stackLimit, remove it from cache.
                                if (nonFullThing.stackCount == thing.def.stackLimit)
                                    this.NonFullThings.Remove(nonFullThing);
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Warning($"Naughty item {thing} refuses to play with a little white mouse. Please report this warning" +
                                        $"to LWM.\n{e}");
                        }
                    }
                }

                // thing got absorbed.
                if (thing.Destroyed)
                    continue;

                float weight;
                if (this.ThingCache.TryGetValue(thing.def, out Dictionary<Thing, float> value))
                {
                    weight = value[thing] = thing.GetStatValue(StatDefOf.Mass) * thing.stackCount;
                }
                else
                {
                    Dictionary<Thing, float> newCache = new Dictionary<Thing, float>()
                    {
                        [thing] = weight = thing.GetStatValue(StatDefOf.Mass) * thing.stackCount,
                    };

                    this.ThingCache[thing.def] = newCache;
                }


                this.TotalWeight += weight;
                this.Count++;
            }
        }

        /// <summary>
        /// Remove item from NonFull cache.
        /// </summary>
        /// <param name="item"> Item to remove. </param>
        /// <remarks> If the add and update operation runs correctly, there will always be only one non-full stack per kind. </remarks>
        private void RemoveFromNonFull(Thing item) {
            if (item.stackCount == item.def.stackLimit)
                return;

            if (NonFullThings[item] == item)
                NonFullThings.Remove(item);
        }

        /// <summary>
        /// Add <paramref name="item"/> to the NonFull cache if qualified.
        /// </summary>
        /// <param name="item"> Item to add. </param>
        private void AddToNonFull(Thing item) {
            // Possible states:
            // 1. Item stack count equals to stack limit.
            // 2. Item not in cache.
            // 3. Item in cache.

            ThingDef def = item.def;
            int defStackLimit = def.stackLimit;

            // State 1
            if (item.stackCount == defStackLimit)
                return;

            // State 2
            if (!NonFullThings.TryGetValue(item, out Thing value))
            {
                NonFullThings[item] = item;
                return;
            }

            // State 3
            // Update() method will take care of the change related to value.
            // Note: Steps are taken to clean up the spawn setup for item if TryAbsorbStack() returns true,
            // because item is DeSpawned when in the process of spawning.
            // Detail can check in the Harmony patch to GenSpawn.Spawn() and Thing.SpawnSetup().
            if (!value.TryAbsorbStack(item, true))
            {
                this.NonFullThings[item] = item;
            }
        }

        private void AddToThingCache(Thing thing, float unitWeight) {
            if (ThingCache.TryGetValue(thing.def, out Dictionary<Thing, float> things))
            {
                things[thing] = thing.stackCount * unitWeight;
            }
            else
            {
                Dictionary<Thing, float> newCache = new Dictionary<Thing, float>
                {
                    [thing] = thing.stackCount * unitWeight
                };

                ThingCache[thing.def] = newCache;
            }
        }

        private void SelfCorrection() {
            int count = 0;
            float totalWeight = 0;

            foreach (Dictionary<Thing, float> sameThings in this.ThingCache.Values)
            {
                foreach (Thing thing in sameThings.Keys)
                {
                    totalWeight += sameThings[thing] = thing.GetStatValue(StatDefOf.Mass);
                    count++;
                }
            }

            this.Count = count;
            this.TotalWeight = totalWeight;
        }
    }
}