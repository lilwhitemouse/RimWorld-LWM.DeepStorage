using System;
using Verse;
using HarmonyLib;

namespace LWM.DeepStorage {
    [StaticConstructorOnStartup]
    public static class ModInit {
        static ModInit() {
            // Thanks to Static Constructor On Startup, all defs should be loaded now
            LWM.DeepStorage.Properties.RemoveAnyMultipleCompProps();
            LWM.DeepStorage.Settings.DefsLoaded();
            Log.Message("....");
            var harmony = new Harmony("net.littlewhitemouse.LWM.DeepStorage");
            harmony.PatchAll();

            // patch things individually:
//            LWM.DeepStorage.Patch_IsSaveCompressible.Postfix();
//            harmony.Patch(AccessTools.Method(typeof(CompressibilityDeciderUtility), "IsSaveCompressible"),
//                null, SymbolExtensions.GetMethodInfo(()=> LWM.DeepStorage.Patch_IsSaveCompressible.))

        }
    }
}
