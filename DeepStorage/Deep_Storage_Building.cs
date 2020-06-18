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
    public class Deep_Storage_Building : ICollection<Thing>
    {
        private Dictionary<Thing, IntVec3> _thingToCell = new Dictionary<Thing, IntVec3>();

        private Dictionary<IntVec3, CellStorage> _cellToCache = new Dictionary<IntVec3, CellStorage>();

        private StorageSettings _settings;

        public Deep_Storage_Building(Building_Storage storage)
        {
            if (storage.Spawned)
            {
                _settings = storage.settings;
                this.Init(storage.AllSlotCellsList(), storage.Map);
            }
        }

        public float CarriedWeight => _cellToCache.Values.Sum(model => model.TotalWeight);

        public int Count => _cellToCache.Sum(pair => pair.Value.Count);

        public bool IsReadOnly => false;

        public bool TryGetCellStorage(IntVec3 position, out CellStorage model)
        {
            return _cellToCache.TryGetValue(position, out model);
        }

        public void Add(Thing item)
        {
            // There is a chance that when an item with size larger than one is used as input,
            // and the item is not supposed to be stored to this building, a corpse clips a meathook, e.g..
            if (_cellToCache.TryGetValue(item.Position, out CellStorage model))
            {
                model.Add(item);
                _thingToCell[item] = item.Position;
            }
        }

        public void Clear()
        {
            _thingToCell.Clear();
            _cellToCache.Clear();
        }

        public bool Contains(Thing item)
        {
            return _thingToCell.Keys.Contains(item);
        }

        public void CopyTo(Thing[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(Thing item)
        {
            return _thingToCell.Remove(item) & (_cellToCache.TryGetValue(item.Position, out CellStorage model) && model.Remove(item));
        }

        public IEnumerator<Thing> GetEnumerator()
        {
            return _thingToCell.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _thingToCell.Keys.GetEnumerator();
        }

        public void Update(Thing thing)
        {
            if (_cellToCache.TryGetValue(thing.Position, out CellStorage model))
            {
                model.Update(thing);
            }
        }

        private void Init(List<IntVec3> cells, Map map)
        {
            foreach (IntVec3 cell in cells)
            {
                _cellToCache[cell] = new CellStorage();
                foreach (Thing thing in cell.GetThingList(map))
                {
                    if (_settings.AllowedToAccept(thing))
                    {
                        this.Add(thing);
                    }
                }
            }
        }
    }
}
