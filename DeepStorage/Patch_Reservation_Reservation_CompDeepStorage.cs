using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;
using Verse.AI;


namespace LWM.DeepStorage
{
    [HarmonyPatch(typeof(Verse.AI.ReservationManager), "Reserve")]
    class Patch_Reservation_Reservation_CompDeepStorage
    {
        static bool Prefix(Pawn claimant, Job job, LocalTargetInfo target, ref bool __result, Map ___map)
        {
            if (target.HasThing == false && ___map != null && target.Cell.InBounds(___map))
            {

                CompDeepStorage building_target = target.Cell.GetThingList(___map).Where(t => t is ThingWithComps thing && (thing.TryGetComp<CompDeepStorage>() != null)).Select(thing => thing.TryGetComp<CompDeepStorage>()).FirstOrDefault();
                if (building_target != null)
                {
                    __result = true;
                    return false;
                }
            }
            return true;
        }
    }
}
