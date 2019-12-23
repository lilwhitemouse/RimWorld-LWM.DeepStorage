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

        private class Dialog_DSU_Settings : Window {
            public Dialog_DSU_Settings(ThingDef def) {
                this.forcePause = true;
                this.doCloseX = true;
                this.doCloseButton = false;
                this.closeOnClickedOutside = true;
                this.absorbInputAroundWindow = true;
                this.def=def;

                if (defaultDSUValues.ContainsKey("DSU_"+def.defName+"_filter")) {
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
                string k="DSU_"+def.defName;
//                if (defaultDSUValues.ContainsKey(k+"_label")) tmpLabel=(string)defaultDSUValues[k+"label"];
                HelpSetTempVarToDefault<string>(ref tmpLabel, "label");
                HelpSetTempVarToDefault<int>(ref tmpMaxNumStacks, "maxNumStacks");
                HelpSetTempVarToDefault<float>(ref tmpMaxTotalMass, "maxTotalMass");
                HelpSetTempVarToDefault<float>(ref tmpMaxMassStoredItem, "maxMassStoredItem");
                HelpSetTempVarToDefault<bool>(ref tmpShowContents, "showContents");
                HelpSetTempVarToDefault<StoragePriority>(ref tmpStoragePriority, "storagePriority");
                HelpSetTempVarToDefault<LWM.DeepStorage.GuiOverlayType>(ref tmpOverlayType, "overlayType");
                useCustomThingFilter=false;
                customThingFilter=null;
                //HelpSetTempVarToDefault<>(ref tmp, "");
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
                var XXXcontentRect = new Rect(0, 0, inRect.width, inRect.height - (CloseButSize.y + 10f)).ContractedBy(10f);

                var l = new Listing_Standard();
//                l.Begin(new Rect(inRect.x, inRect.y, inRect.width, inRect.height-CloseButSize.y-5f));
                Rect s=new Rect(inRect.x, inRect.y, inRect.width, inRect.height-CloseButSize.y-5f);
                Rect v=new Rect(inRect.x, inRect.y, inRect.width-20f, inRect.height-CloseButSize.y-5f);
                if (useCustomThingFilter) v.height+=300f;
                l.BeginScrollView(s, ref DSUScrollPosition, ref v);
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
                    Rect r=l.GetRect(300);
                    r.width*=2f/3f;
                    r.x+=10f;
                    ThingFilterUI.DoThingFilterConfigWindow(r, ref thingFilterScrollPosition, customThingFilter);
                } else { // not using custom thing filter:
                    if (customThingFilter!=null || defaultDSUValues.ContainsKey("DSU_"+def.defName+"_filter")) {
                        customThingFilter=null;
                        if (defaultDSUValues.ContainsKey("DSU_"+def.defName+"_filter")) {
                            Utils.Mess(Utils.DBF.Settings, "  Removing filter for "+def.defName);
                            def.building.fixedStorageSettings.filter=(ThingFilter)defaultDSUValues["DSU_"+def.defName+"_filter"];
                            defaultDSUValues.Remove("DSU_"+def.defName+"_filter");
                        }
                    }
                }
//                l.End();
                l.EndScrollView(ref v);

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
                    if (useCustomThingFilter) {
                        if (!defaultDSUValues.ContainsKey("DSU_"+def.defName+"_filter")) {
                            Utils.Mess(Utils.DBF.Settings, "Creating default filter record for item "+def.defName);
                            defaultDSUValues["DSU_"+def.defName+"_filter"]=def.building.fixedStorageSettings.filter;
                        }
                        def.building.fixedStorageSettings.filter=customThingFilter;
                    } else {
                        if (defaultDSUValues.ContainsKey("DSU_"+def.defName+"_filter")) {
                            // we need to remove it
                            Utils.Mess(Utils.DBF.Settings, "Removing default filter record for item "+def.defName);
                            def.building.fixedStorageSettings.filter=(ThingFilter)defaultDSUValues["DSU_"+def.defName+"_filter"];
                            defaultDSUValues.Remove("DSU_"+def.defName+"_filter");
                        }
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
                // if the user reset to originald defaul values, remove the default values key
                if (defaultValue.CompareTo(origValue)==0 && defaultDSUValues.ContainsKey(key)) {
                    defaultDSUValues.Remove(key);
                    Utils.Mess(Utils.DBF.Settings, "  removing default record for item "+keylet+" ("+def.defName+")");
                } else if (!defaultDSUValues.ContainsKey(key)) {
                    defaultDSUValues[key]=defaultValue;
                    Utils.Mess(Utils.DBF.Settings, "  creating default record for item "+keylet+" ("+def.defName+")");
                }
            }

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
            Vector2 thingFilterScrollPosition=new Vector2(0,0);
            Vector2 DSUScrollPosition=new Vector2(0,0);
        }
        private static void ResetDSUToDefaults(string defName) {
            var allKeys=new List<string>(defaultDSUValues.Keys);
            Utils.Warn(Utils.DBF.Settings, "Resetting DSU to default: "+(defName==null?"ALL":defName)
                       +" ("+allKeys.Count+" defaults to search)");
            while (allKeys.Count > 0) {
                var key=allKeys.Last();
                var value = defaultDSUValues[key];
                string s=key.Remove(0,4); // string off first DSU_
                var t=s.Split('_');
                string prop=t[t.Length-1]; // LWM_Big_Shelf_label ->  grab label
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
            foreach (ThingDef u in units) {
                Utils.Warn(Utils.DBF.Settings, "Expose DSU Settings: "+u.defName+" ("+Scribe.mode+")");
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
                if (defaultDSUValues.ContainsKey("DSU_"+u.defName+"_filter")) {
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
                }
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
    }

}
