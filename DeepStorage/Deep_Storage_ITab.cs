using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LWM.DeepStorage
{
    /********* UI ITab ******************************
     * Original ITab from sumghai - thanks!         *
     *   That saved a lot of work.                  *
     * Now almost all rewritten, with many requests *
     *   from various ppl on steam                  *
     *                                              */
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

        public ITab_DeepStorage_Inventory() {
            this.size = new Vector2(460f, 450f);
            this.labelKey = "Contents"; // could define <LWM.Contents>Contents</LWM.Contents> in Keyed language, but why not use what's there.
        }

        protected override void FillTab() {
            Building_Storage storageBuilding = this.SelThing as Building_Storage; // don't attach this to other things, 'k?
            List<Thing> storedItems;
//TODO: set fonts ize, etc.
            Text.Font = GameFont.Small;
            // 10f border:
            Rect frame=new Rect(10f,10f, this.size.x-10, this.size.y-10);
            GUI.BeginGroup(frame);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            /*******  Title *******/
            float curY = 0f;
            Widgets.ListSeparator(ref curY, frame.width, labelKey.Translate()
                #if DEBUG
                +"    ("+storageBuilding.ToString()+")" // extra info for debugging
                #endif
                );
            curY += 5f;
            /****************** Header: Show count of contents, mass, etc: ****************/
            //TODO: handle each cell separately?
            string header, headerTooltip;
            CompDeepStorage cds=storageBuilding.GetComp<CompDeepStorage>();
            if (cds!=null) {
                storedItems=cds.getContentsHeader(out header, out headerTooltip);
            } else {
                storedItems=CompDeepStorage.genericContentsHeader(storageBuilding, out header, out headerTooltip);
            }
            Rect tmpRect=new Rect(8f, curY, frame.width-16, Text.CalcHeight(header, frame.width-16));
            Widgets.Label(tmpRect, header);
            // TODO: tooltip.  Not that it's anything but null now
            curY+=(tmpRect.height); //todo?

            /*************          ScrollView              ************/
            /*************          (contents)              ************/
            storedItems = storedItems.OrderBy((Thing x) => x.def.defName).
                ThenByDescending((Thing x) => {
                    QualityCategory c;
                    x.TryGetQuality(out c);
                    return (int)c;
                }).
                ThenByDescending((Thing x) => (x.HitPoints / x.MaxHitPoints)).ToList();
            // outRect is the is the rectangle that is visible on the screen:
            Rect outRect = new Rect(0f, 10f + curY, frame.width, frame.height-curY);
            // viewRect is inside the ScrollView, so it starts at y=0f
            Rect viewRect = new Rect(0f, 0f, frame.width - 16f, this.scrollViewHeight);
            // 16f ensures plenty of room for scrollbars.
            // scrollViewHeight is set at the end of this call (via layout?); it is the proper
            //   size the next time through, so it all works out.
            Widgets.BeginScrollView(outRect, ref this.scrollPosition, viewRect, true);

            curY = 0f; // now inside ScrollView
            if (storedItems.Count<1) {
                Widgets.Label(viewRect, "NoItemsAreStoredHere".Translate());
                curY += 22;
            }

            for (int i = 0; i < storedItems.Count; i++) {
                this.DrawThingRow(ref curY, viewRect.width, storedItems[i]);
            }

            if (Event.current.type == EventType.Layout) {
                this.scrollViewHeight = curY + 25f; //25f buffer   -- ??
            }

            Widgets.EndScrollView();
            GUI.EndGroup();
            //TODO: this should get stored at top and set here.
            GUI.color = Color.white;
            //TODO: this should get stored at top and set here.
            // it should get set to whatever draw-row uses at top
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawThingRow(ref float y, float width, Thing thing) {
            // Sumghai started from the right, as several things in vanilla do, and that's fine with me:

            /************************* InfoCardButton *************************/
            //       (it's the little "i" that pulls up full info on the item.)
            //   It's 24f by 24f in size
            width-=24f;
            Widgets.InfoCardButton(width, y, thing);

            /************************* Allow/Forbid toggle *************************/
            //   We make this 24 by 24 too:
            width-=24f;
            Rect forbidRect = new Rect(width, y, 24f, 24f); // is creating this rect actually necessary?
            bool allowFlag = !thing.IsForbidden(Faction.OfPlayer);
            bool tmpFlag=allowFlag;
            if (allowFlag)
                TooltipHandler.TipRegion(forbidRect, "CommandNotForbiddenDesc".Translate());
            else
                TooltipHandler.TipRegion(forbidRect, "CommandForbiddenDesc".Translate());
//            TooltipHandler.TipRegion(forbidRect, "Allow/Forbid"); // TODO: Replace "Allow/Forbid" with a translated entry in a Keyed Language XML file
            Widgets.Checkbox(forbidRect.x, forbidRect.y, ref allowFlag, 24f, false, true, null, null);
            if (allowFlag!=tmpFlag) // spamming SetForbidden is bad when playing multi-player - it spams Sync requests
                ForbidUtility.SetForbidden(thing, !allowFlag,false);

            /************************* Mass *************************/
            width-=60f; // Caravans use 100f
            Rect massRect = new Rect(width,y,60f,28f);
            RimWorld.Planet.CaravanThingsTabUtility.DrawMass(thing, massRect);
            /************************* How soon does it rot? *************************/
            // Some mods add non-food items that rot, so we track those too:
            CompRottable cr = thing.TryGetComp<CompRottable>();
            if (cr != null) {
                int rotTicks=Math.Min(int.MaxValue, cr.TicksUntilRotAtCurrentTemp);
                if (rotTicks < 36000000) {
                    width-=60f;  // Caravans use 75f?  TransferableOneWayWidget.cs
                    Rect rotRect=new Rect(width,y,60f,28f);
                    GUI.color = Color.yellow;
                    Widgets.Label(rotRect, (rotTicks/60000f).ToString("0.#"));
                    GUI.color = Color.white;
                    TooltipHandler.TipRegion(rotRect, "DaysUntilRotTip".Translate());
                }
            } // finish how long food will last

            /************************* Text area *************************/
            // TODO: use a ButtonInvisible over the entire area with a label and the icon.
            Rect itemRect = new Rect(0f,y,width,28f);
            if (Mouse.IsOver(itemRect)) {
                GUI.color = ITab_Pawn_Gear.HighlightColor;
                GUI.DrawTexture(itemRect, TexUI.HighlightTex);
            }
            if (thing.def.DrawMatSingle != null && thing.def.DrawMatSingle.mainTexture != null) {
                Widgets.ThingIcon(new Rect(4f, y, 28f, 28f), thing, 1f);
            }
//TODO: set all this once:
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = ITab_Pawn_Gear.ThingLabelColor; // TODO: Aaaaah, sure?
            Rect textRect = new Rect(36f, y, itemRect.width - 36f, itemRect.height);
            string text = thing.LabelCap;
            Text.WordWrap = false;
            Widgets.Label(textRect, text.Truncate(textRect.width, null));
//            if (Widgets.ButtonText(rect4, text.Truncate(rect4.width, null),false)) {
            if (Widgets.ButtonInvisible(itemRect)) {
                Find.Selector.ClearSelection();
                Find.Selector.Select(thing);
            }
//TODO: etc
            Text.WordWrap = true;
            /************************* mouse-over description *************************/
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
            TooltipHandler.TipRegion(itemRect, text2);
            y += 28f;
        } // end draw thing row
    } /* End itab */
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
            if (tab == null) { tab = t.GetInspectTabs().OfType<ITab_DeepStorage_Inventory>().First(); }
            if (tab == null) { Log.Error("LWM Deep Storage object " + t + " does not have an inventory tab?");  return; }
            tab.OnOpen();
            if (tab is ITab_DeepStorage_Inventory)
                pane.OpenTabType = typeof(ITab_DeepStorage_Inventory);
            else
                pane.OpenTabType = typeof(ITab_Storage);
        }
    } // end patch of Select to open ITab
}
