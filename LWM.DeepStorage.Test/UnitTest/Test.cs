using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

using CellStorage = LWM.DeepStorage.Deep_Storage_Cell_Storage_Model;

namespace LWM.DeepStorage.UnitTest
{
    public static class Test
    {
        private static FieldInfo _cacheCells =
            typeof(Cell_Storage_Collection).GetField("_cacheCell ", BindingFlags.Instance | BindingFlags.NonPublic);

        private static void ReportCantFindCell(Cell_Storage_Collection collection, IntVec3 cell) {
            Dictionary<IntVec3, CellStorage> cells = (Dictionary<IntVec3, CellStorage>)_cacheCells.GetValue(collection);
            StringBuilder sb = new StringBuilder();
            foreach (IntVec3 c in cells.Keys)
                sb.Append(c);

            Log.Error($"Cannot find {cell} in storage {sb}");
        }

        public static bool TestCellStorageWeight(this Cell_Storage_Collection collection, IntVec3 cell, float expectedWeight) {
            if (collection.TryGetCellStorage(cell, out CellStorage cellStorage))
            {
                return Assert.AreEqaul(
                    cellStorage.TotalWeight
                    , expectedWeight
                    , nameof(cellStorage.TotalWeight)
                    , nameof(expectedWeight));
            }

            ReportCantFindCell(collection, cell);
            return false;
        }

        public static bool TestCellStorageStack(this Cell_Storage_Collection collection, IntVec3 cell, int expectedCount) {
            if (collection.TryGetCellStorage(cell, out CellStorage cellStorage))
            {
                return Assert.AreEqaul(
                    cellStorage.Count
                    , expectedCount
                    , nameof(cellStorage.Count)
                    , nameof(expectedCount));
            }

            ReportCantFindCell(collection, cell);
            return false;
        }

        public static bool TestCellStorageCapacity(this CompCachedDeepStorage compCached, Thing thing, Map map, IntVec3 cell, int expectedCount) {
            return Assert.AreEqaul(
                compCached.CapacityToStoreThingAt(thing, map, cell)
                , expectedCount
                , $"Capacity at {cell}"
                , $"Expected capacity");
        }

        public static bool TestSpareSpaceOnNonFull(this Cell_Storage_Collection collection, Thing thing, IntVec3 cell, int expectedCount) {
            if (collection.TryGetCellStorage(cell, out CellStorage cellStorage))
            {
                return Assert.AreEqaul(
                    cellStorage.SpareSpaceOnNonFull(thing)
                    , expectedCount
                    , $"SpareSpace On {cell}"
                    , nameof(expectedCount));
            }

            ReportCantFindCell(collection, cell);
            return false;
        }
 
       public static bool StoreXUntilStackFull(CompCachedDeepStorage comp, ThingDef def, int stackCount, Map map,
                                                  IntVec3 cell, out List<Thing> storedThings) {
            bool result = false;
            storedThings = new List<Thing>();
            stackCount = stackCount > def.stackLimit ? def.stackLimit : stackCount;

            float unitWeight = def.GetStatValueAbstract(StatDefOf.Mass);
            float maxWeight = comp.maxNumberStacks * def.stackLimit * unitWeight;

            // Used for query NonFullThings. Using temp, which is destroyed because TryAbsorbStack(), will return value not found.
            Thing temp2 = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));

            int maxCapacity = comp.CapacityToStoreThingAt(temp2, map, cell);
            int iteration = Mathf.CeilToInt((float)maxCapacity / stackCount);
            for (int i = 0; i < iteration; i++) {
                Thing temp = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
                temp.stackCount = stackCount; 

                if (GenPlace.TryPlaceThing(temp, cell, map, ThingPlaceMode.Direct))
                    storedThings.Add(temp);

                float rawWeight = unitWeight * (i + 1) * stackCount;
                float finalWeight = rawWeight > maxWeight ? maxWeight : rawWeight;

                float rawStack = (float)stackCount * (i + 1) / def.stackLimit;
                int finalStack = rawStack > comp.maxNumberStacks ? comp.maxNumberStacks : Mathf.CeilToInt(rawStack);

                int rawCapacity = maxCapacity - stackCount * (i + 1);
                int finalCapacity = rawCapacity < 0 ? 0 : rawCapacity;

                result = comp.CellStorages.TestCellStorageWeight(cell, finalWeight);
                result &= comp.CellStorages.TestCellStorageStack(cell, finalStack);
                result &= comp.CellStorages.TestSpareSpaceOnNonFull(
                    temp2, cell, (def.stackLimit - stackCount * ((i + 1) % iteration) % def.stackLimit) % def.stackLimit);
                result &= comp.TestCellStorageCapacity(temp2, map, cell, finalCapacity);
            }

