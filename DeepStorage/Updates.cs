#if false
using System;
using Harmony;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using UnityEngine; // because graphics.

/********************* Updates.cs  ************************
 * Contains code to update BigShelf to LWM_BigShelf
 * After 2020 starts, this code will be removed, as 
 * will the BigShelf def.  This way, no risk of the
 * defName conflicting with anyone else, but we can
 * switch everyone to the new defName.
 */

namespace LWM.DeepStorage
{
    [HarmonyPatch(typeof(Verse.BackCompatibility), "BackCompatibleDefName")]
    public static class Patch_LWM_DS_BackCompatibleDefName {
        public static void Postfix(Type defType, string defName, ref string __result) {
            if (defType==typeof(ThingDef)) {
                if (defName=="BigShelf") __result="LWM_BigShelf";
            }
        }
        
    }
}
#endif
#if false
using System;
using System.Xml;
using RimWorld;
using Verse;
using System.Collections.Generic;
using HarmonyLib;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using UnityEngine; // because graphics.

// A way to update save games when defs have changed at mod addition

namespace Whatever
{
    [StaticConstructorOnStartup]
    public class UpdateAncientSaves : BackCompatibilityConverter
    {
        static UpdateAncientSaves()
        {
            // Runs when defs loaded at game startup
            List<BackCompatibilityConverter> list;
            var listFieldInfo = typeof(Verse.BackCompatibility).GetField("conversionChain", HarmonyLib.AccessTools.all);
            list = (List<Verse.BackCompatibilityConverter>)listFieldInfo.GetValue(null);
            if (list == null) Log.Error("Well, I fucked up");
            if (!list.Any(x => x is UpdateAncientSaves)) list.Add(new UpdateAncientSaves());
        }
        public override bool AppliesToVersion(int majorVer, int minorVer)
        {
            return majorVer == 1 && minorVer == 4;
        }

        public override string BackCompatibleDefName(Type defType, string defName, bool forDefInjections = false, XmlNode node = null)
        {
            return null; // don nothing?
        }

        public override Type GetBackCompatibleType(Type baseType, string providedClassName, XmlNode node)
        {
            return null;
        }

        public override void PostExposeData(object obj)
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Building_Storage storage = obj as Building_Storage;
                if (storage != null && storage.def.defName == "AncientBuildingThing?")
                {
                    // Add storage settings?
                }
            }
        }
    }
}
#endif