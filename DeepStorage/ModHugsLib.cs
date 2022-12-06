using System;
using Verse;
using HarmonyLib;
#if DEBUG
using HugsLib;
using HugsLib.Settings;

namespace LWM.DeepStorage
{
    internal class LWM_Hug : HugsLib.ModBase
    {
        public override string ModIdentifier {
            get { return "LWM_DeepStorage"; }
        }

        //#if DEBUG
        private SettingHandle<bool>[] debugONorOFF = new SettingHandle<bool>[Utils.showDebug.Length];
        //#endif
        public override void DefsLoaded() {
            //#if DEBUG
            //Log.Message("LWM.DeepStorage:  DefsLoaded via HugsLib():");
            for (int i=1; i<Utils.showDebug.Length; i++) {
                debugONorOFF[i]=Settings.GetHandle("turnDebugONorOFF"+(Utils.DBF)i, "Turn ON/OFF debugging: "+(Utils.DBF)i,
                                       "Turn ON/OFF all debugging - this is a lot of trace, and only available on debug builds",
                                        false);
            }
            SettingsChanged();
            //#endif
            // Move all this to our own INIT:
//            LWM.DeepStorage.Properties.RemoveAnyMultipleCompProps();
//            LWM.DeepStorage.Settings.DefsLoaded();
//            Log.Message("....");
//            var harmony = new Harmony("net.littlewhitemouse.LWM.DeepStorage");
            //harmony.Patch()

        }
        //#if DEBUG
        public override void SettingsChanged() {

            Log.Message("LWM's Deep Storage: Debug settings changed");
            UpdateDebug();
        }

        public void UpdateDebug() {
            for (int i=1; i<Utils.showDebug.Length; i++) { // 0 is always true
                Utils.showDebug[i]=debugONorOFF[i];
            }
        }
        //#endif
        // For careful harmony adding:
        protected override bool HarmonyAutoPatch => false;
    }

}
#endif

