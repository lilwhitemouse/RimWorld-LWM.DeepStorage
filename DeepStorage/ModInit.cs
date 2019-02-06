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
        public override void DefsLoaded() {
            // Setting to allow bionic racoons to haul to Deep Storage:
            intSetting=Settings.GetHandle("get_intelligence", "LWM_DS_IntTitle".Translate(),
                 "LWM_DS_IntDesc".Translate(), Intelligence.Humanlike, null, "LWM_DS_Int_");


            AssignSettings();
        }

        public override void SettingsChanged() {
            Log.Message("LWM's Deep Storage: HugsLib settings changed");
            AssignSettings();
        }

        public void AssignSettings() {
            Patch_IsGoodStoreCell.NecessaryIntelligenceToUseDeepStorage = intSetting;

        }
    }

}
