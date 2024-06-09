using System;
using Verse;
using RimWorld;
using HarmonyLib;

namespace LWM.DeepStorage {
    [StaticConstructorOnStartup]
    public static class ModInit {
        static ModInit() {
            Log.Message("LWM Update: stable(ish) 1.5.0.4");
            // Thanks to Static Constructor On Startup, all defs should be loaded now
            RemoveAnyMultipleCompProps();
            LWM.DeepStorage.Settings.DefsLoaded();
            if (ModLister.GetActiveModWithIdentifier("rwmt.Multiplayer") != null) Settings.multiplayerIsActive = true;
            // Can use this when pushing out changes to Steam, to make sure user-tester has
            //     the correct version
            var harmony = new Harmony("net.littlewhitemouse.LWM.DeepStorage");
            harmony.PatchAll();

            // patch things individually:
//            LWM.DeepStorage.Patch_IsSaveCompressible.Postfix();
//            harmony.Patch(AccessTools.Method(typeof(CompressibilityDeciderUtility), "IsSaveCompressible"),
//                null, SymbolExtensions.GetMethodInfo(()=> LWM.DeepStorage.Patch_IsSaveCompressible.))
        }


        public static void RemoveAnyMultipleCompProps()
        {
            // For each def, make sure that only the last DS.Properties is
            // used.  (this can happen if a modder makes another DSU based
            // off of one of the base ones; see Pallet_Covered). Call this
            // after all defs are loaded
            // Note: this design choice was probably stupid, but it's done
            //   now, for better or worse. Better to be safe than sorry, I
            //   guess? I do a lot of crazy stuff.
            foreach (var d in DefDatabase<ThingDef>.AllDefs)
            {
                if (typeof(Building_Storage).IsAssignableFrom(d.thingClass))
                {
                    var cmps = d.comps;
                    for (int i = cmps.Count - 1; i >= 0; i--)
                    {
                        if (cmps[i] is LWM.DeepStorage.Properties && i > 0)
                        {
                            // remove any earlier instances
                            // last one in should count:
                            for (i--; i >= 0; i--)
                            {
                                if (cmps[i] is LWM.DeepStorage.Properties)
                                    cmps.RemoveAt(i);
                            }
                            break;
                        }
                    }
                }
                //continue to next def
            }
        } //end RemoveAnyMultipleCompProps


    }


}
