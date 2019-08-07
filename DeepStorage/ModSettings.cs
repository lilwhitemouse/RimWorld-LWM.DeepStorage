using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace LWM.DeepStorage
{
    public class Settings : ModSettings {
        public static bool storingTakesTime=true;
        public static float storingGlobalScale=1f;
        public static bool storingTimeConsidersStackSize=true;

        // Architect Menu:
        // The defName for the DesignationCategoryDef the mod items are in by default:
        // To change for another mod, change this const string, and then add to the
        // file ModName/Languages/English(etc)/Keyed/settings(or whatever).xml:
        // <[const string]_ArchitectMenuSettings>Location on Architect Menu:</...>
        // Copy and paste the rest of anything that says "Architect Menu"
        // Change the list of new mod items in the final place "Architect Menu" tells you to
        private const string architectMenuDefaultDesigCatDef="LWM_DS_Storage";
        private static string architectMenuDesigCatDef=architectMenuDefaultDesigCatDef;


//        public static DesignationCategoryDef architectLWM_DS_Storage_DesignationCatDef=null; // keep track of this as it may be removed from DefDatabase
//        public static DesignationCategoryDef architectCurrentDesignationCatDef=null;

        private static List<ThingDef> allDeepStorageUnits=null;

        public static bool intelligenceWasChanged=false; // for switching away from HugsLib settings

//        private static float scrollViewHeight=100f;
        public static void DoSettingsWindowContents(Rect inRect) {
            Setup();
            Listing_Standard l = new Listing_Standard(GameFont.Medium); // my tiny high-resolution monitor :p
            l.Begin(inRect);


            l.GapLine();  // Intelligence to haul to
            string [] intLabels={
                "LWM_DS_Int_Animal".Translate(),
                "LWM_DS_Int_ToolUser".Translate(),
                "LWM_DS_Int_Humanlike".Translate(),
            };
            // This setting was changed by HugsLib setting.  Am phasing out now:
            if (l.EnumRadioButton<Intelligence>(ref Patch_IsGoodStoreCell.NecessaryIntelligenceToUseDeepStorage, "LWM_DS_IntTitle".Translate(),
                                                "LWM_DS_IntDesc".Translate(), false, intLabels)) {
                intelligenceWasChanged=true;
            }

            
            l.GapLine();  //Storing Delay Settings
            l.Label("LWMDSstoringDelaySettings".Translate());
            l.CheckboxLabeled("LWMDSstoringTakesTimeLabel".Translate(),
                                             ref storingTakesTime, "LWMDSstoringTakesTimeDesc".Translate());
            l.Label("LWMDSstoringGlobalScale".Translate((storingGlobalScale*100f).ToString("0.")));
            storingGlobalScale=l.Slider(storingGlobalScale, 0f, 2f);
            l.CheckboxLabeled("LWMDSstoringTimeConsidersStackSize".Translate(),
                              ref storingTimeConsidersStackSize, "LWMDSstoringTimeConsidersStackSizeDesc".Translate());
            // Reset storing delay settings to defaults
            if (l.ButtonText("LWMDSstoringDelaySettings".Translate()+": "+"ResetBinding".Translate()/*Reset to Default*/)) {
                storingTakesTime=true;
                storingGlobalScale=1f;
                storingTimeConsidersStackSize=true;
            }


            // Architect Menu:
            l.GapLine();  //Architect Menu location
/*
//            string archLabel=
//            if (archLabel==n
//            l.Label("LWMDSarchitectMenuSettings".Translate());
            if (architectMenuDesigCatDef==architectMenuDefaultDesigCatDef) {
//                if (architectLWM_DS_Storage_DesignationCatDef==null) {
//                    Log.Error("LWM.DeepStorage: architectLWM_DS_Storage_DesignationCatDef was null; this should never happen.");
//                    tmp="ERROR";
//                } else {
//                    tmp=architectCurrentDesignationCatDef.LabelCap; // todo: (default)
//                }
                archLabel+=" ("+
            } else {
                var x=DefDatabase<DesignationCategoryDef>.GetNamed(architectMenuDesignationCatDefDefName, false);
                if (x==null) {
                    // TODO
                }
                tmp=x.LabelCap; // todo: (<menuname>)
            }*/
            if ( l.ButtonTextLabeled((architectMenuDefaultDesigCatDef+"_ArchitectMenuSettings").Translate(), // Label
                                     // value of dropdown button:
                                     DefDatabase<DesignationCategoryDef>.GetNamed(architectMenuDesigCatDef)?.LabelCap
                                     ?? "--ERROR--") ) { // error display text
//                                     , DefDatabase<DesigarchitectMenuDesigCatDef) ) {
                // Float menu for architect Menu choice:
                List<FloatMenuOption> alist = new List<FloatMenuOption>();
                var arl=DefDatabase<DesignationCategoryDef>.AllDefsListForReading; //all reading list
                alist.Add(new FloatMenuOption(DefDatabase<DesignationCategoryDef>.GetNamed(architectMenuDefaultDesigCatDef).LabelCap
                                              +" ("+"default".Translate()+")",
                                              delegate () {
                                                  Utils.Mess(Utils.DBF.Settings, "Architect Menu placement set to default Storage");
                                                  ChangeArchitectMenuLocation(architectMenuDefaultDesigCatDef);
//                                                  architectCurrentDesignationCatDef=architectLWM_DS_Storage_DesignationCatDef;
//                                                  architectMenuDesignationCatDefDefName="LWM_DS_Storage";
//
//                                                  SettingsChanged();
                                              }, MenuOptionPriority.Default, null, null, 0f, null, null));
                // Architect Menu:  You may remove the "Furniture" references here if you wish
                alist.Add(new FloatMenuOption(DefDatabase<DesignationCategoryDef>.GetNamed("Furniture").LabelCap,
                                              delegate () {
//                                                  architectCurrentDesignationCatDef=
//                                                      DefDatabase<DesignationCategoryDef>.GetNamed("Furniture");
                                                  Utils.Mess(Utils.DBF.Settings, "Architect Menu placement set to Furniture.");
                                                  ChangeArchitectMenuLocation("Furniture");
//                                                  architectMenuDesignationCatDefDefName="Furniture";
//
//                                                  SettingsChanged();
                                              }, MenuOptionPriority.Default,null,null,0f,null,null));
                foreach (var adcd in arl) { //architect designation cat def
                    if (adcd.defName!=architectMenuDefaultDesigCatDef && adcd.defName!="Furniture")
                        alist.Add(new FloatMenuOption(adcd.LabelCap,
                                                      delegate () {
//                                                          architectCurrentDesignationCatDef=adcd;
//                                                          architectMenuDesignationCatDefDefName=adcd.defName;
                                                          Utils.Mess(Utils.DBF.Settings, "Architect Menu placement set to "+adcd);
                                                          ChangeArchitectMenuLocation(adcd.defName);
//                                                          SettingsChanged();
                                                      }, MenuOptionPriority.Default,null,null,0f,null,null));
                }
                Find.WindowStack.Add(new FloatMenu(alist));
            }
            // finished drawing settings for Architect Menu


               
            l.End();
        }

        public static void DefsLoaded() {
            // Todo? If settings are different from defaults, then:
            Setup();
            // Architect Menu:
            if (architectMenuDesigCatDef != architectMenuDefaultDesigCatDef) {
                ChangeArchitectMenuLocation(architectMenuDesigCatDef, true);
            }
//            SettingsChanged();
        }

        public static void SettingsChanged() {
            Setup();

        }

        // Architect Menu:
        public static void ChangeArchitectMenuLocation(string newDefName, bool loadingOnStartup=false) {
//            Utils.Warn(Utils.DBF.Settings, "SettingsChanged()");
            DesignationCategoryDef prevDesignationCatDef;
            if (loadingOnStartup) prevDesignationCatDef=DefDatabase<DesignationCategoryDef>.GetNamed(architectMenuDefaultDesigCatDef);
            else prevDesignationCatDef=DefDatabase<DesignationCategoryDef>.GetNamed(architectMenuDesigCatDef, false);
            DesignationCategoryDef newDesignationCatDef=DefDatabase<DesignationCategoryDef>.GetNamed(newDefName);
            if (newDesignationCatDef == null) {
                Log.Error("Failed to change Architect Menu settings!");
                return;
            }
            // Architect Menu: Specify all your buildings/etc:
            //   var allMyBuildings=DefDatabase<ThingDef>.AllDefsListForReading.FindAll(x=>x.HasComp(etc)));
            foreach (var d in allDeepStorageUnits) {
                d.designationCategory=newDesignationCatDef;
            }
            // Flush designation category defs:
            prevDesignationCatDef?.ResolveReferences();
            newDesignationCatDef.ResolveReferences();
            // To remove mod's DesignationCategoryDef from Architect menu:
            //   remove it from RimWorld.MainTabWindow_Architect's desPanelsCached.
            // We remove it from the desPanelsCached rather than removing the def from
            //   the DefDatabasea nd then re-doing the entire caching process, because:
            //   1.  Removing a def from the DefDatabase is probably a bad idea:
            //       entries have an index; who knows what happens if it changes?
            //   2.  Compatibility with other mods is somewhat safer this way.
            List<ArchitectCategoryTab> archMenu=(List<ArchitectCategoryTab>)Harmony.AccessTools
                .Field(typeof(RimWorld.MainTabWindow_Architect), "desPanelsCached")
                .GetValue((MainTabWindow_Architect)MainButtonDefOf.Architect.TabWindow);
            archMenu.RemoveAll(t=>t.def.defName==architectMenuDefaultDesigCatDef);

            archMenu.Add(new ArchitectCategoryTab(newDesignationCatDef));
            archMenu.Sort((a,b)=>a.def.order.CompareTo(b.def.order));
            architectMenuDesigCatDef=newDefName;
/*
            
            if (architectMenuDesignationCatDefDefName=="LWM_DS_Storage") { // default
                if (DefDatabase<DesignationCategoryDef>.GetNamedSilentFail("LWM_DS_Storage") == null) {
                    Utils.Mess(Utils.DBF.Settings,"Adding 'Storage' to the architect menu.");
                    DefDatabase<DesignationCategoryDef>.Add(architectLWM_DS_Storage_DesignationCatDef);
                } else {
                    Utils.Mess(Utils.DBF.Settings, "No need to add 'Storage' to the architect menu.");
                }
                architectCurrentDesignationCatDef=architectLWM_DS_Storage_DesignationCatDef;
            } else {
                // remove our "Storage" from the architect menu:
                Utils.Mess(Utils.DBF.Settings,"Removing 'Storage' from the architect menu.");
                DefDatabase<DesignationCategoryDef>.AllDefsListForReading.Remove(architectLWM_DS_Storage_DesignationCatDef);
                if (DefDatabase<DesignationCategoryDef>.GetNamedSilentFail("LWM_DS_Storage") != null) {
                    Log.Error("Failed to remove LWM_DS_Storage :("+DefDatabase<DesignationCategoryDef>.GetNamedSilentFail("LWM_DS_Storage"));
                }

                architectCurrentDesignationCatDef=DefDatabase<DesignationCategoryDef>.GetNamed(architectMenuDesignationCatDefDefName);
            }
            prevDesignationCatDef?.ResolveReferences();
            architectCurrentDesignationCatDef.ResolveReferences();
            
//            Harmony.AccessTools.Method(typeof(RimWorld.MainTabWindow_Architect), "CacheDesPanels")
//                .Invoke((), null);
*/
            Utils.Warn(Utils.DBF.Settings, "Settings changed architect menu");
            
        }

        // Setup stuff that needs to be run before settings can be used.
        //   I don't risk using a static constructor because I must make sure defs have been finished loading.
        //     (testing shows this is VERY correct!!)
        //   There's probably some rimworld annotation that I could use, but this works:
        private static void Setup() {
            if (allDeepStorageUnits.NullOrEmpty()) {
                allDeepStorageUnits=DefDatabase<ThingDef>.AllDefsListForReading.FindAll(x=>x.HasComp(typeof(CompDeepStorage)));
                Utils.Mess(Utils.DBF.Settings, "  allDeepStorageUnits initialized: "+allDeepStorageUnits.Count+" units");
            }
            /*           if (architectLWM_DS_Storage_DesignationCatDef==null) {
                architectLWM_DS_Storage_DesignationCatDef=DefDatabase<DesignationCategoryDef>.GetNamed("LWM_DS_Storage");
                Utils.Mess(Utils.DBF.Settings, "  Designation Category Def loaded: "+architectLWM_DS_Storage_DesignationCatDef);
            }*/
        }

        public override void ExposeData() {
            base.ExposeData();

            Scribe_Values.Look(ref storingTakesTime, "storing_takes_time", true);
            Scribe_Values.Look(ref storingGlobalScale, "storing_global_scale", 1f);
            Scribe_Values.Look(ref Patch_IsGoodStoreCell.NecessaryIntelligenceToUseDeepStorage, "int_to_use_DS", Intelligence.Humanlike);
            Scribe_Values.Look(ref intelligenceWasChanged, "int_was_changed", false);
            // Architect Menu:
            Scribe_Values.Look(ref architectMenuDesigCatDef, "architect_desig", architectMenuDefaultDesigCatDef);
        }
    }

    static class DisplayHelperFunctions {
        // Helper function to create EnumRadioButton for Enums in settings
        public static bool EnumRadioButton<T>(this Listing_Standard ls, ref T val, string label, string tooltip="",
                                              bool showEnumValues=true, string[] buttonLabels=null) {
            if ((val as Enum)==null) {
                Log.Error("LWM.DisplayHelperFunction: EnumRadioButton passed non-enum value");
                return false;
            }
            bool result=false;
            if (tooltip=="")
                ls.Label(label);
            else
                ls.Label(label,-1,tooltip);
            var enumValues = Enum.GetValues(val.GetType());
            int i=0;
            foreach (T x in enumValues) {
                string optionLabel;
                if (showEnumValues || buttonLabels==null) {
                    optionLabel=x.ToString();
                    if (buttonLabels != null) {
                        optionLabel+=": "+buttonLabels[i];
                    }
                } else {
                    optionLabel=buttonLabels[i]; // got a better way?
                }
                if (ls.RadioButton(optionLabel, (val.ToString()==x.ToString()))) {
                    val=x; // I swear....ToString() was the only thing that worked.
                    result=true;
                } // nice try, C#, nice try.
                // ((val as Enum)==(x as Enum)) // nope
                // (System.Convert.ChangeType(val, Enum.GetUnderlyingType(val.GetType()))==
                //  System.Convert.ChangeType(  x, Enum.GetUnderlyingType(  x.GetType()))) // nope
                // (x.ToString()==val.ToString())// YES!
                i++;
            }
            return result;
        }
    } // end DisplayHelperFunctions


}
