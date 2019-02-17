using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
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
                    // TODO: make this cleaner:
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


    /************************* Let user click on DSU instead of giant pile of stacks! ******************/
    /* We would like it so when a player clicks on a DSU that has stuff in it,
     * the DSU gets selected instead of the first item, then the 2nd item, etc.
     * 
     * The reason the items get selected first is that ThingsUnderMouse sorts
     * usng CompareThingsByDrawAltitude - and buildings are below items.
     * So, we add a call to SortForDeepStorage.
     *
     * However, we only want to use the SortForDeepStorage if we are selecting
     * a single object!  If we are selecting all Wheat on the screen, (double click) 
     * we almost certainly want the default behavior.
     * 
     * So we control whether we sort by adding a flag to SelectUnderMouse.
     * 
     * Basically, we make ThingsUnderMouse() into ThingsUnderMouse(useSortForDeepStorage=false),
     * and make SelectUnderMouse() call it with true.
     */
    [HarmonyPatch(typeof(Verse.GenUI),"ThingsUnderMouse")]
    public static class Patch_GenUI_ThingsUnderMouse {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            // First marker we are looking for is
            //   ldftn int32 Verse.GenUI::CompareThingsByDrawAltitude(class Verse.Thing, class Verse.Thing)
            var wrongComparison = Harmony.AccessTools.Method("Verse.GenUI:CompareThingsByDrawAltitude");
            if (wrongComparison == null) {
                Log.Error("LWM: Deep Storage: harmony transpiler fail: no CompareThingsByDrawAltitude");
            }
            // Second marker we are looking for is
            //   callvirt instance void class
            //     [mscorlib]System.Collections.Generic.List`1<class Verse.Thing>::Sort(class [mscorlib]System.Comparison`1<!0>)
            var sortFunction = typeof(System.Collections.Generic.List<Thing>)
                               .GetMethod("Sort", new Type[] {typeof(System.Comparison<Thing>)});

            var code = new List<CodeInstruction>(instructions);
            var i = 0; // using multiple 'for' loops
            bool foundMarkerOne=false;
            for (; i < code.Count; i++) {
                yield return code[i];
                if (code[i].opcode == OpCodes.Ldftn && code[i].operand == wrongComparison) {
                    foundMarkerOne=true;
                }
                if (foundMarkerOne && code[i].opcode == OpCodes.Callvirt && code[i].operand == sortFunction) {
                    // We insert our own sorting function here, to put DSUs on top of click order:
                    yield return new CodeInstruction(OpCodes.Ldloc_S,6); // the temporary list
                    yield return new CodeInstruction(OpCodes.Call, Harmony.AccessTools.
                                                     Method("LWM.DeepStorage.Patch_GenUI_ThingsUnderMouse:SortForDeepStorage"));
                    i++;
                    break; // our work is done here
                }
            }
            for (; i < code.Count; i++) { // finish up
                yield return code[i];
            }
        }

        static public bool useSortForDeepStorage=false;
        // Put DeepStorage at the top of the list:
        static public void SortForDeepStorage(List<Thing> list) {
            if (!useSortForDeepStorage) return;
            useSortForDeepStorage=false;
            if (list.Count <2) return;
            for (int i=list.Count-1; i>0; i--) { // don't need to check i=0
                if (list[i].TryGetComp<CompDeepStorage>()!=null) {
                    Thing t=list[i];
                    list.RemoveAt(i);
                    list.Insert(0,t);
                    return; // That's all we needed!
                }
            }
        }        
    }
    [HarmonyPatch(typeof(RimWorld.Selector), "SelectUnderMouse")]
    static class Make_Select_Under_Mouse_Use_SortForDeepStorage {
        static void Prefix() {
            Patch_GenUI_ThingsUnderMouse.useSortForDeepStorage=true;
        }
    }
    [HarmonyPatch(typeof(RimWorld.Selector),"SelectAllMatchingObjectUnderMouseOnScreen")]
    static class Make_DoubleClick_Work {
        static void Prefix(Selector __instance) {
            if (__instance.SingleSelectedThing?.TryGetComp<CompDeepStorage>()!=null)
                __instance.ClearSelection();
        }
    }

    /********* UI ITab from sumghai - thanks! ********/
    public class ITab_DeepStorage_Inventory : ITab {
        private Vector2 scrollPosition = Vector2.zero;

        private float scrollViewHeight=1000f;

        private const float TopPadding = 20f;

        public static readonly Color ThingLabelColor = new Color(0.9f, 0.9f, 0.9f, 1f);

        public static readonly Color HighlightColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        private const float ThingIconSize = 28f;

        private const float ThingRowHeight = 28f;

        private const float ThingLeftX = 36f;

        private const float StandardLineHeight = 22f;

        private static List<Thing> cabinetInvList = new List<Thing>();

        public float cabinetMaxCapacity = 123; // Placeholder variable for cabinet max capacity; replace with XML-driven value

        public ITab_DeepStorage_Inventory() {
            this.size = new Vector2(460f, 450f);
            this.labelKey = "Contents"; // could define <LWM.Contents>Contents</LWM.Contents> in Keyed language, but why not use what's there.
        }

        protected override void FillTab() {
            Text.Font = GameFont.Small;
            Rect rect = new Rect(0f, 20f, this.size.x, this.size.y - 20f);
            Rect rect2 = rect.ContractedBy(10f);
            Rect position = new Rect(rect2.x, rect2.y, rect2.width, rect2.height);
            GUI.BeginGroup(position);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            //TODO: handle each cell separately?

            cabinetInvList.Clear();

            Building_Storage cabinetBuilding = SelThing as Building_Storage;
            List<IntVec3> cabinetStorageCells = cabinetBuilding.AllSlotCellsList();
            foreach (IntVec3 storageCell in cabinetStorageCells) {
                foreach (Thing t in cabinetBuilding.Map.thingGrid.ThingsListAt(storageCell)) {
                    if (t.Spawned && t.def.EverStorable(false)) cabinetInvList.Add(t);
                }
            }

            cabinetInvList = cabinetInvList.OrderBy((Thing x) => x.def.defName).
                ThenByDescending((Thing x) => {
                    QualityCategory c;
                    x.TryGetQuality(out c);
                    return (int)c;
                }).
                ThenByDescending((Thing x) => (x.HitPoints / x.MaxHitPoints)).ToList();

            float curY = 0f;
            Widgets.ListSeparator(ref curY, position.width, labelKey.Translate()
                #if DEBUG
                +"    ("+cabinetBuilding.ToString()+")" // extra info for debugging
                #endif
                );
            curY += 5f;
            // Show count of contents, mass, etc:
            DisplayHeaderInfo(ref curY, position.width-16f,
                              cabinetBuilding, cabinetStorageCells.Count, cabinetInvList);

            Rect outRect = new Rect(0f, 10f + curY, position.width, position.height-curY);
            // viewRect is inside the ScrollView, so it starts at y=0f
            Rect viewRect = new Rect(0f, 0f, position.width - 16f, this.scrollViewHeight);


            Widgets.BeginScrollView(outRect, ref this.scrollPosition, viewRect, true);

            curY = 0f; // now inside ScrollView

            for (int i = 0; i < cabinetInvList.Count; i++) {
                this.DrawThingRow(ref curY, viewRect.width, cabinetInvList[i]);
            }
            cabinetInvList.Clear();

            if (Event.current.type == EventType.Layout) {
                this.scrollViewHeight = curY + 25f; //25f buffer
            }

            Widgets.EndScrollView();
            GUI.EndGroup();
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawThingRow(ref float y, float width, Thing thing) {
            Rect rect = new Rect(0f, y, width, 28f);
            Widgets.InfoCardButton(rect.width - 24f, y, thing);
            rect.width -= 24f;

            Rect rect2 = new Rect(rect.width - 24f, y, 24f, 24f);
            TooltipHandler.TipRegion(rect2, "Allow/Forbid"); // Replace "Allow/Forbid" with a translated entry in a Keyed Language XML file

            bool forbidFlag = !thing.IsForbidden(Faction.OfPlayer);

            Widgets.Checkbox(rect2.x, rect2.y, ref forbidFlag, 24f, false, true, null, null);

            ForbidUtility.SetForbidden(thing, !forbidFlag,false);

            rect.width -= 24f;

            Rect rect3 = rect;
            rect3.xMin = rect3.xMax - 60f;
            RimWorld.Planet.CaravanThingsTabUtility.DrawMass(thing, rect3);
            rect.width -= 60f;
            if (Mouse.IsOver(rect)) {
                GUI.color = ITab_Pawn_Gear.HighlightColor;
                GUI.DrawTexture(rect, TexUI.HighlightTex);
            }
            if (thing.def.DrawMatSingle != null && thing.def.DrawMatSingle.mainTexture != null) {
                Widgets.ThingIcon(new Rect(4f, y, 28f, 28f), thing, 1f);
            }
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = ITab_Pawn_Gear.ThingLabelColor;
            Rect rect4 = new Rect(36f, y, rect.width - 36f, rect.height);
            string text = thing.LabelCap;
            Apparel apparel = thing as Apparel;
            Text.WordWrap = false;
            Widgets.Label(rect4, text.Truncate(rect4.width, null));
            Text.WordWrap = true;
            string text2 = thing.DescriptionDetailed;
            if (thing.def.useHitPoints) {
                string text3 = text2;
                text2 = string.Concat(new object[]
                {
                    text3,
                    "\n",
                    thing.HitPoints,
                    " / ",
                    thing.MaxHitPoints
                });
            }
            TooltipHandler.TipRegion(rect, text2);
            y += 28f;
        }

        // LWM rewrote most of this method to meet their implementation of CompDeepStorage
        private void DisplayHeaderInfo(ref float curY, float width, Building_Storage cabinet, 
                                       int numCells, List<Thing> itemsList) {
            CompDeepStorage cds = cabinet.GetComp<CompDeepStorage>();
            if (cds == null) return; // what are we even doing here, mmm?

            Rect rect = new Rect(0f, curY, width, 22f);
            if (itemsList.Count<1) {
                Widgets.Label(rect, "NoItemsAreStoredHere".Translate());
                curY += 22;
                return;
            }
            float itemsTotalMass = 0; // or Bulk for CE ;p
            for (int i=0; i<itemsList.Count; i++) {
                itemsTotalMass += itemsList[i].GetStatValue(cds.stat, true) * (float)itemsList[i].stackCount;
            }
            if (cds.limitingTotalFactorForCell > 0f) {
                Widgets.Label(rect, "LWM.ContentHeaderOneOf".Translate(itemsList.Count.ToString(),
                              cds.stat.ToString(), itemsTotalMass.ToString("0.##"),
                              (cds.limitingTotalFactorForCell * numCells).ToString("0.##")));
            } else {
                Widgets.Label(rect, "LWM.ContentHeaderOne".Translate(itemsList.Count.ToString(),
                              cds.stat.ToString(), itemsTotalMass.ToString("0.##")));
            }
            curY += 22f;
        }
    } /* End sumghai's itab */
    /* Now make the itab open automatically! */
    /*   Thanks to Falconne for doing this in ImprovedWorkbenches, and showing how darn useful it is! */
    [HarmonyPatch(typeof(Selector), "Select")]
    public static class Open_DS_Tab_On_Select {
        public static void Postfix(Selector __instance) {
            if (__instance.NumSelected != 1) return;
            Thing t = __instance.SingleSelectedThing;
            if (t == null) return;
            if (!(t is ThingWithComps)) return;
            CompDeepStorage cds = t.TryGetComp<CompDeepStorage>();
            if (cds == null) return;
            ITab_DeepStorage_Inventory tab = t.GetInspectTabs().OfType<ITab_DeepStorage_Inventory>().First();
            if (tab == null) { Log.Error("LWM Deep Storage object " + t + " does not have an inventory tab?");  return; }
            var pane= (MainTabWindow_Inspect)MainButtonDefOf.Inspect.TabWindow;
            tab.OnOpen();
            pane.OpenTabType = typeof(ITab_DeepStorage_Inventory);
        }
    }
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
