using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;

using CellStorage = LWM.DeepStorage.Deep_Storage_Cell_Storage_Model;

namespace LWM.DeepStorage.UnitTest
{
    public static class StorageTestUtility
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

        public static void PrintStates(this CellStorage cellStorage) {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"Storage at {cellStorage.Cell} has:");
            stringBuilder.AppendLine($"Stack: {cellStorage.Count}");
            stringBuilder.AppendLine($"TotalWeight: {cellStorage.TotalWeight}");
            stringBuilder.AppendLine($"NonFullThings:");
            foreach (KeyValuePair<Thing, Thing> nonFullThing in cellStorage.NonFullThings) {
                stringBuilder.AppendLine($"{nonFullThing.Value}: {nonFullThing.Value.stackCount}");
            }

            Log.Warning($"{stringBuilder}");
        }
    }
}
