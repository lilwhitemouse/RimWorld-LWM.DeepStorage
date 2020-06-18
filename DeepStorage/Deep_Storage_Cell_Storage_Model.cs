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
        public IntVec3 Cell { get; private set; }

        public List<Thing> Things { get; private set; } = new List<Thing>();

        public float TotalWeight { get; private set; }

        public int Count => Things.Count;

        public bool IsReadOnly => false;

        public void Add(Thing item)
        {
            Things.Add(item);
            TotalWeight += item.GetStatValue(StatDefOf.Mass);
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
                TotalWeight -= item.GetStatValue(StatDefOf.Mass);
                return true;
            }

            return false;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (Things as IEnumerable).GetEnumerator();
        }
    }
}