            return result;
        }

        public static bool RemoveXUntilEmpty(CompCachedDeepStorage comp, List<Thing> storedThings, int stackCount, Map map, IntVec3 cell) {
            bool result = true;
            comp.CellStorages.TryGetCellStorage(cell, out Deep_Storage_Cell_Storage_Model model);
            ThingDef def = storedThings.First().def;
            Thing blackHole = ThingMaker.MakeThing(storedThings.First().def, GenStuff.DefaultStuffFor(def));

            float unitWeight = def.GetStatValueAbstract(StatDefOf.Mass);
            int totalStacks = model.Count;
            int totalStackCount = storedThings.Sum(t => t.stackCount);
            float totalWeight =  unitWeight * totalStackCount;
            int spareSpaceOnNonFull = model.SpareSpaceOnNonFull(blackHole);

            float rollingWeight = totalWeight;
            int rollingStackCount = 0;

            int remainAmount = stackCount;
            foreach (Thing thing in storedThings) {

                while (thing.stackCount > 0) {

                    if (remainAmount == thing.stackCount) {
                        blackHole.TryAbsorbStack(thing, false);

                        bool hasNonFull = model.NonFullThings.ContainsKey(thing);
                        result &= RunTests(
                            rollingWeight -= remainAmount * unitWeight
                            , totalStacks -= 1
                            , spareSpaceOnNonFull = hasNonFull ? spareSpaceOnNonFull : 0
                            , rollingStackCount += remainAmount);

                        remainAmount = 0;
                    }
                    else if (remainAmount < thing.stackCount) {
                        thing.stackCount -= remainAmount;
                        thing.Map.listerMergeables.Notify_ThingStackChanged(thing);

                        bool sameAsNonFull = false;
                        if (model.NonFullThings.TryGetValue(blackHole, out Thing nonFullThing)) {
                            sameAsNonFull = nonFullThing == thing;
                        }

                        bool absorbed = thing.stackCount <= spareSpaceOnNonFull;
                        result &= RunTests(
                            rollingWeight -= remainAmount * unitWeight
                            , totalStacks = thing.stackCount == 0 ? --totalStacks : totalStacks
                            , spareSpaceOnNonFull = sameAsNonFull
                                ? def.stackLimit - thing.stackCount
                                : absorbed
                                    ? spareSpaceOnNonFull - thing.stackCount
                                    : def.stackLimit - (thing.stackCount - spareSpaceOnNonFull)
                            , rollingStackCount += remainAmount);

                        remainAmount = 0;
                    }
                    else {
                        remainAmount -= thing.stackCount;
                        int temp = thing.stackCount;

                        blackHole.TryAbsorbStack(thing, false);

                        bool hasNonFull = model.NonFullThings.ContainsKey(thing);
                        result &= RunTests(
                            rollingWeight -= temp * unitWeight
                            , totalStacks -= 1
                            , spareSpaceOnNonFull = hasNonFull ? spareSpaceOnNonFull : 0
                            , rollingStackCount += temp);
                    }

                    if (remainAmount <= 0)
                        remainAmount = stackCount;
                }
            }

            bool RunTests(float weight, int stacks, int spareOnNonFull, int capacity) {
                bool innerResult = comp.CellStorages.TestCellStorageWeight(cell, weight);
                innerResult &= comp.CellStorages.TestCellStorageStack(cell, stacks);
                innerResult &= comp.CellStorages.TestSpareSpaceOnNonFull(blackHole, cell, spareOnNonFull);
                innerResult &= comp.TestCellStorageCapacity(blackHole, map, cell, capacity);
                return innerResult;
            }

            return result;
        }

        public static bool TestSelfCorrection(CompCachedDeepStorage compCached, IntVec3 cell, Map map, ThingDef def, int stackCount, int iteration) {
            FieldInfo _mapIndex =
                typeof(Thing).GetField("mapIndexOrState", BindingFlags.Instance | BindingFlags.NonPublic);

            bool result = true;

            if (compCached.CellStorages.TryGetCellStorage(cell, out CellStorage cellStorage)) {
                Dictionary<ThingDef, Dictionary<Thing, float>> thingCache = cellStorage.ThingCache;
                Dictionary<Thing, float> sameThings = new Dictionary<Thing, float>();
                thingCache[def] = sameThings;

                // Can't use destroyed thing(thing got absorbed) to query NonFullThings because if will always fails the test, CanStackWith().
                Thing reference = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
                for (int i = 0; i < iteration; i++) {

                    Thing newThing = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
                    newThing.stackCount = stackCount;

                    // If the stackCount is not equal to the stackLimit, we will need to sneak those things into cache,
                    // otherwise, non-full things will be absorbed when try to spawn to cache.
                    if (stackCount != def.stackLimit) {
                        newThing.Position = cell;
                        _mapIndex.SetValue(newThing, (sbyte)compCached.parent.Map.Index);
                        sameThings[newThing] = newThing.GetStatValue(StatDefOf.Mass) * stackCount;
                    }
                    else {
                        // stackCount equals to stackLimit, we can use the "normal" way to put it into cache.
                        GenPlace.TryPlaceThing(newThing, cell, map, ThingPlaceMode.Direct);
                    }
                }

                cellStorage.SelfCorrection();

                int expectedStack = Mathf.CeilToInt((float) stackCount * iteration / def.stackLimit);
                float expectedWeight = stackCount * iteration * reference.GetStatValue(StatDefOf.Mass);
                int expectedSpareSpaceOnNonFull = (def.stackLimit - (stackCount * iteration % def.stackLimit)) % def.stackLimit;

                result &= compCached.CellStorages.TestCellStorageStack(cell, expectedStack);
                result &= compCached.CellStorages.TestCellStorageWeight(cell, expectedWeight);
                result &= compCached.CellStorages.TestSpareSpaceOnNonFull(reference, cell, expectedSpareSpaceOnNonFull);

                cellStorage.Clear();
            }

            return result;
        }
    }
}
