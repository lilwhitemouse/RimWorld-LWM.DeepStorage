using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

/// <summary>
///   Two Dialog windows to allow whiny RimWorld players to change settings for the Deep Storage units
///     and the logic to make that happen.
///   Basic idea:
///     on load, check if modifying units is turned on.  If so, once defs are loaded, do second
///     pass through the Setting's ExposeData() and this time, parse "DSU_LWM_defName_fieldName",
///     populate a dictionary with the default values, and change the defs on the fly.
///
///   This file contains the two dialog windows, keeps the default settings, and handles making the def changes.
///   The call to re-read the storage settings is done in ModSettings.cs, and the call to ExposeDSUSettings is
///     done in ModSettings' ExposeData.
/// </summary>

namespace LWM.DeepStorage
{
    // The window that lists all the DSUs available:
    public class Dialog_DS_Settings : Window {
        public Dialog_DS_Settings() {
			this.forcePause = true;
			this.doCloseX = true;
            this.doCloseButton = false;
			this.closeOnClickedOutside = true;
			this.absorbInputAroundWindow = true;
        }

		public override Vector2 InitialSize
		{
			get
			{
				return new Vector2(900f, 700f);
			}
		}

        public override void DoWindowContents(Rect inRect)
		{
            var contentRect = new Rect(0, 0, inRect.width, inRect.height - (CloseButSize.y + 10f)).ContractedBy(10f);
            bool scrollBarVisible = totalContentHeight > contentRect.height;
            var scrollViewTotal = new Rect(0f, 0f, contentRect.width - (scrollBarVisible ? ScrollBarWidthMargin : 0), totalContentHeight);
            Widgets.DrawHighlight(contentRect);
            Widgets.BeginScrollView(contentRect, ref scrollPosition, scrollViewTotal);
            float curY = 0f;
            Rect r=new Rect(0,curY,scrollViewTotal.width, LabelHeight);

            Widgets.CheckboxLabeled(r, "LWMDSperDSUturnOn".Translate(), ref Settings.allowPerDSUSettings);//TODO
            TooltipHandler.TipRegion(r, "LWMDSperDSUturnOnDesc".Translate());
            curY+=LabelHeight+1f;
            if (!Settings.allowPerDSUSettings) {
                r=new Rect(5f, curY, scrollViewTotal.width-10f, LabelHeight);
                Widgets.Label(r, "LWMDSperDSUWarning".Translate());
                curY+=LabelHeight;
            }
            Widgets.DrawLineHorizontal(0f, curY, scrollViewTotal.width);
            curY+=10f;

            // todo: make this static?
            //List<ThingDef> l=DefDatabase<ThingDef>.AllDefsListForReading.Where(ThingDef d => d.Has

            // Roll my own buttons, because dammit, I want left-justified buttons:
            //   (mirroring Widgets.ButtonTextWorker)
            GenUI.SetLabelAlign(TextAnchor.MiddleLeft);
            var bg=ContentFinder<Texture2D>.Get("UI/Widgets/ButtonBG", true);
            var bgmouseover=ContentFinder<Texture2D>.Get("UI/Widgets/ButtonBGMouseover", true);
            var bgclick=ContentFinder<Texture2D>.Get("UI/Widgets/ButtonBGClick", true);
            //  note: make own list b/c this can modify what's in the DefDatabase.
            foreach (ThingDef u in Settings.AllDeepStorageUnits.ToList()) {
                //////////////// Disble button: //////////////////
                // disabled if it's already been disabled previously
                //   or if it's slated to be disabled on window close
                bool isEnabled=!tracker.HasDefaultValueFor(u.defName, "def") &&
                    (this.unitsToBeDisabled==null || !unitsToBeDisabled.Contains(u));
                bool wasEnabled = isEnabled;
                Rect disableRect = new Rect(5f, curY, LabelHeight, LabelHeight);
                TooltipHandler.TipRegion(disableRect, "TODO: Add description. But basically, you can disable some units and they won't show up in game.\n\nVERY likely to cause unimportant errors in saved games.");
                Widgets.Checkbox(disableRect.x, disableRect.y, ref isEnabled, LabelHeight, false, true, null, null);
                if (!isEnabled && wasEnabled) { // newly disabled
                    Utils.Warn(Utils.DBF.Settings, "Marking unit for disabling: "+u.defName);
                    if (unitsToBeDisabled==null) unitsToBeDisabled=new HashSet<ThingDef>();
                    unitsToBeDisabled.Add(u); // hash sets don't care if it's already there!
                }
                if (isEnabled && !wasEnabled) { // add back:
                    Utils.Warn(Utils.DBF.Settings, "Restoring disabled unit: "+u.defName);
                    if (unitsToBeDisabled !=null &&  unitsToBeDisabled.Contains(u)) {
                        unitsToBeDisabled.Remove(u);
                    }
                    if (tracker.HasDefaultValueFor(u.defName, "def")) {
                        tracker.Remove(u.defName, "def");
                    }
                    if (!DefDatabase<ThingDef>.AllDefsListForReading.Contains(u))
                        ReturnDefToUse(u);
                }
                //////////////// Select def: //////////////////
                r=new Rect(10f+LabelHeight, curY, (scrollViewTotal.width)*2/3-12f-LabelHeight, LabelHeight);
                // Draw button-ish background:
                Texture2D atlas = bg;
				if (Mouse.IsOver(r))
				{
					atlas = bgmouseover;
					if (Input.GetMouseButton(0))
					{
						atlas = bgclick;
					}
				}
				Widgets.DrawAtlas(r, atlas);
                // button text:
                Widgets.Label(r, u.label+" (defName: "+u.defName+")");
                // button clickiness:
                if (Widgets.ButtonInvisible(r)) {
                    Find.WindowStack.Add(new Dialog_DSU_Settings(u));
                }
                //////////////// Reset button: //////////////////
                r=new Rect((scrollViewTotal.width)*2/3+2f,curY, (scrollViewTotal.width)/3-7f, LabelHeight);
                if (tracker.IsChanged(u.defName) && Widgets.ButtonText(r, "ResetBinding".Translate())) {
                    ResetDSUToDefaults(u.defName);
                }
                curY+=LabelHeight+2f;
            }
            GenUI.ResetLabelAlign();
            // end buttons

            Widgets.EndScrollView();
            // close button:
            r=new Rect(inRect.width/2-(CloseButSize.x/2), inRect.height-CloseButSize.y-5f, CloseButSize.x, CloseButSize.y);
            if (Widgets.ButtonText(r, "CloseButton".Translate())) {
                if (unitsToBeDisabled != null && unitsToBeDisabled.Count > 0) {
                    //TODO: add out-of-order flag.
                    foreach (ThingDef d in unitsToBeDisabled) {
                        Utils.Warn(Utils.DBF.Settings, "Closing Window: Removing def: "+d.defName);
                        RemoveDefFromUse(d);
                        tracker.AddDefaultValue(d.defName, "def", d);
                    }
                    unitsToBeDisabled=null;
                }
                Close();
            }
            r=new Rect(10f, inRect.height-CloseButSize.y-5f, 2*CloseButSize.x, CloseButSize.y);
            if (tracker.HasAnyDefaultValues && Widgets.ButtonText(r, "LWM.ResetAllToDefault".Translate())) {
                Utils.Mess(Utils.DBF.Settings, "Resetting all per-building storage settings to default:");
                ResetAllToDefaults();
            }
            totalContentHeight = curY;
        }
        private static void RemoveDefFromUse(ThingDef def) {
            // Remove from DefDatabase:
            //   equivalent to DefDatabase<DesignationCategoryDef>.Remove(def);
            //                  that's a private method, of course ^^^^^^
            //   #reflection #magic
            typeof(DefDatabase<>).MakeGenericType(new Type[] {typeof(ThingDef)})
                    .GetMethod("Remove", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                    .Invoke (null, new object [] { def });

            DefDatabase<ThingDef>.AllDefsListForReading.Remove(def);
            ThingDef tester=DefDatabase<ThingDef>.GetNamed(def.defName, false);
            if (tester != null) Log.Error("Tried to remove "+def.defName+" from DefDatabase, but it's stil there???");
            // remove from architect menu
            if (def.designationCategory != null) {
                Utils.Mess(Utils.DBF.Settings, "  removing "+def+" from designation category "+def.designationCategory);
                def.designationCategory.AllResolvedDesignators.RemoveAll(x=>((x is Designator_Build) &&
                                                                             ((Designator_Build)x).PlacingDef==def));
            }
            return;
        }
        private static void ReturnDefToUse(ThingDef def) {
            Utils.Mess(Utils.DBF.Settings, "Returning def "+def+" to use.");
            // Def database
            DefDatabase<ThingDef>.AllDefsListForReading.Add(def);
            // restore to architect menu:
            if (def.designationCategory != null) {
                def.designationCategory.AllResolvedDesignators.Add(new Designator_Build(def));
            }
        }
  ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private class Dialog_DSU_Settings : Window {
            public Dialog_DSU_Settings(ThingDef def) {
                this.forcePause = true;
                this.doCloseX = true;
                this.doCloseButton = false;
                this.closeOnClickedOutside = true;
                this.absorbInputAroundWindow = true;
                this.def=def;

                if (tracker.HasDefaultValueFor(def.defName, "filter")) {
                    this.useCustomThingFilter=true;
                }

                SetTempVars();
            }
            private void SetTempVars() {
                tmpLabel=def.label;
                tmpMaxNumStacks=def.GetCompProperties<Properties>().maxNumberStacks;
                tmpMaxTotalMass=def.GetCompProperties<Properties>().maxTotalMass;
                tmpMaxMassStoredItem=def.GetCompProperties<Properties>().maxMassOfStoredItem;
                tmpShowContents=def.GetCompProperties<Properties>().showContents;
                tmpStoragePriority=def.building.defaultStorageSettings.Priority;
                tmpOverlayType=def.GetCompProperties<Properties>().overlayType;
            }

            private void SetTempVarsToDefaults() {
                SetTempVars();
                tmpLabel=tracker.GetDefaultValue(def.defName, "label", tmpLabel);
                tmpMaxNumStacks=tracker.GetDefaultValue(def.defName, "maxNumStacks", tmpMaxNumStacks);
                tmpMaxTotalMass=tracker.GetDefaultValue(def.defName, "maxTotalMass", tmpMaxTotalMass);
                tmpMaxMassStoredItem=tracker.GetDefaultValue(def.defName, "maxMassStoredItem", tmpMaxMassStoredItem);
                tmpShowContents=tracker.GetDefaultValue(def.defName, "showContents", tmpShowContents);
                tmpStoragePriority=tracker.GetDefaultValue(def.defName, "storagePriority", tmpStoragePriority);
                tmpOverlayType=tracker.GetDefaultValue(def.defName, "overlayType", tmpOverlayType);
                useCustomThingFilter=false;
                customThingFilter=null;
            }
            private bool AreTempVarsDefaults() {
                var cp=def.GetCompProperties<LWM.DeepStorage.Properties>();
                if (tmpLabel!=def.label) return false;
                if (tmpMaxMassStoredItem!=cp.maxMassOfStoredItem ||
                    tmpMaxNumStacks!=cp.maxNumberStacks ||
                    tmpMaxTotalMass!=cp.maxTotalMass ||
                    tmpOverlayType!=cp.overlayType ||
                    tmpShowContents!=cp.showContents
                    ) return false;
                if (tmpStoragePriority!=def.building.defaultStorageSettings.Priority) return false;
                if (useCustomThingFilter) return false;
                return true;
            }

            public override Vector2 InitialSize
            {
                get
                {
                    return new Vector2(900f, 700f);
                }
            }

            public override void DoWindowContents(Rect inRect) // For a specific DSU
            {
                // for the record, Listing_Standards kind of suck. Convenient enough, but no flexibility
                // TODO when I'm bored: switch to manual, add red background for disabled units
                // Bonus problem with Listing_Standard: nested scrolling windows do not work
                //   well - specifically the ThingFilter UI insisde a Listing_Standard
                // We are able to get around that problem by having our ScrollView be outside
                //   the Listing_S... (instead of using the L_S's .ScrollView) and having the
                //   ThingFilter's UI after the L_S ends.

                // First: Set up a ScrollView:
                // outer window (with room for buttons at bottom:
                Rect s = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - CloseButSize.y - 5f);
                // inner window that has entire content:
                y = y + 200; // this is to ensure the Listing_Standard does not run out of height,
                //                and can fully display everything - giving a proper length 'y' at
                //                its .End() call.
                //          Worst case scenario: y starts as 0, and the L_S gets to a CurHeight of
                //            200, and then updates at 400 the next time.  Because of the way RW's
                //            windows work, this will rapidly converge on a large enough value.
                Rect inner = new Rect(0, 0, s.width - 20, this.y);
                Widgets.BeginScrollView(s, ref DSUScrollPosition, inner);
                // We cannot do the scrollview inside the L_S:
                // l.BeginScrollView(s, ref DSUScrollPosition, ref v); // Does not allow filter UI
                var l = new Listing_Standard();
                l.Begin(inner);
                l.Label(def.label);
                l.GapLine();
                // Much TODO, so wow:
                tmpLabel=l.TextEntryLabeled("LWMDSpDSUlabel".Translate(), tmpLabel);
                string tmpstring=null;
                l.TextFieldNumericLabeled("LWM_DS_maxNumStacks".Translate().CapitalizeFirst()+" "
                                          +"LWM_DS_Default".Translate(tracker.GetDefaultValue(def.defName, "maxNumStacks",
                                                                                              tmpMaxNumStacks)),
                                          ref tmpMaxNumStacks, ref tmpstring,0);
                tmpstring=null;
                l.TextFieldNumericLabeled<float>("LWM_DS_maxTotalMass".Translate().CapitalizeFirst()+" "+
                                                 "LWM_DS_Default".Translate(tracker.GetDefaultValue(def.defName,
                                                                            "maxTotalMass", tmpMaxTotalMass).ToString()),
                                                 ref tmpMaxTotalMass, ref tmpstring,0f);
                tmpstring=null;
                l.TextFieldNumericLabeled<float>("LWM_DS_maxMassOfStoredItem".Translate().CapitalizeFirst()+" "+
                                                 "LWM_DS_Default".Translate(tracker.GetDefaultValue(def.defName,
                                                             "maxMassStoredItem", tmpMaxMassStoredItem).ToString()),
                                                 ref tmpMaxMassStoredItem, ref tmpstring,0f);
                l.CheckboxLabeled("LWMDSpDSUshowContents".Translate(), ref tmpShowContents);
                l.GapLine();
                l.EnumRadioButton(ref tmpOverlayType, "LWMDSpDSUoverlay".Translate());
                l.GapLine();
                l.EnumRadioButton(ref tmpStoragePriority, "LWMDSpDSUstoragePriority".Translate());
                l.GapLine();
                l.CheckboxLabeled("LWMDSpDSUchangeFilterQ".Translate(), ref useCustomThingFilter,
                                  "LWMDSpDSUchangeFilterQDesc".Translate());
                y = l.CurHeight;
                l.End();
                if (useCustomThingFilter) {
                    if (customThingFilter==null) {
                        customThingFilter=new ThingFilter();
                        customThingFilter.CopyAllowancesFrom(def.building.fixedStorageSettings.filter);
                        Utils.Mess(Utils.DBF.Settings,"Created new filter for "+def.defName+": "+customThingFilter);
//                        Log.Error("Old filter has: "+def.building.fixedStorageSettings.filter.AllowedDefCount);
//                        Log.Warning("New filter has: "+customThingFilter.AllowedDefCount);
                    }
                    // Since this is outside the L_S, we make our own rectangle and use it:
                    //   Nope: Rect r=l.GetRect(CustomThingFilterHeight); // this fails
                    Rect r = new Rect(20, y, (inner.width - 40)*3/4, CustomThingFilterHeight);
                    y += CustomThingFilterHeight;
                    ThingFilterUI.DoThingFilterConfigWindow(r, thingFilterState, customThingFilter);
                } else { // not using custom thing filter:
                    if (customThingFilter!=null || tracker.HasDefaultValueFor(this.def.defName, "filter")) {
                        customThingFilter=null;
                        if (tracker.HasDefaultValueFor(this.def.defName, "filter")) {
                            Utils.Mess(Utils.DBF.Settings, "  Removing filter for "+def.defName);
                            def.building.fixedStorageSettings.filter=(ThingFilter)tracker
                                .GetDefaultValue<ThingFilter>(def.defName, "filter", null);
                            tracker.Remove(def.defName, "filter");
                        }
                    }
                }

                // This fails: l.EndScrollView(ref v);
                Widgets.EndScrollView();

                // Cancel button
                var closeRect = new Rect(inRect.width-CloseButSize.x, inRect.height-CloseButSize.y,CloseButSize.x,CloseButSize.y);
                if (Widgets.ButtonText(closeRect, "CancelButton".Translate())) {
                    Utils.Mess(Utils.DBF.Settings, "Cancel button selected - no changes made");
                    Close();
                }
                // Accept button - with accompanying logic
                closeRect = new Rect(inRect.width-(2*CloseButSize.x+5f), inRect.height-CloseButSize.y,CloseButSize.x,CloseButSize.y);
                if (Widgets.ButtonText(closeRect, "AcceptButton".Translate())) {
                    LWM.DeepStorage.Properties props=def.GetCompProperties<Properties>();
                    GUI.FocusControl(null); // unfocus, so that a focused text field may commit its value
                    Utils.Warn(Utils.DBF.Settings, "\"Accept\" button selected: changing values for "+def.defName);
                    tracker.UpdateToNewValue(def.defName, "label", tmpLabel, ref def.label);
                    tracker.UpdateToNewValue(def.defName,
                               "maxNumStacks", tmpMaxNumStacks, ref props.maxNumberStacks);
                    tracker.UpdateToNewValue(def.defName,
                               "maxTotalMass", tmpMaxTotalMass, ref props.maxTotalMass);
                    tracker.UpdateToNewValue(def.defName,
                               "maxMassStoredItem", tmpMaxMassStoredItem, ref props.maxMassOfStoredItem);
                    tracker.UpdateToNewValue(def.defName,
                               "showContents", tmpShowContents, ref props.showContents);
                    tracker.UpdateToNewValue(def.defName,
                               "overlayType", tmpOverlayType, ref props.overlayType);
                    StoragePriority tmpSP=def.building.defaultStorageSettings.Priority; // hard to access private field directly
                    tracker.UpdateToNewValue(def.defName, "storagePriority", tmpStoragePriority, ref tmpSP);
                    def.building.defaultStorageSettings.Priority=tmpSP;
                    if (useCustomThingFilter) { // danger ahead - automatically use it, even if stupidly set up
                        if (!tracker.HasDefaultValueFor(def.defName, "filter")) {
                            Utils.Mess(Utils.DBF.Settings, "Creating default filter record for item "+def.defName);
                            tracker.AddDefaultValue(def.defName, "filter", def.building.fixedStorageSettings.filter);
                        }
                        def.building.fixedStorageSettings.filter=customThingFilter;
                    } else {
                        // restore default filter:
                        if (tracker.HasDefaultValueFor(def.defName, "filter")) {
                            // we need to remove it
                            Utils.Mess(Utils.DBF.Settings, "Removing default filter record for item "+def.defName);
                            def.building.fixedStorageSettings.filter=(ThingFilter)tracker
                                .GetDefaultValue<ThingFilter>(def.defName, "filter", null);
                            tracker.Remove(def.defName, "filter");
                        }
                    }
                    Close();
                }
                // Reset to Defaults
                closeRect = new Rect(inRect.width-(4*CloseButSize.x+10f), inRect.height-CloseButSize.y,2*CloseButSize.x,CloseButSize.y);
                if (!AreTempVarsDefaults() && Widgets.ButtonText(closeRect, "ResetBinding".Translate())) {
                    SetTempVarsToDefaults();
                }
            }

            public override void PreOpen() {
                // Per Dialog_BillsConfig
                base.PreOpen();
                thingFilterState.quickSearch.Reset();
            }

            DefChangeTracker tracker = Settings.defTracker;
            ThingDef def;
            string tmpLabel;
            int tmpMaxNumStacks;
            float tmpMaxTotalMass;
            float tmpMaxMassStoredItem;
            bool tmpShowContents;
            LWM.DeepStorage.GuiOverlayType tmpOverlayType;
            StoragePriority tmpStoragePriority;

            bool useCustomThingFilter=false;
            ThingFilter customThingFilter=null;
            ThingFilterUI.UIState thingFilterState = new ThingFilterUI.UIState();
            Vector2 DSUScrollPosition=new Vector2(0,0);
            float y=1000f;
            const float CustomThingFilterHeight=400f;
        }
        private static void ResetDSUToDefaults(string resetDefName) {
            object tmpObject;
            string defName=resetDefName;
            string prop;
            bool resetAll=(resetDefName==null || resetDefName=="");
            while ((resetAll && Settings.defTracker.GetFirstDefaultValue(out defName, out prop, out tmpObject))
                   || Settings.defTracker.GetFirstDefaultValueFor(defName, out prop, out tmpObject)) {
                Utils.Mess(Utils.DBF.Settings,"Resetting "+prop+" to default value for "+defName);
                var def=DefDatabase<ThingDef>.GetNamed(defName, false);
                if (def==null) {
                    ThingDef tmp = (ThingDef)Settings.defTracker.GetDefaultValue(defName, "def", def);
                    if (tmp!=null) {
                        def=tmp;
                        // We are resetting the def, so we need it back in the DefDatabase!
                        ReturnDefToUse(def);
                        Settings.defTracker.Remove(defName, "def");
                        if (prop=="def") continue;
                    } else {
                        //todo: put this error message it translate
                        Log.Warning("LWM.DeepStorage: Tried to change mod setting for "+defName+" but could not find def.\nClear your settings to remove this error.");
                        Settings.defTracker.Remove(defName, prop);
                        continue;
                    }
                }
                if (prop=="label") {
                    def.label=(string)(tmpObject);
                } else if (prop=="maxNumStacks") {
                    def.GetCompProperties<Properties>().maxNumberStacks=(int)(tmpObject);
                } else if (prop=="maxTotalMass") {
                    def.GetCompProperties<Properties>().maxTotalMass=(float)(tmpObject);
                } else if (prop=="maxMassStoredItem") {
                    def.GetCompProperties<Properties>().maxMassOfStoredItem=(float)(tmpObject);
                } else if (prop=="showContents") {
                    def.GetCompProperties<Properties>().showContents=(bool)(tmpObject);
                } else if (prop=="storagePriority") {
                    def.building.defaultStorageSettings.Priority=(StoragePriority)(tmpObject);
                } else if (prop=="overlayType") {
                    def.GetCompProperties<Properties>().overlayType=(LWM.DeepStorage.GuiOverlayType)(tmpObject);
                } else if (prop=="filter") {
                    def.building.fixedStorageSettings.filter=(ThingFilter)(tmpObject);
                } else if (prop=="def") {
                    // Def was marked for removal but hasn't been removed yet
                    Utils.Mess(Utils.DBF.Settings, "Removing "+defName+" from list of defs to remove.");
                    ReturnDefToUse(def);
                } else {
                    Log.Error("LWM.DeepStorage: FAILED TO RESET OPTION TO DEFAULT: "+defName+", "+prop);
                }
                Settings.defTracker.Remove(defName, prop);
            } // end while loop, defSettings.DefTracker didn't have anything else
            // done resetting!
        }

