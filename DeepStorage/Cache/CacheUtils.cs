using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace LWM.DeepStorage
{
    public static class CacheUtils
    {
        public static bool IsCachedDeepStorage(this SlotGroup slotGroup, out CompCachedDeepStorage comp)
        {
            if (slotGroup?.parent is Building_Storage building)
            {
                comp = building.TryGetComp<CompCachedDeepStorage>();
                return comp != null;
            }

            comp = null;
            return false;
        }
    }
}
