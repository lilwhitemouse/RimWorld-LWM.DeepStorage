using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

/// <summary>
///   Two Dialog windows to allow whiny RimWorld players to change settings for the Deep Storage units
///     and the logic to make that happen.
///   Basic (old) idea:
///     on load, check if modifying units is turned on.  If so, once defs are loaded, do second
///     pass through the Setting's ExposeData() and this time, parse "DSU_LWM_defName_fieldName",
///     populate a dictionary with the default values, and change the defs on the fly.
///   New idea:
///     Only do the above if running non-steam verion of mod.  If running steam version of mod,
///     grab list of categories and defs for user to select (what's allowable, etc)
///
///   This file contains the two dialog windows, keeps the default settings, and handles making the def changes.
///   The call to re-read the storage settings is done in ModSettings.cs, and the call to ExposeDSUSettings is
///     done in ModSettings' ExposeData.
/// </summary>

namespace LWM.DeepStorage
{
    /****************************** The window that lists all the DSUs available: *******************/
//    [StaticConstructorOnStartup]
    public class Dialog_DS_Settings : Window {
        static Dialog_DS_Settings() {
//            Log.Message("LWM Settings: preparing Defs");
        }

        private SettingsPerDSUSaver settingsSaver;

        public Dialog_DS_Settings(SettingsPerDSUSaver sSaver) { // fuck the SS
			this.forcePause = true;
			this.doCloseX = true;
            this.doCloseButton = true;
			this.closeOnClickedOutside = true;
			this.absorbInputAroundWindow = true;
            this.settingsSaver=sSaver;
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
//            var scrollViewVisible = new Rect(0f, titleRect.height, contentRect.width, contentRect.height - titleRect.height);
            bool scrollBarVisible = totalContentHeight > contentRect.height;
            var scrollViewTotal = new Rect(0f, 0f, contentRect.width - (scrollBarVisible ? ScrollBarWidthMargin : 0), totalContentHeight);
            Widgets.DrawHighlight(contentRect);
            Widgets.BeginScrollView(contentRect, ref scrollPosition, scrollViewTotal);
            float curY = 0f;
            Rect r=new Rect(0,curY,scrollViewTotal.width, LabelHeight);

//            r=new Rect(0,curY,scrollViewTotal.width, LabelHeight);
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
            GenUI.SetLabelAlign(TextAnchor.MiddleLeft);
            var bg=ContentFinder<Texture2D>.Get("UI/Widgets/ButtonBG", true);
            var bgmouseover=ContentFinder<Texture2D>.Get("UI/Widgets/ButtonBGMouseover", true);
            var bgclick=ContentFinder<Texture2D>.Get("UI/Widgets/ButtonBGClick", true);
            foreach (ThingDef u in Settings.allDeepStorageUnits) { // Todo: just recalculate the damn thing here

                r=new Rect(5f, curY, (scrollViewTotal.width)*2/3-7f, LabelHeight);
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
                    Find.WindowStack.Add(new Dialog_DSU_Settings(settingsSaver, u));
                }
                // Reset button:
                r=new Rect((scrollViewTotal.width)*2/3+2f,curY, (scrollViewTotal.width)/3-7f, LabelHeight);
                if (settingsSaver.IsDSUChanged(u) && Widgets.ButtonText(r, "ResetBinding".Translate())) {
                    settingsSaver.ResetDSUToDefaults(u.defName);
                }
                curY+=LabelHeight+2f;
            }
            GenUI.ResetLabelAlign();
            // end buttons

