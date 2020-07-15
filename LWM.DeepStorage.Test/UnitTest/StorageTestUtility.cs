using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        public static bool TestCellStorageWeight(this Cell_Storage_Collection collection, IntVec3 cell, float expectedWeight) {
            if (collection.TryGetCellStorage(cell, out CellStorage cellStorage))
            {
                return Assert.AreEqaul(
                    cellStorage.TotalWeight
                    , expectedWeight
                    , nameof(cellStorage.TotalWeight)
                    , nameof(expectedWeight));
            }

            Dictionary<IntVec3, CellStorage> cells = (Dictionary<IntVec3, CellStorage>)_cacheCells.GetValue(collection);
            StringBuilder sb = new StringBuilder();
            foreach (IntVec3 c in cells.Keys)
                sb.Append(c);

            Log.Error($"Cannot find {cell} in storage {sb}");
            return false;
        }

        public static bool TestCellStorageCount(this Cell_Storage_Collection collection, IntVec3 cell, int expectedCount) {
            if (collection.TryGetCellStorage(cell, out CellStorage cellStorage))
            {
                return Assert.AreEqaul(
                    cellStorage.Count
                    , expectedCount
                    , nameof(cellStorage.Count)
                    , nameof(expectedCount));
            }

            Dictionary<IntVec3, CellStorage> cells = (Dictionary<IntVec3, CellStorage>)_cacheCells.GetValue(collection);
            StringBuilder sb = new StringBuilder();
            foreach (IntVec3 c in cells.Keys)
                sb.Append(c);

            Log.Error($"Cannot find {cell} in storage {sb}");
            return false;
        }
    }
}
