using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using UnityEngine;

using static LWM.DeepStorage.Utils.DBF;

namespace LWM.DeepStorage
{
    [HarmonyPatch(typeof(Verse.GenPlace), "TryPlaceDirect")]
    static class Patch_GenPlace_TryPlaceDirect
    {
        static void Postfix(Map map, IntVec3 loc)
        {
            Utils.Mess(Utils.DBF.TryPlaceDirect, "LWM.DS: clearing cache at " + loc + " after TryPlaceDirect");
            map.GetComponent<MapComponentDS>().DirtyCache(loc);
        }
    }
}