            Widgets.EndScrollView();
            r=new Rect(10f, inRect.height-CloseButSize.y-5f, inRect.width/3, CloseButSize.y);
            if (settingsSaver.AnythingChanged() && Widgets.ButtonText(r, "LWM.ResetAllToDefault".Translate())) {
                Utils.Mess(Utils.DBF.Settings, "Resetting all per-building storage settings to default:");
                settingsSaver.ResetAllToDefaults();
            }
            totalContentHeight = curY;
        }
        /************************  Per DSU window ****************************/
        private class Dialog_DSU_Settings : Window {
            public Dialog_DSU_Settings(SettingsPerDSUSaver settingsSaver, ThingDef def) {
                this.forcePause = true;
                this.doCloseX = true;
                this.doCloseButton = false;
                this.closeOnClickedOutside = true;
                this.absorbInputAroundWindow = true;

                this.def=def;
                this.origFilter=settingsSaver.GetOriginalFilter(def);
                this.settingsSaver=settingsSaver;
                SetTempVars();
                possibleCategories=DefDatabase<ThingCategoryDef>.AllDefsListForReading;
            }
            SettingsPerDSUSaver settingsSaver;
            List<ThingCategoryDef> possibleCategories;
            private void SetTempVars() { // from whatever the comp property has
                tmpLabel=def.label;
                tmpMaxNumStacks=def.GetCompProperties<Properties>().maxNumberStacks;
                //TODO: minNumberStacks!!!!!
                tmpMaxTotalMass=def.GetCompProperties<Properties>().maxTotalMass;
                tmpMaxMassStoredItem=def.GetCompProperties<Properties>().maxMassOfStoredItem;
                tmpShowContents=def.GetCompProperties<Properties>().showContents;
                tmpStoragePriority=def.building.defaultStorageSettings.Priority;
                tmpOverlayType=def.GetCompProperties<Properties>().overlayType;
                //todo:  meh:
                tmpAdditionalCategories=new List<string>(settingsSaver.GetAdditionalCategoryNames(def));
/*                if (defsUsingCustomFilter!=null && defsUsingCustomFilter.Contains(def.defName)) {
                    this.useCustomThingFilter=true; // meh
                }*/
            }

            private void SetTempVarsToDefaults() {
                //SetTempVars();
                tmpLabel=(string)settingsSaver.GetDefaultValue(def, "label");
                tmpMaxNumStacks=(int)settingsSaver.GetDefaultValue(def, "maxNumStacks");
                tmpMaxTotalMass=(float)settingsSaver.GetDefaultValue(def, "maxTotalMass");
                tmpMaxMassStoredItem=(float)settingsSaver.GetDefaultValue(def, "maxMassStoredItem");
                tmpShowContents=(bool)settingsSaver.GetDefaultValue(def, "showContents");
                tmpStoragePriority=(StoragePriority)settingsSaver.GetDefaultValue(def, "storagePriority");
                tmpOverlayType=(DeepStorage.GuiOverlayType)settingsSaver.GetDefaultValue(def, "overlayType");
                //tmp=()settingsSaver.GetDefaultValue(def, "");
                tmpAdditionalCategories=null;
                tmpAdditionalDefs=null;
                useCustomThingFilter=false;//todo: probably not use this, but whatevs
                customThingFilter=null;
                //HelpSetTempVarToDefault<>(ref tmp, "");
            }
            private bool AreTempVarsDefaults() { //TODO: WTF?  Is this right?  "Defaults"???  This is NOT right
                if (tmpLabel!=(string)settingsSaver.GetDefaultValue(def, "label") ||
                    tmpMaxNumStacks!=(int)settingsSaver.GetDefaultValue(def, "maxNumStacks") ||
                    tmpMaxTotalMass != (float)settingsSaver.GetDefaultValue(def, "maxTotalMass") ||
                    tmpMaxMassStoredItem != (float)settingsSaver.GetDefaultValue(def, "maxMassStoredItem") ||
                    tmpShowContents != (bool)settingsSaver.GetDefaultValue(def, "showContents") ||
                    tmpStoragePriority != (StoragePriority)settingsSaver.GetDefaultValue(def, "storagePriority") ||
                    tmpOverlayType != (DeepStorage.GuiOverlayType)settingsSaver.GetDefaultValue(def, "overlayType"))
                    //tmp != ()settingsSaver.GetDefaultValue(def, "") ||
                    //TODO: add minNumStacks
                    return false;
                // TODO: NOT correct:
                //if (settingsSaver.IsFilterChanged(def)) return false;
                // This isn't quite correct, but whatevs - it's close enough.
                if (!tmpAdditionalCategories.NullOrEmpty()) return false;
                if (useCustomThingFilter) return false;
                return true;
            }
/*            private void HelpSetTempVarToDefault<T>(ref T v, string keylet) { // MEH.
                string key="DSU_"+def.defName+"_"+keylet;
                if (defaultDSUValues.ContainsKey(key)) {
                    Utils.Mess(Utils.DBF.Settings, "Resetting "+def.defName+"'s "+keylet+" to default "+defaultDSUValues[key]);
                    v=(T)defaultDSUValues[key];
                }
            }*/
            public override Vector2 InitialSize
            {
                get
                {
                    return new Vector2(900f, 700f);
                }
            }

