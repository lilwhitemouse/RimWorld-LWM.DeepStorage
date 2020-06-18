using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace LWM.DeepStorage
{
    public class Deep_Storage_Cell_Storage_Model : ICollection<Thing>
    {
        private Dictionary<Thing, float> _addedWeight = new Dictionary<Thing, float>();

        public IntVec3 Cell { get; private set; }

        public List<Thing> Things { get; private set; } = new List<Thing>();

        public List<Thing> NonFullThings { get; private set; } = new List<Thing>();

        public float TotalWeight { get; private set; }

        public int Count => Things.Count;

        public bool IsReadOnly => false;

        public void Add(Thing item)
        {
            Things.Add(item);
            TotalWeight += (_addedWeight[item] = item.GetStatValue(StatDefOf.Mass) * item.stackCount);
            this.UpdateNonFullThing(item);
        }

        public void Clear()
        {
            Things.Clear();
            TotalWeight = 0;
        }

        public bool Contains(Thing item)
        {
            return Things.Contains(item);
        }

        public void CopyTo(Thing[] array, int arrayIndex)
        {
            Things.CopyTo(array, arrayIndex);
        }

        public IEnumerator<Thing> GetEnumerator()
        {
            return Things.GetEnumerator();
        }

        public bool Remove(Thing item)
        {
            if (Things.Remove(item))
            {
                TotalWeight -= item.GetStatValue(StatDefOf.Mass) * item.stackCount;
                _addedWeight.Remove(item);
                this.NonFullThings.Remove(item);
                return true;
            }

            return false;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (Things as IEnumerable).GetEnumerator();
        }

        public void Update(Thing item)
        {
            TotalWeight -= _addedWeight[item];
            TotalWeight += (_addedWeight[item] = item.GetStatValue(StatDefOf.Mass) * item.stackCount);
            this.UpdateNonFullThing(item);
        }

        private void UpdateNonFullThing(Thing item)
        {
            if (item.stackCount < item.def.stackLimit)
                this.NonFullThings.Add(item);
            else
                this.NonFullThings.Remove(item);
        }
    }
}
