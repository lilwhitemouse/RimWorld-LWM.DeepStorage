using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace LWM.DeepStorage
{
    public class ThingCountComparer : EqualityComparer<Thing>
    {
        public static ThingCountComparer Instance = new ThingCountComparer();

        private ThingCountComparer()
        {
        }

        #region Overrides of EqualityComparer<Thing>

        public override bool Equals(Thing x, Thing y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x is null || y is null)
                return false;

            return x.CanStackWith(y) && x.stackCount == y.stackCount;
        }

        public override int GetHashCode(Thing obj)
        {
            return (obj.thingIDNumber * 397) ^ obj.stackCount;
        }

        #endregion
    }
}