            public override void DoWindowContents(Rect inRect) // For a specific DSU
            {
                var l = new Listing_Standard();
//                l.Begin(new Rect(inRect.x, inRect.y, inRect.width, inRect.height-CloseButSize.y-5f));
                Rect screenRect=new Rect(inRect.x+5f, inRect.y+5f, inRect.width-10f, inRect.height-CloseButSize.y-15f);
  //              Rect v=new Rect(inRect.x, inRect.y, inRect.width-20f, inRect.height-CloseButSize.y-5f);
  //              if (useCustomThingFilter) v.height+=300f;
                l.BeginScrollView(screenRect, ref DSUScrollPosition, ref DSUViewRect);
//                l.BeginScrollView(
//                l.BeginScrollView(Rect rect, ref Vector2 scrollPosition, ref Rect viewRect)
                l.Label(def.label);
                l.GapLine();
                // Much TODO, so wow:
                tmpLabel=l.TextEntryLabeled("LWMDSpDSUlabel".Translate(), tmpLabel);
                string tmpstring=null;
                //TODO: redo, include defaults:
                l.TextFieldNumericLabeled("LWM_DS_maxNumStacks".Translate().CapitalizeFirst()+" "
                                          +"LWM_DS_Default".Translate(tmpMaxNumStacks),
                                          ref tmpMaxNumStacks, ref tmpstring,0);
//                l.TextFieldNumericLabeled("Maximum Number of Stacks per Cell", ref tmpMaxNumStacks, ref tmpstring,0);
                tmpstring=null;
//                l.TextFieldNumericLabeled<float>("Maximum Total Mass per Cell", ref tmpMaxTotalMass, ref tmpstring,0f);
                l.TextFieldNumericLabeled<float>("LWM_DS_maxTotalMass".Translate().CapitalizeFirst()+" "+
                                                 "LWM_DS_Default".Translate(tmpMaxTotalMass),
                                                 ref tmpMaxTotalMass, ref tmpstring,0f);
                tmpstring=null;
//                l.TextFieldNumericLabeled<float>("Maximum Mass of any Stored Item", ref tmpMaxMassStoredItem, ref tmpstring,0f);
                l.TextFieldNumericLabeled<float>("LWM_DS_maxMassOfStoredItem".Translate().CapitalizeFirst()+" "+
                                                 "LWM_DS_Default".Translate(tmpMaxMassStoredItem),
                                                 ref tmpMaxMassStoredItem, ref tmpstring,0f);
                l.CheckboxLabeled("LWMDSpDSUshowContents".Translate(), ref tmpShowContents);
                l.GapLine();
                l.EnumRadioButton(ref tmpOverlayType, "LWMDSpDSUoverlay".Translate());
                l.GapLine();
                l.EnumRadioButton(ref tmpStoragePriority, "LWMDSpDSUstoragePriority".Translate());
                l.GapLine();
                // New attempt at per-storage-unit buildings.
                ////////////////// additional categories ///////////////////
                //todo: make this a tidy scrollview inside a new rect?
                l.Label("Select any ADDITIONAL categories you wish to store here", -1, null);
                // todo: put this behind if usecustomthingfilter
                foreach (ThingCategoryDef tcd in DefDatabase<ThingCategoryDef>.AllDefs) {
                    bool tmpBool=false;
                    if (tmpAdditionalCategories != null
                        && tmpAdditionalCategories.Contains(tcd.defName)) {
                        tmpBool=true;
                    }
                    bool tmpBoolOrig=tmpBool;
                    l.CheckboxLabeled("   "+tcd.LabelCap, ref tmpBool);
                    // TODO: check if filter already has it....
                    if (tmpBool) {
                        if (tmpAdditionalCategories==null) tmpAdditionalCategories=new List<string>();
                        if (!tmpAdditionalCategories.Contains(tcd.defName)) tmpAdditionalCategories.Add(tcd.defName);
                    } else {
                        if (tmpAdditionalCategories!=null && tmpAdditionalCategories.Contains(tcd.defName))
                            tmpAdditionalCategories.Remove(tcd.defName);
                    }
                    if (tmpBoolOrig!=tmpBool && useCustomThingFilter) RebuildFilter();
                }
                //////////////// entire new filter ////////////////
                l.GapLine();
                l.CheckboxLabeled("LWMDSpDSUchangeFilterQ".Translate(), ref useCustomThingFilter,
                                  "LWMDSpDSUchangeFilterQDesc".Translate());
                if (useCustomThingFilter) {
                    if (customThingFilter==null) {
                        RebuildFilter();
/*                        customThingFilter=new ThingFilter();
                        customThingFilter.CopyAllowancesFrom(def.building.fixedStorageSettings.filter);
                        Utils.Mess(Utils.DBF.Settings,"Created new filter for "+def.defName+": "+customThingFilter);
                        */
//                        Log.Error("Old filter has: "+def.building.fixedStorageSettings.filter.AllowedDefCount);
//                        Log.Warning("New filter has: "+customThingFilter.AllowedDefCount);
                    }
                    // Make new filter UI:
                    Rect r=l.GetRect(300);
                    r.width*=2f/3f;
                    r.x+=10f; //indent a little, etc.
                    ThingFilterUI.DoThingFilterConfigWindow(r, ref thingFilterScrollPosition, customThingFilter);
                } else { // not using custom thing filter:
/*                    if (customThingFilter!=null || defaultDSUValues.ContainsKey("DSU_"+def.defName+"_filter")) {
                        customThingFilter=null;
                        //todo: This is all wrong.
                        def.building.fixedStorageSettings.filter=(ThingFilter)defaultDSUValues["DSU_"+def.defName+"_filter"];
                        if (tmpAdditionalCategories.NullOrEmpty() && defaultDSUValues.ContainsKey("DSU_"+def.defName+"_filter")) {
                            Utils.Mess(Utils.DBF.Settings, "  Removing filter for "+def.defName);
                            defaultDSUValues.Remove("DSU_"+def.defName+"_filter");
                        }
                    }*/
                }
//                l.End();
                l.EndScrollView(ref DSUViewRect);

                // Cancel button
                var closeRect = new Rect(inRect.width-CloseButSize.x, inRect.height-CloseButSize.y,CloseButSize.x,CloseButSize.y);
                if (Widgets.ButtonText(closeRect, "CancelButton".Translate())) {
                    Utils.Mess(Utils.DBF.Settings, "Cancel button selected - no changes made");
                    Close();
                }
                // Accept button - with accompanying logic
                closeRect = new Rect(inRect.width-(2*CloseButSize.x+5f), inRect.height-CloseButSize.y,CloseButSize.x,CloseButSize.y);
                if (Widgets.ButtonText(closeRect, "AcceptButton".Translate())) {
                    GUI.FocusControl(null); // unfocus, so that a focused text field may commit its value
                    Utils.Warn(Utils.DBF.Settings, "\"Accept\" button selected: changing values for "+def.defName);
                    UpdateSettings();
                    Close();
                }
                // Reset to Defaults
                closeRect = new Rect(inRect.width-(4*CloseButSize.x+10f), inRect.height-CloseButSize.y,2*CloseButSize.x,CloseButSize.y);
                if (!AreTempVarsDefaults() && Widgets.ButtonText(closeRect, "ResetBinding".Translate())) {
                    SetTempVarsToDefaults();
                    //ResetDSUToDefaults(def.defName);
                    //SetTempVars();
                }
            }

