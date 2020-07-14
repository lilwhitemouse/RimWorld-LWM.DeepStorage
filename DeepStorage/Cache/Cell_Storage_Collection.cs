using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

using CellStorage = LWM.DeepStorage.Deep_Storage_Cell_Storage_Model;

namespace LWM.DeepStorage
{
    public class Cell_Storage_Collection : ICollection<Thing>
    {
        private HashSet<Thing> _thingsInStore = new HashSet<Thing>();

        private Dictionary<IntVec3, CellStorage> _cacheCell = new Dictionary<IntVec3, CellStorage>();

        private StorageSettings _settings;

        private CompCachedDeepStorage _comp;

        private Map _map;

        public Cell_Storage_Collection(Building_Storage storage, CompCachedDeepStorage comp)
        {
            if (storage.Spawned)
            {
                _settings = storage.settings;
                this.Init(storage.AllSlotCellsList(), storage.Map);
            }

            _comp = comp;
            _map = storage.Map;
        }

        public float TotalWeight { get; private set; }

        public int Count => _thingsInStore.Count;

        public bool IsReadOnly => false;

        public bool TryGetCellStorage(IntVec3 position, out CellStorage model)
        {
            return _cacheCell.TryGetValue(position, out model);
        }

        /// <summary>
        /// Add <paramref name="item"/> to cache.
        /// </summary>
        /// <param name="item"> Thing to add. </param>
        /// <remarks>
        ///     It is called in <see cref="Patch_Notify_ReceivedThing"/>. <see cref="Thing.stackCount"/> is sanitized in <see cref="GenSpawn.Spawn"/>.
        /// The number should not go above <see cref="ThingDef.stackLimit"/>.
        /// </remarks>
        public void Add(Thing item)
        {
            if (!_comp.StackableAt(item, item.Position, item.MapHeld))
                return; 

            if (!_cacheCell.TryGetValue(item.Position, out CellStorage model))
                return;

            model.Add(item, out float itemWeight);
            _thingsInStore.Add(item);
            TotalWeight += itemWeight;
        }

        public void Clear()
        {
            _thingsInStore.Clear();
            _cacheCell.Clear();
            TotalWeight = 0;
        }

        public bool Contains(Thing item)
        {
            return _thingsInStore.Contains(item);
        }

        public void CopyTo(Thing[] array, int arrayIndex)
        {
            _thingsInStore.CopyTo(array, arrayIndex);
        }

        public bool Remove(Thing item)
        {
            if (item.Map != _map)
                return false;

            float itemWeight = 0;
            if (_thingsInStore.Remove(item)
                & (_cacheCell.TryGetValue(item.Position, out CellStorage model)
                   && model.Remove(item, out itemWeight)))
            {
                TotalWeight -= itemWeight;
                return true;
            }

            return false;
        }

        public IEnumerator<Thing> GetEnumerator()
        {
            return _thingsInStore.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _thingsInStore.GetEnumerator();
        }

        /// <summary>
        /// It is invoked in the postfix of <see cref="ListerMergeables.Notify_ThingStackChanged"/>
        /// iff <paramref name="thing"/> is spawned.
        /// </summary>
        /// <param name="thing"> Thing to update. </param>
        /// <remarks>
        ///     As of RW1.1, the only call site that meets with the criteria to execute this method is TryPlaceDirect(),
        /// which we have check capacity to limit the number of items that can be merged. Thus, the overweight possibilities
        /// of a storage should be nil.
        /// </remarks>
        public void Update(Thing thing)
        {
            if (thing.Map != _map)
                return;

            if (_cacheCell.TryGetValue(thing.Position, out CellStorage model))
            {
                model.Update(thing, out float deltaWeight);
                TotalWeight += deltaWeight;
            }
        }

        public bool StackableOnNonFull(Thing thing)
        {
            return _cacheCell.Values.Any(cellStorage => cellStorage.StackableOnNonFull(thing));
        }

        public int SpareSpaceOnNonFull(Thing thing)
        {
            return _cacheCell.Values.Sum(c => c.SpareSpaceOnNonFull(thing));
        }

        /// <summary>
        /// Absorb <paramref name="thing"/> only with Non-full stack in this storage.
        /// </summary>
        /// <param name="thing"> Thing to absorb by cell storage. </param>
        /// <param name="placedAction"> Callback for when <paramref name="thing"/> is fully absorbed or Spawned. </param>
        /// <param name="resultingThing"> The thing that, partly of fully, consists of <paramref name="thing"/>.
        /// Only when return value is <see langword="true"/> will the <paramref name="resultingThing"/> be non-null. </param>
        /// <returns> Returns <see langword="true"/> only if there is no stack left to place in <paramref name="thing"/>. </returns>
        public bool AbsorbWithNonFull(Thing thing, Action<Thing, int> placedAction, ref Thing resultingThing)
        {
            foreach (CellStorage cellStorage in _cacheCell.Values)
            {
                if (cellStorage.NonFullThings.TryGetValue(thing, out Thing nonFullThing))
                {
                    // TryAbsorbStack() could trigger Update or Remove action in cache.
                    // As long as it doesn't recursively call TryPlaceDirect(), it is less error prone.
                    bool result = nonFullThing.TryAbsorbStack(thing, true);
                    if (result)
                    {
                        resultingThing = nonFullThing;
                        placedAction?.Invoke(resultingThing, resultingThing.stackCount);
                        return true;
                    }
                }
            }

            return false;
        }

        private void Init(List<IntVec3> cells, Map map)
        {
            foreach (IntVec3 cell in cells)
            {
                _cacheCell[cell] = new CellStorage(cell);
                foreach (Thing thing in cell.GetThingList(map))
                {
                    // There is a chance that when an item with size larger than one is used as input,
                    // and the item is not supposed to be stored to this building, a corpse clips a meat hook, e.g..
                    // Thus, ThingListAt() is not the appropriate method to use here.
                    if (thing.Position != cell)
                        continue;

                    if (thing.def.EverStorable(false) && _settings.AllowedToAccept(thing))
                    {
                        this.Add(thing);
                    }
                }
            }
        }
    }
}
