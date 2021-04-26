using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace LWM.DeepStorage
{
	[HarmonyPatch(typeof(TantrumMentalStateUtility), "CanSmash")]
	internal class Patch_TantrumMentalStateUtility_CanSmash
	{
		// Token: 0x06000154 RID: 340 RVA: 0x0000BFA8 File Offset: 0x0000A1A8
		[HarmonyPostfix]
		private static void AfterCanSmash(Pawn pawn, Thing thing, ref bool __result)
		{
			if (__result && (!(thing is ThingWithComps) || thing.TryGetComp<CompDeepStorage>() == null))
			{
				SlotGroup slotGroup = thing.Position.GetSlotGroup(pawn.Map);
				ThingWithComps thingWithComps = ((slotGroup != null) ? slotGroup.parent : null) as ThingWithComps;
				CompDeepStorage compDeepStorage = (thingWithComps != null) ? thingWithComps.GetComp<CompDeepStorage>() : null;
				__result = (compDeepStorage == null);
			}
		}
	}
}