            void RebuildFilter() {
                customThingFilter=new ThingFilter();
                customThingFilter.CopyAllowancesFrom((ThingFilter)settingsSaver.GetDefaultValue(def, "filter"));
                if (tmpAdditionalCategories!=null) {
                    foreach (var catdefname in tmpAdditionalCategories) {
                        ThingCategoryDef tcd=DefDatabase<ThingCategoryDef>.GetNamed(catdefname, false);
                        if (tcd==null) {
                            Log.Warning("LWM.DeepStorage: tried to create filter with nonexistant category def of "+catdefname);
                            tmpAdditionalCategories.Remove(catdefname);
                            settingsSaver.RemoveCategory(def, catdefname);
                        } else {
                            customThingFilter.SetAllow(tcd, true);
                        }
                    }
                }
                if (tmpAdditionalDefs!=null) {
                    foreach (var tdefname in tmpAdditionalDefs) {
                        ThingDef td=DefDatabase<ThingDef>.GetNamed(tdefname, false);
                        if (td==null) {
                            Log.Warning("LWM.DeepStorage: tried to create filter with nonexistant thing def of "+tdefname);
                            tmpAdditionalCategories.Remove(tdefname);
                            settingsSaver.RemoveDef(def, tdefname);
                        } else {
                            customThingFilter.SetAllow(td, true);
                        }
                    }
                }
            } // RebuildFilter

