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
namespace LWM.DeepStorage
{
    /* A little patch to display the cache information in a given cell when
     *   the Debug Inspector is being used
     */
#if DEBUG
    [HarmonyPatch(typeof(Verse.EditWindow_DebugInspector), "CurrentDebugString")]
    public static class Patch_EditWindow_DebugInspector
    {
        public static string Postfix(string __result)
        {
            Map map = Find.CurrentMap;
            if (Current.ProgramState == ProgramState.Playing && map != null)
            {
                IntVec3 cell = UI.MouseCell();
                if (cell.InBounds(map))
                {
                    var cds = map.edificeGrid[cell]?.GetComp<CompDeepStorage>();
                    if (cds != null)
                    {
                        /* The text should have:
                         *    Tick 45894759
                         *    Inspecting (34,0,233)
                         *    [...]
                         * We insert our cache lines after the Inspecting line
                         */                        
                        __result = System.Text.RegularExpressions.Regex.Replace(__result, "(.*Inspecting.*?\n)",
                            "$1"+ 
                            map.GetComponent<DSMapComponent>().Debug(cell) + "\n");
                    }
                }
            }
            return __result;
        }
    }
#endif
}
