using System;
using System.Collections.Generic;
using System.Reflection;
using Harmony;
using RimWorld;
using UnityEngine;
using Verse;

// RT_Shelves:
using System.Linq;
using Verse.AI;


namespace LWM.DeepStorage
{
    /***************************************************
     * Select Deep Storage Unit
     * 
     * It's a pain to click thru 10 items to get to the
     * Deep Storage Unit.
     * We patch Selector.cs's HandleMapClicks to allow
     * selecting the Deep Storage Unit by right clicking
     * after selecting an item in it.
     * 
     */

    [HarmonyPatch(typeof(Selector), "HandleMapClicks")]
    class Patch_HandleMapClicks {
        static bool Prefix(Selector __instance, List<object> ___selected) {
            if (Event.current.type == EventType.MouseDown) {
                // Right mouse clicked and some item selected:
                if (Event.current.button == 1 && ___selected.Count == 1 && !(___selected[0] is Pawn)) {
                    Thing t = ___selected[0] as Thing;
                    if (t==null) {
                        return true; // Don't know what it was...
                    }
                    if (t.Map == null) {
                        return true; // Don't know where it is...
                    }
                    if (t.Position == IntVec3.Invalid) {
                        return true; // Don't know how it got selected, either :p
                    }
                    if (t.Map != Find.CurrentMap) {
                        return true; // Don't know where the player is looking
                    }
                    if (Utils.CanStoreMoreThanOneThingAt(t.Map,t.Position)) {
                        __instance.ClearSelection();
                        // Select the Deep Storage Unit:
                        __instance.Select(t.Position.GetSlotGroup(t.Map).parent);
                        Event.current.Use();
                        return false;
                    }
                }
            }
            return true; // not us
        }
    } // end HandleMapClick's patch


}

// Used under GPL 3 from Ratysz.  Also with permission.  Thanks, RT!
// https://github.com/Ratysz/RT_Shelves/blob/master/Source/Patches_FloatMenuMakerMap.cs
// Note that this is not every possible humanlike order - things involving caravans, trips, etc?
namespace RT_Shelves {
    [HarmonyPatch(typeof(FloatMenuMakerMap), "AddHumanlikeOrders")]
    class Patch_AddHumanlikeOrders {
        static void Postfix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts) {
            var cell = clickPos.ToIntVec3();
            if (pawn.equipment != null) {
                foreach (var equipment in cell.GetThingList(pawn.Map).OfType<ThingWithComps>().Where(t => t.TryGetComp<CompEquippable>() != null).Skip(1)) {
                    string labelShort = equipment.LabelShort;
                    FloatMenuOption option;
                    if (equipment.def.IsWeapon && pawn.story.WorkTagIsDisabled(WorkTags.Violent)) {
                        option = new FloatMenuOption("CannotEquip".Translate(labelShort) + " (" + "IsIncapableOfViolenceLower".Translate(pawn.LabelShort, pawn) + ")", null, MenuOptionPriority.Default, null, null, 0f, null, null);
                    } else if (!pawn.CanReach(equipment, PathEndMode.ClosestTouch, Danger.Deadly, false, TraverseMode.ByPawn)) {
                        option = new FloatMenuOption("CannotEquip".Translate(labelShort) + " (" + "NoPath".Translate() + ")", null, MenuOptionPriority.Default, null, null, 0f, null, null);
                    } else if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) {
                        option = new FloatMenuOption("CannotEquip".Translate(labelShort) + " (" + "Incapable".Translate() + ")", null, MenuOptionPriority.Default, null, null, 0f, null, null);
                    } else if (equipment.IsBurning()) {
                        option = new FloatMenuOption("CannotEquip".Translate(labelShort) + " (" + "BurningLower".Translate() + ")", null, MenuOptionPriority.Default, null, null, 0f, null, null);
                    } else {
                        string text5 = "Equip".Translate(labelShort);
                        if (equipment.def.IsRangedWeapon && pawn.story != null && pawn.story.traits.HasTrait(TraitDefOf.Brawler)) {
                            text5 = text5 + " " + "EquipWarningBrawler".Translate();
                        }
                        option = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text5, delegate
                        {
                            equipment.SetForbidden(false, true);
                            pawn.jobs.TryTakeOrderedJob(new Job(JobDefOf.Equip, equipment), JobTag.Misc);
                            MoteMaker.MakeStaticMote(equipment.DrawPos, equipment.Map, ThingDefOf.Mote_FeedbackEquip, 1f);
                            PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.EquippingWeapons, KnowledgeAmount.Total);
                        }, MenuOptionPriority.High, null, null, 0f, null, null), pawn, equipment, "ReservedBy");
                    }
                    opts.Add(option);
                }
            }
            if (pawn.apparel != null) {
                foreach (var apparel in cell.GetThingList(pawn.Map).OfType<Apparel>().Skip(1)) {
                    FloatMenuOption option;
                    if (!pawn.CanReach(apparel, PathEndMode.ClosestTouch, Danger.Deadly, false, TraverseMode.ByPawn)) {
                        option = new FloatMenuOption("CannotWear".Translate(apparel.Label, apparel) + " (" + "NoPath".Translate() + ")", null, MenuOptionPriority.Default, null, null, 0f, null, null);
                    } else if (apparel.IsBurning()) {
                        option = new FloatMenuOption("CannotWear".Translate(apparel.Label, apparel) + " (" + "BurningLower".Translate() + ")", null, MenuOptionPriority.Default, null, null, 0f, null, null);
                    } else if (!ApparelUtility.HasPartsToWear(pawn, apparel.def)) {
                        option = new FloatMenuOption("CannotWear".Translate(apparel.Label, apparel) + " (" + "CannotWearBecauseOfMissingBodyParts".Translate() + ")", null, MenuOptionPriority.Default, null, null, 0f, null, null);
                    } else {
                        option = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("ForceWear".Translate(apparel.LabelShort, apparel), delegate
                        {
                            apparel.SetForbidden(false, true);
                            Job job = new Job(JobDefOf.Wear, apparel);
                            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                        }, MenuOptionPriority.High, null, null, 0f, null, null), pawn, apparel, "ReservedBy");
                    }
                    opts.Add(option);
                }
            }
        }
    }
}