            void UpdateSettings() {
                settingsSaver.Update(def, tmpLabel, tmpMaxNumStacks, tmpMaxTotalMass,
                                     tmpMaxMassStoredItem, tmpShowContents, tmpOverlayType,
                                     tmpStoragePriority,
                                     tmpAdditionalCategories,
                                     useCustomThingFilter?customThingFilter:null);

                /*
                    TestAndUpdate(def, "label", tmpLabel, ref def.label);
                    TestAndUpdate(def, "maxNumStacks", tmpMaxNumStacks, ref def.GetCompProperties<Properties>().maxNumberStacks);
                    TestAndUpdate(def, "maxTotalMass", tmpMaxTotalMass, ref def.GetCompProperties<Properties>().maxTotalMass);
                    TestAndUpdate(def, "maxMassStoredItem", tmpMaxMassStoredItem, ref def.GetCompProperties<Properties>().maxMassOfStoredItem);
                    TestAndUpdate(def, "showContents", tmpShowContents, ref def.GetCompProperties<Properties>().showContents);
                    TestAndUpdate(def, "overlayType", tmpOverlayType, ref def.GetCompProperties<Properties>().overlayType);
                    StoragePriority tmpSP=def.building.defaultStorageSettings.Priority; // hard to access private field directly
                    TestAndUpdate(def, "storagePriority", tmpStoragePriority, ref tmpSP);
                    def.building.defaultStorageSettings.Priority=tmpSP;
                    if (useCustomThingFilter || !tmpAdditionalCategories.NullOrEmpty()) {
                        if (!defaultDSUValues.ContainsKey("DSU_"+def.defName+"_filter")) {
                            Utils.Mess(Utils.DBF.Settings, "Creating default filter record for item "+def.defName);
                            defaultDSUValues["DSU_"+def.defName+"_filter"]=def.building.fixedStorageSettings.filter;
                        }
                    } else { // nothing adding to filter:
                        if (defaultDSUValues.ContainsKey("DSU_"+def.defName+"_filter")) {
                            // we need to remove it
                            Utils.Mess(Utils.DBF.Settings, "Removing default filter record for item "+def.defName);
                            def.building.fixedStorageSettings.filter=(ThingFilter)defaultDSUValues["DSU_"+def.defName+"_filter"];
                            defaultDSUValues.Remove("DSU_"+def.defName+"_filter");
                        }
                    }
                    if (useCustomThingFilter) {
                        def.building.fixedStorageSettings.filter=customThingFilter;
                        if (defsUsingCustomFilter==null) defsUsingCustomFilter=new List<string>();
                        if (!defsUsingCustomFilter.Contains(def.defName)) defsUsingCustomFilter.Add(def.defName);
                    } else {
                        if (defsUsingCustomFilter!=null) defsUsingCustomFilter.Remove(def.defName);
                    }
                    if (!tmpAdditionalCategories.NullOrEmpty()) {
                        if (!useCustomThingFilter) {
                            def.building.fixedStorageSettings.filter=new ThingFilter();
                            def.building.fixedStorageSettings.filter
                                .CopyAllowancesFrom((ThingFilter)defaultDSUValues["DSU_"+def.defName+"_filter"]);
                        }
                        //todo: logic to remove categories?
                        foreach (string catDefName in tmpAdditionalCategories) {
                            def.building.fixedStorageSettings.filter
                                .SetAllow(DefDatabase<ThingCategoryDef>.GetNamed(catDefName), true);
                        }
                        if (additionalCategories==null) additionalCategories=new Dictionary<string,List<string>>();
                        additionalCategories[def.defName]=new List<string>(tmpAdditionalCategories);
                    }
                    */

            }

            ThingDef def;
            ThingFilter origFilter;
            string tmpLabel;
            int tmpMaxNumStacks;
            float tmpMaxTotalMass;
            float tmpMaxMassStoredItem;
            bool tmpShowContents;
            LWM.DeepStorage.GuiOverlayType tmpOverlayType;
            StoragePriority tmpStoragePriority;
            List<string> tmpAdditionalCategories=null;
            List<string> tmpAdditionalDefs=null;

            bool useCustomThingFilter=false;
            ThingFilter customThingFilter=null;
            Vector2 thingFilterScrollPosition=new Vector2(0,0);
            Rect DSUViewRect=new Rect(0,0,100f,1000f);
            Vector2 DSUScrollPosition=new Vector2(0,0);
        }



        private float totalContentHeight=1000f;
        private Vector2 scrollPosition;
//        private static Vector2 scrollPosition;

		private const float TopAreaHeight = 40f;
		private const float TopButtonHeight = 35f;
		private const float TopButtonWidth = 150f;
        private const float ScrollBarWidthMargin = 18f;
        private const float LabelHeight=22f;


    }

}
