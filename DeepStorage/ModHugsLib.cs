using System;
using Verse;
using Harmony;
using HugsLib;
using HugsLib.Settings;

namespace LWM.DeepStorage
{
    internal class LWM_Hug : HugsLib.ModBase
    {
        public override string ModIdentifier {
            get { return "LWM_DeepStorage"; }
        }

        private SettingHandle<Intelligence> intSetting;
        private SettingHandle<bool>[] debugONorOFF = new SettingHandle<bool>[Utils.showDebug.Length];
        public override void DefsLoaded() {
            // Setting to allow bionic racoons to haul to Deep Storage:
            intSetting=Settings.GetHandle("get_intelligence", "DO NOT CHANGE THIS.",
                 "CHANGE ALL SETTINGS BY USING THE BUTTON ABOVE", Intelligence.Humanlike, null, "LWM_DS_Int_");

            #if DEBUG
            for (int i=1; i<Utils.showDebug.Length; i++) {
                debugONorOFF[i]=Settings.GetHandle("turnDebugONorOFF"+(Utils.DBF)i, "Turn ON/OFF debugging: "+(Utils.DBF)i,
                                       "Turn ON/OFF all debugging - this is a lot of trace, and only available on debug builds",
                                        false);
            }

            /* TODO: More compatibility with StockpileForDisaster
            var sfdType=Harmony.AccessTools.TypeByName("StockpileForDisaster.EntityListComp");
            if (sfdType==null) {
                Log.Error("Didn't find type?? "+sfdType);
            } else {
                Log.Error("Found type "+sfdType);
            }

            }*/
            #endif

            
            AssignSettings();
            LWM.DeepStorage.Settings.DefsLoaded();
        }

        public override void SettingsChanged() {
            Log.Message("LWM's Deep Storage: HugsLib settings changed");
            AssignSettings();
            #if DEBUG
            UpdateDebug();
            #endif
        }

        public void AssignSettings() {
            if (!LWM.DeepStorage.Settings.intelligenceWasChanged) {
                Patch_IsGoodStoreCell.NecessaryIntelligenceToUseDeepStorage = intSetting;
                if (Patch_IsGoodStoreCell.NecessaryIntelligenceToUseDeepStorage == Intelligence.Humanlike) {
                    LWM.DeepStorage.Settings.intelligenceWasChanged = true; // never change it here again...
                }
            }

        }
        public void UpdateDebug() {
            for (int i=1; i<Utils.showDebug.Length; i++) { // 0 is always true
                Utils.showDebug[i]=debugONorOFF[i];
            }
        }
    }

}
