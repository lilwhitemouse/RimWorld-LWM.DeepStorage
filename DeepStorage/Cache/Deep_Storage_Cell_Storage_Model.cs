using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace LWM.DeepStorage
{
    public class Deep_Storage_Cell_Storage_Model : ICollection<Thing>
    {
        public Dictionary<ThingDef, Dictionary<Thing, float>> ThingCache { get; private set; } = new Dictionary<ThingDef, Dictionary<Thing, float>>();

        public IntVec3 Cell { get; private set; }

        /// <summary>
        /// The value in the KeyValuePair represents the current <see cref="Thing.stackCount"/> for <see cref="ThingDef"/>.
        /// </summary>
        public Dictionary<Thing, Thing> NonFullThings { get; } = new Dictionary<Thing, Thing>(StackableThing_Comparer.Instance);

        public float TotalWeight { get; private set; }

        public int Count { get; private set; }

        public bool IsReadOnly => false;

        public Deep_Storage_Cell_Storage_Model(IntVec3 cell)
        {
            Cell = cell;
        }

        public void Add(Thing item)
        {
            Add(item, out _);
        }

        // Should not call DeSpawn() on item in this method or in any subsequent method call,
        // because this method is a callback from SpawnSetup().
        public void Add(Thing item, out float itemWeight)
        {
            float unitWeight = GetUnitWeight(item);
            itemWeight = unitWeight * item.stackCount;
            TotalWeight += itemWeight;
            Count++;

            AddToThingCache(item, unitWeight);
            AddToNonFull(item);
        }

        public void Clear()
        {
            ThingCache.Clear();
            NonFullThings.Clear();
            TotalWeight = 0;
        }

        public bool Contains(Thing item)
        {
            if (item is null)
                return false;

            return ThingCache[item.def].ContainsKey(item);
        }

        public void CopyTo(Thing[] array, int arrayIndex)
        {
            ThingCache.Values
                .SelectMany(pair => pair.Keys)
                .ToList()
                .CopyTo(array, arrayIndex);
        }

        public IEnumerator<Thing> GetEnumerator()
        {
            return ThingCache.Values
                .SelectMany(pair => pair.Keys)
                .GetEnumerator();
        }

        public bool Remove(Thing item, out float itemWeight)
        {
            itemWeight = 0;

            if (item is null)
                return false;

            if (!ThingCache.TryGetValue(item.def, out Dictionary<Thing, float> things))
                return false;

            if (!things.Remove(item))
                return false;

            itemWeight = item.GetStatValue(StatDefOf.Mass) * item.stackCount;
            TotalWeight -= itemWeight;
            Count--;

            RemoveFromNonFull(item);
            return true;
        }

        public bool Remove(Thing item)
        {
            return Remove(item, out _);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // It is only invoked when ListerMergeables_Notify_ThingStackChanged() is called,
        // therefore, stackCount of item should be sanitized.
        public void Update(Thing item, out float deltaWeight)
        {
            Dictionary<Thing, float> things = ThingCache[item.def];
            float newWeight = item.GetStatValue(StatDefOf.Mass) * item.stackCount;
            float oldWeight = things[item];
            deltaWeight = newWeight - oldWeight;

            TotalWeight += deltaWeight;
            things[item] = newWeight;

            if (item.stackCount == item.def.stackLimit)
            {
                NonFullThings.Remove(item);
            }
            else
            {
                NonFullThings[item] = item;
            }
        }

        public int SpareSpaceOnNonFull(Thing thing)
        {
            if (NonFullThings.TryGetValue(thing, out Thing value))
            {
                return thing.def.stackLimit - value.stackCount;
            }

            return 0;
        }

        public bool StackableOnNonFull(Thing thing)
        {
            return SpareSpaceOnNonFull(thing) >= thing.stackCount;
        }

        private void RemoveFromNonFull(Thing item)
        {
            if (item.stackCount == item.def.stackLimit)
                return;

            NonFullThings.Remove(item);
        }

        /// <summary>
        /// Add <paramref name="item"/> to the NonFull cache if qualified.
        /// </summary>
        /// <param name="item"> Item to add. </param>
        private void AddToNonFull(Thing item)
        {
            // Possible states:
            // 1. Item stack count equals to stack limit.
            // 2. Item not in cache.
            // 3. Item in cache, which should not happen given TryPlaceDirect() should have handled this case with calling TryAbsorbStack().
            // NOTE: Possible erroneous states in cache due to state 3.

            ThingDef def = item.def;
            int defStackLimit = def.stackLimit;

            // State 1
            if (item.stackCount == defStackLimit)
                return;

            // State 2
            if (!NonFullThings.ContainsKey(item))
            {
                NonFullThings[item] = item;
                return;
            }

            Utils.Err(Utils.DBF.CheckCapacity, $"{item.LabelCap} is spawned even though there is NonFull {item.LabelCap} in cache");
        }

        private void AddToThingCache(Thing thing, float unitWeight)
        {
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

        private static float GetUnitWeight(Thing thing)
        {
            return thing.GetStatValue(StatDefOf.Mass);
        }
    }
}
