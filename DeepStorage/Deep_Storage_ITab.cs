using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using Harmony;
using RimWorld;
using UnityEngine;
using Verse;

namespace LWM.DeepStorage
{
    /********* UI ITab *****************************
     * Original ITab from sumghai - thanks!        *
     *   That saved a lot of work.                 *
     * Other requests from various ppl on steam    *
     *                                             */
    public class ITab_DeepStorage_Contents : ITab {
        private Vector2 scrollPosition = Vector2.zero;
        private float scrollViewHeight=1000f;
        private const float TopPadding = 20f;
        public static readonly Color ThingLabelColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        public static readonly Color HighlightColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private const float ThingIconSize = 28f;
        private const float ThingRowHeight = 28f;
        private const float ThingLeftX = 36f;
        private const float StandardLineHeight = 22f;
        private static List<Thing> listOfStoredItems = new List<Thing>();
//        public float cabinetMaxCapacity = 123; // Placeholder variable for cabinet max capacity; replace with XML-driven value

        public ITab_DeepStorage_Contents() {
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

            listOfStoredItems.Clear();

            Building_Storage storageBuilding = SelThing as Building_Storage;
            List<IntVec3> cabinetStorageCells = storageBuilding.AllSlotCellsList();
            foreach (IntVec3 storageCell in cabinetStorageCells) {
                foreach (Thing t in storageBuilding.Map.thingGrid.ThingsListAt(storageCell)) {
                    if (t.Spawned && t.def.EverStorable(false)) listOfStoredItems.Add(t);
                }
            }

            listOfStoredItems = listOfStoredItems.OrderBy((Thing x) => x.def.defName).
                ThenByDescending((Thing x) => {
                    QualityCategory c;
                    x.TryGetQuality(out c);
                    return (int)c;
                }).
                ThenByDescending((Thing x) => (x.HitPoints / x.MaxHitPoints)).ToList();

            float curY = 0f;
            Widgets.ListSeparator(ref curY, position.width, labelKey.Translate()
                #if DEBUG
                +"    ("+storageBuilding.ToString()+")" // extra info for debugging
                #endif
                );
            curY += 5f;
            // Show count of contents, mass, etc:
            DisplayHeaderInfo(ref curY, position.width-16f,
                              storageBuilding, cabinetStorageCells.Count, listOfStoredItems);


            /*************          ScrollView              ************/
            Rect outRect = new Rect(0f, 10f + curY, position.width, position.height-curY);
            // viewRect is inside the ScrollView, so it starts at y=0f
            Rect viewRect = new Rect(0f, 0f, position.width - 16f, this.scrollViewHeight);

            Widgets.BeginScrollView(outRect, ref this.scrollPosition, viewRect, true);

            curY = 0f; // now inside ScrollView

            for (int i = 0; i < listOfStoredItems.Count; i++) {
                this.DrawThingRow(ref curY, viewRect.width, listOfStoredItems[i]);
            }
            listOfStoredItems.Clear();

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
            // Header information regardless of what the storage building is:
            Rect rect = new Rect(0f, curY, width, 22f);
            // TODO: Add hooks for other mods:
            //   E.g., StockpileForDisaster has a nice little checkbox that shows whether 
            //   pawns can freely take from the unit, or whether restrictions are in effect
            if (itemsList.Count<1) {
                Widgets.Label(rect, "NoItemsAreStoredHere".Translate());
                curY += 22;
                return;
            }

            CompDeepStorage cds = cabinet.GetComp<CompDeepStorage>();
            if (cds == null) return; // what are we even doing here, mmm?  In a vanilla shelf, probably!


            
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
            // Off to a good start; it's a DSU
            // Check to see if a tab is already open.
            var pane= (MainTabWindow_Inspect)MainButtonDefOf.Inspect.TabWindow;
            Type alreadyOpenTabType = pane.OpenTabType;
            if (alreadyOpenTabType != null) {
                var listOfTabs=t.GetInspectTabs();
                foreach (var x in listOfTabs) {
                    if (x.GetType() == alreadyOpenTabType) { // Misses any subclassing?
                        return; // standard Selector behavior should kick in.
                    }
                }
            }
            // If not, open ours!
            // TODO: ...make this happen for shelves, heck, any storage buildings?
            ITab tab = null;
            /* If there are no items stored, default intead to settings (preferably with note about being empty?) */
            // If we find a stored item, open Contents tab:
            // TODO: Make storage settings tab show label if it's empty
            if (t.Spawned && t is IStoreSettingsParent && t is ISlotGroupParent) {
                foreach (IntVec3 c in ((ISlotGroupParent)t).GetSlotGroup().CellsList) {
                    List<Thing> l = t.Map.thingGrid.ThingsListAt(c);
                    foreach (Thing tmp in l) {
                        if (tmp.def.EverStorable(false)) {
                            goto EndLoop;
                            // Seriously?  C# doesn't have "break 2;"?
                        }
                    }
                }
                tab = t.GetInspectTabs().OfType<ITab_Storage>().First();
            }
          EndLoop:
            if (tab == null) { tab = t.GetInspectTabs().OfType<ITab_DeepStorage_Contents>().First(); }
            if (tab == null) { Log.Error("LWM Deep Storage object " + t + " does not have an inventory tab?");  return; }
            tab.OnOpen();
            if (tab is ITab_DeepStorage_Contents)
                pane.OpenTabType = typeof(ITab_DeepStorage_Contents);
            else
                pane.OpenTabType = typeof(ITab_Storage);
        }
    } // end patch of Select to open ITab

}
