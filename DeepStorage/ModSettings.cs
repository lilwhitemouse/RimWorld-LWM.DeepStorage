using System;
using Verse;
using UnityEngine;

namespace LWM.DeepStorage
{
    public class Settings : ModSettings {
        public static bool storingTakesTime=true;
        public static float storingGlobalScale=1f;
        public static bool storingTimeConsidersStackSize=true;

        public static bool intelligenceWasChanged=false;

//        private static float scrollViewHeight=100f;
        public static void DoSettingsWindowContents(Rect inRect) {
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
            
            l.GapLine();  //Storing Delay
            l.Label("LWMDSstoringDelaySettings".Translate());
            l.CheckboxLabeled("LWMDSstoringTakesTimeLabel".Translate(),
                                             ref storingTakesTime, "LWMDSstoringTakesTimeDesc".Translate());
            l.Label("LWMDSstoringGlobalScale".Translate((storingGlobalScale*100f).ToString("0.")));
            storingGlobalScale=l.Slider(storingGlobalScale, 0f, 2f);
            l.CheckboxLabeled("LWMDSstoringTimeConsidersStackSize".Translate(),
                              ref storingTimeConsidersStackSize, "LWMDSstoringTimeConsidersStackSizeDesc".Translate());
            if (l.ButtonText("LWMDSstoringDelaySettings".Translate()+": "+"ResetBinding".Translate()/*Reset to Default*/)) {
                storingTakesTime=true;
                storingGlobalScale=1f;
                storingTimeConsidersStackSize=true;
            }
            l.GapLine();


            

            l.End();
        }

        public override void ExposeData() {
            base.ExposeData();

            Scribe_Values.Look(ref storingTakesTime, "storing_takes_time", true);
            Scribe_Values.Look(ref storingGlobalScale, "storing_global_scale", 1f);
            Scribe_Values.Look(ref Patch_IsGoodStoreCell.NecessaryIntelligenceToUseDeepStorage, "int_to_use_DS", Intelligence.Humanlike);
            Scribe_Values.Look(ref intelligenceWasChanged, "int_was_changed", false);
        }
    }

    static class DisplayHelperFunctions {
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
