using System;
using RimWorld;
using Verse;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using System.Collections.Generic;
using System.Linq;
using UnityEngine; // for icon for gizmo

namespace LWM.DeepStorage
{
    [HarmonyPatch(typeof(RimWorld.Frame), "Destroy")]
    public static class Patch_Frame_Destroy
    {
        public static void Prefix(Frame __instance)
        {
            Utils.Mess(Utils.DBF.StorageGroup, "Frame " + __instance + " destroyed; " +
                (__instance.Map.GetComponent<MapComponentDS>().settingsForBlueprintsAndFrames.ContainsKey(__instance) ?
                 "removing comp" : "no comp to remove"));
            __instance.Map.GetComponent<MapComponentDS>().settingsForBlueprintsAndFrames.Remove(__instance);
        }
    }

    [HarmonyPatch(typeof(RimWorld.Frame), "CompleteConstruction")]
    public static class Patch_Frame_CompleteConstruction
    {
        // So....CompleteConstruction Destroy()s the Frame before the
        //   new item is fully created. So we grab any stored comp before
        //   that happens (because we need to know the Map before then)
        public static CompDeepStorage compDS;
        static void Prefix(Frame __instance)
        {
            compDS = null;
            if (__instance == null) Log.Error("null instance");
            if (__instance.Map == null) Log.Error("Null map for " + __instance);
            __instance.Map.GetComponent<MapComponentDS>().settingsForBlueprintsAndFrames.Remove(__instance, out compDS);
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool found = false;
            foreach (var instr in instructions)
            {
                yield return instr;
                // This should be fairly ironclad in terms of safety: unless they do something weird,
                //   they will always have "if (thing is Building_Storage storage)...." and I can 
                //   get my work done right then!
                if (instr.opcode == OpCodes.Isinst && instr.OperandIs(typeof(RimWorld.Building_Storage)))
                {
                    found = true;
                    yield return new CodeInstruction(OpCodes.Dup);
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // the frame
                    yield return new CodeInstruction(OpCodes.Call, HarmonyLib.AccessTools
                              .Method(typeof(Patch_Frame_CompleteConstruction), "TransferCompData"));
                }
            }
            if (!found) Log.Error("LWM.DeepStorage: Failed to Transpile Frame's Complete Construction - storage group DS settings will not work");
        }
        static void TransferCompData(Building_Storage storage, Frame frame)
        {
            if (storage == null) return; // duplicate test that follows, because it's harder to inject code after test
            if (compDS != null)
            {
                Log.Message("But...compds isn't null: " + compDS);
                Utils.Mess(Utils.DBF.StorageGroup, "Frame " + frame +
                    (storage.TryGetComp<CompDeepStorage>() != null ?
                    " transferring settings to new " + storage.ToString() :
                    " cannot transfer to storage, 'cause no comp in " + storage.ToString()));
                storage.TryGetComp<CompDeepStorage>()?.CopySettingsFrom(compDS);
            }
        }
    }
    [HarmonyPatch(typeof(RimWorld.Frame), "FailConstruction")]
    static class Patch_Frame_FailConstruction
    {
        // As above, the Frame will be Destroy()d before we can transfer comp ownership, so we need to grab comp first;
        // Also, Map, because we need the Map to get the MapComponent that has the settings storage
        static Map map;
        static void Prefix(Frame __instance)
        {
            map = __instance.Map;
            Patch_Frame_CompleteConstruction.compDS = null;
            map.GetComponent<MapComponentDS>().settingsForBlueprintsAndFrames
                    .Remove(__instance, out Patch_Frame_CompleteConstruction.compDS);
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool found = false;
            foreach (var instr in instructions)
            {
                yield return instr;
                // This should also be fairly ironclad in terms of safety: unless they do something weird,
                //   they will always have "if (thing is Blueprint_Storage bps)...." and I can 
                //   get my work done right then!
                if (instr.opcode == OpCodes.Isinst && instr.OperandIs(typeof(RimWorld.Blueprint_Storage)))
                {
                    found = true;
                    yield return new CodeInstruction(OpCodes.Dup); // the blueprint_storage - possibly
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // the frame
                    yield return new CodeInstruction(OpCodes.Call, HarmonyLib.AccessTools
                              .Method(typeof(Patch_Frame_FailConstruction), "TransferCompOwnership"));
                }
            }
            if (!found) Log.Error("LWM.DeepStorage: Failed to Transpile Frame's Complete Construction - storage group DS settings will not work");
        }
        static void TransferCompOwnership(Blueprint_Storage blueprint, Frame frame)
        {
            if (blueprint == null) return;// duplicate test that follows, because it's still harder to inject code after test
            if (Patch_Frame_CompleteConstruction.compDS != null)
            {
                Utils.Mess(Utils.DBF.StorageGroup, "Frame " + frame + " transferring settings back to blueprint " + blueprint);
                Patch_Frame_CompleteConstruction.compDS.parent = blueprint;
                map.GetComponent<MapComponentDS>().settingsForBlueprintsAndFrames[blueprint] = 
                            Patch_Frame_CompleteConstruction.compDS;
            }
        }
    }
    /*// we skip this for now as frames already don't have storage group setting buttons? We can fix it later if need be
    [HarmonyPatch(typeof(RimWorld.Blueprint_Storage), "GetGizmos")]
    public static class Patch_Blueprint_storage_GetGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Blueprint_Storage __instance)
        {
            foreach (var g in __result) yield return g;
            foreach (var g in 
        }
    }
    */
}