        private static void ResetAllToDefaults() {
            ResetDSUToDefaults(null);
        }

        public static void ExposeDSUSettings(IEnumerable<ThingDef> units) {
            // note: make our own list in case we modify DefDatabase/etc from here
            if (units==null) { Log.Warning("Passed null units"); return; }
            if (Settings.defTracker==null) {Log.Error("DefChangeTracker is null"); return;}
            foreach (ThingDef u in units.ToList()) {
                Utils.Warn(Utils.DBF.Settings, "Expose DSU Settings: "+u.defName+" ("+Scribe.mode+")");
                string defName=u.defName;
                Settings.defTracker.ExposeSetting<string>(defName, "label",ref u.label);
                Settings.defTracker.ExposeSetting(defName, "maxNumStacks", ref u.GetCompProperties<Properties>().maxNumberStacks);
                Settings.defTracker.ExposeSetting(defName, "maxTotalMass", ref u.GetCompProperties<Properties>().maxTotalMass);
                Settings.defTracker.ExposeSetting(defName, "maxMassStoredItem", ref u.GetCompProperties<Properties>().maxMassOfStoredItem);
                Settings.defTracker.ExposeSetting(defName, "showContents", ref u.GetCompProperties<Properties>().showContents);
                Settings.defTracker.ExposeSetting(defName, "overlayType", ref u.GetCompProperties<Properties>().overlayType);
                StoragePriority tmpSP=u.building.defaultStorageSettings.Priority; // hard to access private field directly
                Settings.defTracker.ExposeSetting<StoragePriority>(defName, "storagePriority", ref tmpSP);
                u.building.defaultStorageSettings.Priority=tmpSP;
                // If fixedStorageSettings is null, it's because it can store anything. We don't change that:
                if (u.building?.fixedStorageSettings != null)
                    Settings.defTracker.ExposeSettingDeep(defName, "filter", ref u.building.fixedStorageSettings.filter);
                //Utils.Mess(Utils.DBF.Settings, "  Basics exposed.");
                //////////////////////// disabling defs //////////////////////////
                if (Scribe.mode == LoadSaveMode.LoadingVars) {
                    // Check if this unit has been disabled:
                    bool disabled=false;
                    Scribe_Values.Look(ref disabled, "DSU_"+defName+"_disabled", false);
                    if (disabled) {
//todo                        defaultDSUValues["outOfOrder"]=true;
                        Utils.Mess(Utils.DBF.Settings, "Startup: disabling unit "+u.defName);
                        Settings.defTracker.AddDefaultValue(defName, "def", u);
                        RemoveDefFromUse(u);
                    }
                } else if (Scribe.mode == LoadSaveMode.Saving) {
                    if (Settings.defTracker.HasDefaultValueFor(defName, "def")) {
                        bool disabled=true;
                        Scribe_Values.Look(ref disabled, "DSU_"+defName+"_disabled", false);
                        Utils.Mess(Utils.DBF.Settings, "Saving disabled unit name "+u.defName);
                    }
                }
            } // end units loop
        }

        private float totalContentHeight=1000f;
        private Vector2 scrollPosition;

		private const float TopAreaHeight = 40f;
		private const float TopButtonHeight = 35f;
		private const float TopButtonWidth = 150f;
        private const float ScrollBarWidthMargin = 18f;
        private const float LabelHeight=22f;


        // Actual Logic objects:
        //   list of DSUs to be disabled on window close:
        HashSet<ThingDef> unitsToBeDisabled=null;
        // Helpful for typing:
        public DefChangeTracker tracker=Settings.defTracker;
    }

}
