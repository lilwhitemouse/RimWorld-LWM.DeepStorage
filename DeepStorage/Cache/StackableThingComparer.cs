using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace LWM.DeepStorage
{
    public class StackableThing_Comparer : IEqualityComparer<Thing>
    {
        public static StackableThing_Comparer Instance = new StackableThing_Comparer();

        private StackableThing_Comparer()
        {
        }

        #region Implementation of IEqualityComparer<in Thing>

        public bool Equals(Thing x, Thing y)
        {
            if (x == y)
                return true;

            if (x == null || y == null)
                return false;

            return x.CanStackWith(y);
        }

        public int GetHashCode(Thing obj)
        {
            return (obj.def.GetHashCode() * 397) ^ (obj.Stuff?.GetHashCode() ?? 0);
        }

        #endregion
    }
}
