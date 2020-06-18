using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

using Model = DeepStorage.Deep_Storage_Cell_Storage_Model;

namespace LWM.DeepStorage
{
    public class Deep_Storage_Building : Building_Storage, ICollection<Thing>
    {
        private Dictionary<Thing, IntVec3> _thingToCell = new Dictionary<Thing, IntVec3>();

        private Dictionary<IntVec3, Model> _cellToCache = new Dictionary<IntVec3, Model>();

        public float CarriedWeight => _cellToCache.Values.Sum(model => model.TotalWeight);

        public int Count => _cellToCache.Sum(pair => pair.Value.Count);

        public bool IsReadOnly => false;

        public override void PostMapInit()
        {
            base.PostMapInit();
            foreach (IntVec3 cell in AllSlotCellsList())
            {
                _cellToCache[cell] = new Model();
                foreach (Thing thing in cell.GetThingList(this.MapHeld))
                {
                    if (this.Accepts(thing))
                    {
                        this.Add(thing);
                    }
                }
            }
        }

        public override void Notify_ReceivedThing(Thing newItem)
        {
            base.Notify_ReceivedThing(newItem);
            Add(newItem);
        }

        public override void Notify_LostThing(Thing newItem)
        {
            base.Notify_LostThing(newItem);
            Remove(newItem);
        }

        public void Add(Thing item)
        {
            _thingToCell[item] = item.Position;
            _cellToCache[item.Position].Add(item);
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
            return _thingToCell.Remove(item) & _cellToCache[item.Position].Remove(item);
        }

        public IEnumerator<Thing> GetEnumerator()
        {
            return _thingToCell.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _thingToCell.Keys.GetEnumerator();
        }
    }
}
