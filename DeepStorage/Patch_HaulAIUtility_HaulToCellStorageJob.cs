using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

using static LWM.DeepStorage.Utils.DBF; // trace utils
namespace LWM.DeepStorage
{
    /* Desire is to take Verse.AI.HaulAIUtility's HaulToCellStorageJob 
     * and correct the number that the job allows. We do this by patching
     * the job to replace all calls to
     *       GetItemStackSpaceLeftFor(this cell, map, thing.def) //seriously, .def???
     * with this
     *       Patch_HaulAIUtility_HaulToCellStorageJob.GetDSSpace(cell, map, thing) // no def!
     * And of course we use a Transpiler
     * Also, we will separeately patch GetItemStackSpaceLeftFor(....def) just in
     *   case some other mod uses it or something :p
     */
    [HarmonyPatch(typeof(Verse.AI.HaulAIUtility), "HaulToCellStorageJob")]
    public static class Patch_HaulAIUtility_HaulToCellStorageJob
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> code = instructions.ToList();
            var replaceGetItemsStackSpaceLeftFor = AccessTools.Method(typeof(GridsUtility), "GetItemStackSpaceLeftFor");
            var withGetDSSpace = AccessTools.Method(typeof(Patch_HaulAIUtility_HaulToCellStorageJob), "GetDSSpace");
            var thingDef = AccessTools.Field("Thing:def");
            if (thingDef == null || replaceGetItemsStackSpaceLeftFor == null || withGetDSSpace == null)
            {
                Log.Error("LWM.DeepStorage: HaulToCellStorageJob transpile failed to find needed methods. FAIL");
                foreach (var c in code) yield return c;
                yield break;
            }
            for (int i=0; i<code.Count; i++)
            {
                if (code[i].opcode == OpCodes.Ldfld && (FieldInfo)code[i].operand == thingDef 
                    && code[i+1].opcode == OpCodes.Call
                    && (MethodInfo)code[i+1].operand == replaceGetItemsStackSpaceLeftFor)
                {
                    i++; // skip the .def
                    i++; // skip the replaceGetItemsStackSpaceLeftFor
                    yield return new CodeInstruction(OpCodes.Call, withGetDSSpace);
                    //Log.Message("Replacing GetItemsStackSpaceFor"); // should happen twice
                }
                yield return code[i];
            }
            yield break;
        }
        public static int GetDSSpace(IntVec3 cell, Map map, Thing thing)
        {
            Utils.Mess(Utils.DBF.HaulToCellStorageJob, "GetDSSpace called for " + cell +
                " on " + (map == null ? "a null map" : "some map") + " for thing " +
                (thing == null ? "a null thing:(" : thing.ToString()));                        
            var cds = map.edificeGrid[cell]?.GetComp<CompDeepStorage>();
            if (cds == null)
            {
                Utils.Mess(Utils.DBF.HaulToCellStorageJob, "  But it's in a vanilla space: not our problem");
                return cell.GetItemStackSpaceLeftFor(map, thing.def);
            }
            return cds.CapacityToStoreThingAt(thing, map, cell);
        }
    }
}
