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
        public Dialog_DS_Settings() {
			this.forcePause = true;
			this.doCloseX = true;
            this.doCloseButton = true;
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
            foreach (ThingDef u in Settings.allDeepStorageUnits) {

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
                    Find.WindowStack.Add(new Dialog_DSU_Settings(u));
                }
                // Reset button:
                r=new Rect((scrollViewTotal.width)*2/3+2f,curY, (scrollViewTotal.width)/3-7f, LabelHeight);
                if (IsDSUChanged(u) && Widgets.ButtonText(r, "ResetBinding".Translate())) {
                    ResetDSUToDefaults(u.defName);
                }
                curY+=LabelHeight+2f;
            }
            GenUI.ResetLabelAlign();
            // end buttons

            Widgets.EndScrollView();
            r=new Rect(10f, inRect.height-CloseButSize.y-5f, inRect.width/3, CloseButSize.y);
            if (defaultDSUValues.Count>0 && Widgets.ButtonText(r, "LWM.ResetAllToDefault".Translate())) {
                Utils.Mess(Utils.DBF.Settings, "Resetting all per-building storage settings to default:");
                ResetAllToDefaults();
            }
            totalContentHeight = curY;
        }
        /************************  Per DSU window ****************************/
        private class Dialog_DSU_Settings : Window {
            public Dialog_DSU_Settings(ThingDef def) {
                this.forcePause = true;
                this.doCloseX = true;
                this.doCloseButton = false;
                this.closeOnClickedOutside = true;
                this.absorbInputAroundWindow = true;
                this.def=def;
                if (defaultDSUValues.ContainsKey("DSU_"+def.defName+"_filter"))
                    this.origFilter=(ThingFilter)defaultDSUValues["DSU_"+def.defName+"_filter"];
                else
                    this.origFilter=def.building.defaultStorageSettings.filter;

                SetTempVars();
                possibleCategories=DefDatabase<ThingCategoryDef>.AllDefsListForReading;
            }
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
                if (additionalCategories!=null &&
                    additionalCategories.ContainsKey(def.defName)) {
                    Utils.Mess(Utils.DBF.Settings, "  additionalCategories contains def "+def.defName+", creating new list");
                    tmpAdditionalCategories=new List<string>(additionalCategories[def.defName]);
                } else {
                    Utils.Mess(Utils.DBF.Settings, "  additionalCategories does not contain def "+def.defName+", will be null");
                    tmpAdditionalCategories=null;
                }
                if (defsUsingCustomFilter!=null && defsUsingCustomFilter.Contains(def.defName)) {
                    this.useCustomThingFilter=true; // meh
                }
            }

            private void SetTempVarsToDefaults() {
                SetTempVars();
                string k="DSU_"+def.defName;
                //  if (defaultDSUValues.ContainsKey(k+"_label")) tmpLabel=(string)defaultDSUValues[k+"label"];
                HelpSetTempVarToDefault<string>(ref tmpLabel, "label");
                HelpSetTempVarToDefault<int>(ref tmpMaxNumStacks, "maxNumStacks");
                HelpSetTempVarToDefault<float>(ref tmpMaxTotalMass, "maxTotalMass");
                HelpSetTempVarToDefault<float>(ref tmpMaxMassStoredItem, "maxMassStoredItem");
                HelpSetTempVarToDefault<bool>(ref tmpShowContents, "showContents");
                HelpSetTempVarToDefault<StoragePriority>(ref tmpStoragePriority, "storagePriority");
                HelpSetTempVarToDefault<LWM.DeepStorage.GuiOverlayType>(ref tmpOverlayType, "overlayType");
                useCustomThingFilter=false;
                customThingFilter=null;
                tmpAdditionalCategories=null;
                //HelpSetTempVarToDefault<>(ref tmp, "");
            }
            private bool AreTempVarsDefaults() { //TODO: WTF?  Is this right?  "Defaults"???  This is NOT right
                var cp=def.GetCompProperties<LWM.DeepStorage.Properties>();
                string k="DSU_"+def.defName;
                object tmpO;
                if (tmpLabel != (defaultDSUValues.TryGetValue(k+"_label", out tmpO)?(string)tmpO:def.label)||
                    tmpMaxNumStacks != (defaultDSUValues.TryGetValue(k+"_maxNumStacks", out tmpO)?(int)tmpO:cp.maxNumberStacks)||
                    tmpMaxTotalMass != (defaultDSUValues.TryGetValue(k+"_maxTotalMass", out tmpO)?(float)tmpO:cp.maxTotalMass)||
                    tmpMaxMassStoredItem != (defaultDSUValues.TryGetValue(k+"_maxMassStoredItem", out tmpO)?(float)tmpO:cp.maxMassOfStoredItem)||
                    tmpShowContents != (defaultDSUValues.TryGetValue(k+"_showContents", out tmpO)?(bool)tmpO:cp.showContents)||
                    tmpStoragePriority != (defaultDSUValues.TryGetValue(k+"_storagePriority", out tmpO)?(StoragePriority)tmpO:def.building.defaultStorageSettings.Priority)||
                    tmpOverlayType != (defaultDSUValues.TryGetValue(k+"_overlayType", out tmpO)?(DeepStorage.GuiOverlayType)tmpO:cp.overlayType)
                    ) return false;
                if (useCustomThingFilter) return false;
                if (!tmpAdditionalCategories.NullOrEmpty()) return false;
                return true;
            }
            private void HelpSetTempVarToDefault<T>(ref T v, string keylet) { // MEH.
                string key="DSU_"+def.defName+"_"+keylet;
                if (defaultDSUValues.ContainsKey(key)) {
                    Utils.Mess(Utils.DBF.Settings, "Resetting "+def.defName+"'s "+keylet+" to default "+defaultDSUValues[key]);
                    v=(T)defaultDSUValues[key];
                }
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
                l.Label("Select any ADDITIONAL categories you wish to store here", -1, null);
                // todo: put this behind if usecustomthingfilter
                foreach (ThingCategoryDef d in DefDatabase<ThingCategoryDef>.AllDefs) {
                    bool tmpBool=false;
                    if (tmpAdditionalCategories != null
                        && tmpAdditionalCategories.Contains(d.defName)) {
                        tmpBool=true;
                    }
                    l.CheckboxLabeled("   "+d.LabelCap, ref tmpBool);
                    // TODO: check if filter already has it....
                    if (tmpBool) {
                        if (tmpAdditionalCategories==null) tmpAdditionalCategories=new List<string>();
                        if (!tmpAdditionalCategories.Contains(d.defName)) tmpAdditionalCategories.Add(d.defName);
                    } else {
                        if (tmpAdditionalCategories!=null && tmpAdditionalCategories.Contains(d.defName))
                            tmpAdditionalCategories.Remove(d.defName);
                    }
                }
                //////////////// entire new filter ////////////////
                l.GapLine();
                l.CheckboxLabeled("LWMDSpDSUchangeFilterQ".Translate(), ref useCustomThingFilter,
                                  "LWMDSpDSUchangeFilterQDesc".Translate());
                if (useCustomThingFilter) {
                    if (customThingFilter==null) {
                        customThingFilter=new ThingFilter();
                        customThingFilter.CopyAllowancesFrom(def.building.fixedStorageSettings.filter);
                        Utils.Mess(Utils.DBF.Settings,"Created new filter for "+def.defName+": "+customThingFilter);
//                        Log.Error("Old filter has: "+def.building.fixedStorageSettings.filter.AllowedDefCount);
//                        Log.Warning("New filter has: "+customThingFilter.AllowedDefCount);
                    }
                    // Make new filter UI:
                    Rect r=l.GetRect(300);
                    r.width*=2f/3f;
                    r.x+=10f; //indent a little, etc.
                    ThingFilterUI.DoThingFilterConfigWindow(r, ref thingFilterScrollPosition, customThingFilter);
                } else { // not using custom thing filter:
                    if (customThingFilter!=null || defaultDSUValues.ContainsKey("DSU_"+def.defName+"_filter")) {
                        customThingFilter=null;
                        //todo: This is all wrong.
                        def.building.fixedStorageSettings.filter=(ThingFilter)defaultDSUValues["DSU_"+def.defName+"_filter"];
                        if (tmpAdditionalCategories.NullOrEmpty() && defaultDSUValues.ContainsKey("DSU_"+def.defName+"_filter")) {
                            Utils.Mess(Utils.DBF.Settings, "  Removing filter for "+def.defName);
                            defaultDSUValues.Remove("DSU_"+def.defName+"_filter");
                        }
                    }
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
                    TestAndUpdate("label", tmpLabel, ref def.label);
                    TestAndUpdate("maxNumStacks", tmpMaxNumStacks, ref def.GetCompProperties<Properties>().maxNumberStacks);
                    TestAndUpdate("maxTotalMass", tmpMaxTotalMass, ref def.GetCompProperties<Properties>().maxTotalMass);
                    TestAndUpdate("maxMassStoredItem", tmpMaxMassStoredItem, ref def.GetCompProperties<Properties>().maxMassOfStoredItem);
                    TestAndUpdate("showContents", tmpShowContents, ref def.GetCompProperties<Properties>().showContents);
                    TestAndUpdate("overlayType", tmpOverlayType, ref def.GetCompProperties<Properties>().overlayType);
                    StoragePriority tmpSP=def.building.defaultStorageSettings.Priority; // hard to access private field directly
                    TestAndUpdate("storagePriority", tmpStoragePriority, ref tmpSP);
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

            /* Set ref origValue to value, making sure defaultDSUValues[keylet] is updated correctly */
            private void TestAndUpdate<T>(string keylet, T value, ref T origValue) where T : IComparable {
                string key="DSU_"+def.defName+"_"+keylet;
                if (value.CompareTo(origValue)==0) {
                    Utils.Mess(Utils.DBF.Settings,"  No change: "+key);
                    return;
                }
                Utils.Mess(Utils.DBF.Settings,"changing value for "+key+" from "+origValue+" to "+value);
                // "origValue" may be suspect - user could already have changed it once.  So:
                //    (this IS assignment by value, right?)
                T defaultValue=(defaultDSUValues.ContainsKey(key)?(T)defaultDSUValues[key]:origValue);
                origValue=value;
                // if the user reset to original default values, remove the default values key
                if (defaultValue.CompareTo(origValue)==0 && defaultDSUValues.ContainsKey(key)) {
                    defaultDSUValues.Remove(key);
                    Utils.Mess(Utils.DBF.Settings, "  removing default record for item "+keylet+" ("+def.defName+")");
                } else if (!defaultDSUValues.ContainsKey(key)) {
                    defaultDSUValues[key]=defaultValue;
                    Utils.Mess(Utils.DBF.Settings, "  creating default record for item "+keylet+" ("+def.defName+")");
                }
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

            bool useCustomThingFilter=false;
            ThingFilter customThingFilter=null;
            Vector2 thingFilterScrollPosition=new Vector2(0,0);
            Rect DSUViewRect=new Rect(0,0,100f,1000f);
            Vector2 DSUScrollPosition=new Vector2(0,0);
        }
        // defName can be null to mean everything.
        private static void ResetDSUToDefaults(string defName) {
            var allKeys=new List<string>(defaultDSUValues.Keys);
            Utils.Warn(Utils.DBF.Settings, "Resetting DSU to default: "+(defName==null?"ALL":defName)
                       +" ("+allKeys.Count+" defaults to search)");
            if (additionalCategories!=null) {
                if (defName!=null) additionalCategories.Remove(defName);
                //else additionalCategories.Clear(); // todo: defaults?
                else additionalCategories=null;
            }
            if (defsUsingCustomFilter!=null) {
                if (defName!=null) defsUsingCustomFilter.Remove(defName);
                else defsUsingCustomFilter=null;
            }
            while (allKeys.Count > 0) {
                var key=allKeys.Last();
                var value = defaultDSUValues[key];
                string s=key.Remove(0,4); // string off first DSU_
                var t=s.Split('_');
                string prop=t[t.Length-1]; // "LWM_Big_Shelf_label" ->  grab "label"
                string keyDefName=string.Join("_", t.Take(t.Length-1).ToArray()); // put defName back together
                Utils.Mess(Utils.DBF.Settings, "Checking key "+key+" (defName of "+keyDefName+")");
                if (defName==null || defName=="" || defName==keyDefName) {
                    Log.Message("LWM.DeepStorage: Resetting "+keyDefName+"'s "+prop+" to default: "+value);
                    var def=DefDatabase<ThingDef>.GetNamed(keyDefName);
                    if (prop=="label") {
                        def.label=(string)(value);
                    } else if (prop=="maxNumStacks") {
                        def.GetCompProperties<Properties>().maxNumberStacks=(int)(value);
                    } else if (prop=="maxTotalMass") {
                        def.GetCompProperties<Properties>().maxTotalMass=(float)(value);
                    } else if (prop=="maxMassStoredItem") {
                        def.GetCompProperties<Properties>().maxMassOfStoredItem=(float)(value);
                    } else if (prop=="showContents") {
                        def.GetCompProperties<Properties>().showContents=(bool)(value);
                    } else if (prop=="storagePriority") {
                        def.building.defaultStorageSettings.Priority=(StoragePriority)(value);
                    } else if (prop=="overlayType") {
                        def.GetCompProperties<Properties>().overlayType=(LWM.DeepStorage.GuiOverlayType)(value);
                    } else if (prop=="filter") {
                        def.building.fixedStorageSettings.filter=(ThingFilter)(value);
                    } else {
                        Log.Error("LWM.DeepStorage: FAILED TO RESET OPTION TO DEFAULT: "+key);
                    }
                    defaultDSUValues.Remove(key);
                }
                allKeys.RemoveLast();
            }
        }
        private static bool IsDSUChanged(ThingDef d) {
            if (additionalCategories!=null && additionalCategories.ContainsKey(d.defName) && //todo: Do I really need this?
                !additionalCategories[d.defName].NullOrEmpty()) return true;
            foreach (string k in defaultDSUValues.Keys) {
                string s=k.Remove(0,4); // strip off DSU_
                var t=s.Split('_');
                string keyDefName=string.Join("_", t.Take(t.Length-1).ToArray());
                if (keyDefName==d.defName) return true;
            }
            return false;
        }
        private static void ResetAllToDefaults() {
            ResetDSUToDefaults(null);
        }

        public static void ExposeDSUSettings(List<ThingDef> units) {
            // additionalCategories - use custom Dictionary<string,List<string>> saver:
            ExposeDefDict("additionalCats", ref additionalCategories);
            ExposeDefDict("additionalDefs", ref additionalDefs);


            if (Scribe.mode!=LoadSaveMode.LoadingVars) { // saving
                // Convert everything to changedProperties, and then save that:
                // Everything will have a format of DSU_defName_propertyName
                // e.g., DSU_LWM_Skip_maxNumStacks
                changedProperties=new Dictionary<string, object>();
                foreach (string key in defaultDSUValues.Keys) {
                    // defaultDSUValues's keys are already the correct format, but we have to take it apart anyway.
                    string s=key.Remove(0,4); // string off first DSU_
                    var t=s.Split('_');
                    string prop=t[t.Length-1]; // "LWM_Big_Shelf_label" ->  grab "label"
                    string defName=string.Join("_", t.Take(t.Length-1).ToArray()); // put defName back together
                    //Utils.Mess(Utils.DBF.Settings, "Checking key "+key+" (defName of "+keyDefName+")");
                    if (prop == "filter") continue; // handled differently
                    DeepStorage.Properties cp=DefDatabase<ThingDef>.GetNamed(defName, false)?.GetCompProperties<Properties>();
                    if (cp==null) {
                        Log.Error("LWM.DeepStorage: tried to save modified property "+key+", but could not find comp!");
                        continue;
                    }
                    // For the record, I think switch statements are usually stupid.
                    if (prop=="label") {
                        changedProperties[key]=DefDatabase<ThingDef>.GetNamed(defName).label;
                    } else if (prop=="maxNumStacks") {
                        changedProperties[key]=cp.maxNumberStacks;
                    } else if (prop=="maxTotalMass") {
                        changedProperties[key]=cp.maxTotalMass;
                    } else if (prop=="maxMassStoredItem") {
                        changedProperties[key]=cp.maxMassOfStoredItem;
                    } else if (prop=="showContents") {
                        changedProperties[key]=cp.showContents;
                    } else if (prop=="storagePriority") {
                        changedProperties[key]=DefDatabase<ThingDef>.GetNamed(defName).building.defaultStorageSettings.Priority;
                    } else if (prop=="overlayType") {
                        changedProperties[key]=cp.overlayType;
                    } else {
                        Log.Error("LWM.DeepStorage: Trying to save "+key+" but have no idea what "+prop+" is.");
                    }
                } // looped through all things with a default value stored
                if (changedProperties.Count > 0) {
                    // I really hope this works.  But everything should be saveable in LookMode.Value?
                    Utils.Mess(Utils.DBF.Settings, "Expose DSU Settings: writing "+changedProperties.Count+" user-modified properties");
                    Scribe_Collections.Look(ref changedProperties, "changedProps", LookMode.Value, LookMode.Value);
                }
                changedProperties=null; // don't need it anymore
            } else {                                     // loading
                changedProperties=null;
                Utils.Mess(Utils.DBF.Settings, "Expose DSU Settings: loading user-modified properties:");
                Scribe_Collections.Look(ref changedProperties, "changedProps", LookMode.Value, LookMode.Value);
                Utils.Mess(Utils.DBF.Settings, (changedProperties==null || changedProperties.Count==0)?
                           "  No user-modified properties":
                           ("  Found "+changedProperties.Count+":\n    "
                            +string.Join("\n    ", changedProperties.Select(kv=>kv.Key+"="+kv.Value).ToArray())));
                // Actually applying these changes is done when the Defs are Loaded.  (OnDefsLoaded)//todo: what is it called?
            }
            return; // todo - clean this up:
//            Utils.Warn(Utils.DBF.Settings, "ExposeDSUSettings: Looking at defsUsingCustomFilter.");
//            Scribe_Collections.Look<string>(ref defsUsingCustomFilter, "defsUsingCustomFilter", LookMode.Value, new object[0]);
//            foreach (ThingDef u in units) {
            foreach (ThingDef u in new List<ThingDef>()) {
                Utils.Mess(Utils.DBF.Settings, "Expose DSU Settings: "+u.defName+" ("+Scribe.mode+")");
                string k1=u.defName;
                ExposeDSUSetting<string>(k1+"_label",ref u.label);
                ExposeDSUSetting(k1+"_maxNumStacks", ref u.GetCompProperties<Properties>().maxNumberStacks);
                ExposeDSUSetting(k1+"_maxTotalMass", ref u.GetCompProperties<Properties>().maxTotalMass);
                ExposeDSUSetting(k1+"_maxMassStoredItem", ref u.GetCompProperties<Properties>().maxMassOfStoredItem);
                ExposeDSUSetting(k1+"_showContents", ref u.GetCompProperties<Properties>().showContents);
                ExposeDSUSetting(k1+"_overlayType", ref u.GetCompProperties<Properties>().overlayType);
                StoragePriority tmpSP=u.building.defaultStorageSettings.Priority; // hard to access private field directly
                ExposeDSUSetting<StoragePriority>(k1+"_storagePriority", ref tmpSP);
                u.building.defaultStorageSettings.Priority=tmpSP;
                /*              if (defaultDSUValues.ContainsKey("DSU_"+u.defName+"_filter")) {
                    Utils.Mess(Utils.DBF.Settings, "  default filter recorded, doing Scribe_Deep");
                    Scribe_Deep.Look(ref u.building.fixedStorageSettings.filter, "DSU_"+u.defName+"_filter", null);
                    if (u.building.fixedStorageSettings.filter==null) { // we were loading/resetting, looks like
                        Utils.Mess(Utils.DBF.Settings, "  ----> default filter is now null!");
                        u.building.fixedStorageSettings.filter=(ThingFilter)defaultDSUValues["DSU_"+u.defName+"_filter"];
                        defaultDSUValues.Remove("DSU_"+u.defName+"_filter");
                    }
                } else {
                    ThingFilter tmp=null;
                    Scribe_Deep.Look(ref tmp, "DSU_"+u.defName+"_filter", null);
                    if (tmp!=null) {
                        Utils.Mess(Utils.DBF.Settings, "  Found Filter, applying.");
                        defaultDSUValues["DSU_"+u.defName+"_filter"]=u.building.fixedStorageSettings.filter;
                        u.building.fixedStorageSettings.filter=tmp;
                    }
                } */
/*                    string key="DSU_"+u.defName+"_label";
                      string value=u.label;
                      string defaultValue=(defaultDSUValues.ContainsKey(key)?(string)defaultDSUValues[key]:value);
                      Scribe_Values.Look(ref value, key, defaultValue);
                      if (defaultValue != value && !defaultDSUValues.ContainsKey(key)) {
                      defaultDSUValues[key]=defaultValue;
                      }
*/
            }
        }
        // Scribe_Collections is great, but it does not handle nested collections
        //   (at least, the 1.0 version doesn't)  So, we save the keys separately
        //   from the values.  It's okay.
        static void ExposeDefDict(string label, ref Dictionary<string, List<string>> dict) {
            if (Scribe.mode!=LoadSaveMode.LoadingVars) {
                // for saving:
                Utils.Err(Utils.DBF.Settings, "ExposeDSUSettings: Saving dict with label "+label);
                if (dict!=null && dict.Count>0) {
                    // store as defName1|defName2|...
                    string s=String.Join("|", dict.Keys.ToArray());
                    Scribe_Values.Look(ref s, "defsWith"+label.CapitalizeFirst());
                    // save each list under its own name
                    foreach (var kvp in dict) {
                        string key=label+"_"+kvp.Key; // e.g.,  additionalCategories_LWM_Skip
                        List<string> list=kvp.Value;
                        Scribe_Collections.Look<string>(ref list, key, LookMode.Value, new object[0]);
                    }
                }
            } else {
                // for loading:
                Utils.Err(Utils.DBF.Settings, "ExposeDSUSettings: Loading dict with label "+label);
                string s=null;
                Scribe_Values.Look(ref s, "defsWith"+label.CapitalizeFirst());
                if (s!=null) {
                    dict=new Dictionary<string,List<string>>();
                    foreach (string defName in s.Split('|')) {
                        List<string> tmpList=null;
                        Scribe_Collections.Look<string>(ref tmpList, label+"_"+defName, LookMode.Value, new object[0]);
                        dict[defName]=tmpList;
                    }
                } else { dict=null; }
            }



        }
        // Only ONE DSU Setting:
        private static void ExposeDSUSetting<T>(string keylet, ref T value) where T : IComparable {
            string key = "DSU_"+keylet;
            T defaultValue=(defaultDSUValues.ContainsKey(key)?(T)defaultDSUValues[key]:value);
            if (defaultValue.CompareTo(value)!=0) {
                Utils.Mess(Utils.DBF.Settings, "  Expose Setting: "+key+" (current value: "
                           +value+"; default Value: "+defaultValue+")");
            }
            Scribe_Values.Look(ref value, key, defaultValue);
            if (defaultValue.CompareTo(value)!=0) {
                Utils.Mess(Utils.DBF.Settings, "  after scribing: "+key+" (current value: "
                           +value+"; default Value: "+defaultValue+")");
            }
            if (defaultValue.CompareTo(value) != 0 && !defaultDSUValues.ContainsKey(key)) {
//                Log.Message("-->"+key+" storing default value of "+defaultValue);//TODO
//                Log.Warning("        Current value: "+value);
                defaultDSUValues[key]=defaultValue;
            }
        }


        private float totalContentHeight=1000f;
        private static Vector2 scrollPosition;

		private const float TopAreaHeight = 40f;
		private const float TopButtonHeight = 35f;
		private const float TopButtonWidth = 150f;
        private const float ScrollBarWidthMargin = 18f;
        private const float LabelHeight=22f;

        // Actual Logic objects:
        //   Default values for DSU objects, so that when saving mod settings, we know what the defaults are.
        //   Only filled in as user changes the values.
        public static Dictionary<string, object> defaultDSUValues=new Dictionary<string, object>();
        public static Dictionary<string,List<string>> additionalCategories=null;//new Dictionary<string,List<string>>();
        public static Dictionary<string,List<string>> additionalDefs=null;
        public static Dictionary<string,object> changedProperties=null;
//        public static List<string> modifiedDefs=null;
        public static List<string> defsUsingCustomFilter;

    }

}
