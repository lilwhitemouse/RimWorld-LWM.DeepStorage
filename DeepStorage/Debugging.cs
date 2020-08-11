using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler


namespace LWM.DeepStorage {
    /* Some code to trace every line of a function:  *
     *   (specifically, Thing's CanStackWith())      *
     *   Requires using System.Reflection.Emit;      */
#if false
    [HarmonyPatch(typeof(Thing), "CanStackWith")]
    static class Trace_CanStackWith {
        public static void Prefix(Thing __instance, Thing other) {
            Log.Error("Can Thing "+__instance.stackCount+__instance+" stack with "+other.stackCount+other+"?");
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            int i=0;
            foreach (CodeInstruction code in instructions) {
                List<Label> ls=code.labels;
                code.labels=new List<Label>();
                CodeInstruction c = new CodeInstruction(OpCodes.Ldarg_0);
                c.labels=ls;
                yield return c;
                yield return new CodeInstruction(OpCodes.Ldc_I4, i++);

                if (code.opcode == OpCodes.Ret) {
                    yield return new CodeInstruction(OpCodes.Call, Harmony.AccessTools.
                                                     Method("LWM.DeepStorage.Trace_CanStackWith:ReturnTest"));
                } else { // other calls possible as well
                    yield return new CodeInstruction(OpCodes.Ldstr, code.ToString());                                        
                    yield return new CodeInstruction(OpCodes.Call, Harmony.AccessTools.
                                                     Method("LWM.DeepStorage.Trace_CanStackWith:JustTrace"));
                }
                yield return code;
            }
        }

        static void JustTrace(Thing t, int n, string s) {
            if (t?.def?.defName!="Kibble") return; // If you need to be specific
            Log.Warning("..."+t.stackCount+t+": line "+n+": "+s);
        }
        
        static bool DoMyTest(bool result, Thing t, int n) {
            //if (t?.def?.defName == "Kibble") { // as needed
                Log.Warning("...-------------------returning "+result+" for "+t.stackCount+t+" at entry "+n); 
            //}
            return result; /* because we pop it off the stack */
        }
        
        public static void Postfix(bool __result) {
            Log.Warning("Actual result: "+__result); /* Note: this can be changed by other mods, of course... */
        }
    }

#endif


    
#if false
    //    [HarmonyPatch(typeof(WorkGiver_Merge), "JobOnThing")]
    class Patch_WorkGiver_Merge {
        static void Postfix(ref Job __result, Pawn pawn, Thing t, bool forced) {
            if (t.GetSlotGroup() != null &&
                (t.GetSlotGroup().parent as ThingWithComps).TryGetComp<CompDeepStorage>() != null) {
                Log.Error("WorkGiver_Merge for " + pawn.ToString() + " found " +
                          t.ToString() + " in DeepStorage " +
                          t.GetSlotGroup().parent.ToString());
                if (__result == null) { Log.Warning("  JOB WAS NULL"); return; }
                Log.Warning("  Job was " + __result.ToString() + " with count " + __result.count);
                Log.Warning("Job returned was null? " + (__result == null ? "null" : __result.ToString()));
            }
        }
    }

    //    [HarmonyPatch(typeof(JobDriver), "EndJobWith")]
    class Patch_EndJobWith_Debug {
        static void Prefix(JobCondition condition, JobDriver __instance) {
            Log.Warning("--" + __instance.pawn.ToString() + "'s job " + __instance.job.ToString() + " ended with " + condition.ToString());
        }
    }






    // This doesn't work, BTW:
    //    [HarmonyPatch(typeof(PawnComponentsUtility), "CreateInitialComponents")]
    class Debug_Start_Debugger_Jobs {
        static void Postfix(Pawn pawn) {
            pawn.jobs.debugLog = true;
        }
    }



    //    [HarmonyPatch(typeof(JobDriver), "TryActuallyStartNextToil")]
    class Patch_TASNT {
        static void Prefix(JobDriver __instance) {
            var all = BindingFlags.Public
                               | BindingFlags.NonPublic
                               | BindingFlags.Instance
                               | BindingFlags.Static
                               | BindingFlags.GetField
                               | BindingFlags.SetField
                               | BindingFlags.GetProperty
                               | BindingFlags.SetProperty;
            List<Toil> l;
            Toil t;
            Job j = __instance.job;
            if (j == null) {
                Log.Error("+No Job for " + __instance.pawn.ToString());
                return;
            }

            Log.Error("+" + __instance.pawn.ToString() + "'s " + j.ToString() + ": Toil: ");
            var a = typeof(JobDriver)
                .GetField("toils", all);
            l = (List<Toil>)a.GetValue(__instance);
            if (l == null) {
                Log.Error("  Null Toils list");
                return;
            }
            int? x;
            x = (int)typeof(JobDriver)
                .GetField("curToilIndex", all)
                .GetValue(__instance);
            if (x != null) {
                Log.Warning("  Current toil: " + x + "/" + l.Count);
            }

            //            Log.Warning(" ++Number toils: " + l.Count);
            for (int i = 0; i < l.Count; i++) {
                if (l[i] == null) {
                    Log.Warning("  [" + i + "]: NULL");
                } else {
                    Log.Warning("  [" + i + "]: " + l[i].ToString()
                                + " - " + l[i].GetType().ToString());
                }
            }
            return;
        }
    }


    //    [HarmonyPatch(typeof(Thing), "SpawnSetup")]
    class Patch_SpawnThing_Debug {
        static void Prefix(Thing __instance, Map map, bool respawningAfterLoad) {
            if (__instance.Destroyed) {
                Log.Error("Spawning bug.");
                Log.Warning("Thing was " + __instance.ToString());
                Log.Warning("  and was at " + __instance.Position.ToString());
                Log.Warning("  and ... " + __instance.Spawned);
                Log.Warning("  Maybe it has " + __instance.ParentHolder?.ToString());

                foreach (Thing t in map.thingGrid.ThingsAt(__instance.Position)) {
                    Log.Warning("---also here? " + t.stackCount + " " + t.ToString());
                }


            }
        }
    }


    //    [HarmonyPatch(typeof(Thing), "DeSpawn")]
    class Patch_Thing_DeSpawn {
        static void Prefix(Thing __instance) {
            Log.Warning("! " + __instance.ToString() + " is DeSpawning!");
        }
    }
    //   [HarmonyPatch(typeof(GenSpawn), "SpawningWipes")]
    class Patch_SpawningWipes {
        static void Postfix(bool __result, BuildableDef newEntDef, BuildableDef oldEntDef) {
            Log.Warning("SpawningWipes: " + newEntDef.ToString() + " over " + oldEntDef.ToString() +
                        "? " + __result);
        }
    }

    //    [HarmonyPatch(typeof(Thing), "TryAbsorbStack")]
    class Debuging_TryAbsorbStack {
        static void Prefix(Thing __instance, Thing other) {
            Log.Warning("TryAbsorbStack:  " + __instance.ToString() + "("
                        + __instance.stackCount + __instance.Destroyed + ") is trying to absorb "
                        + other.ToString() + "(" + other.stackCount + other.Destroyed + ").");
        }
        static void Postfix(Thing __instance, bool __result, Thing other) {
            Log.Warning("Result:  " + __result + ": " + __instance.ToString() + "("
                        + __instance.stackCount + __instance.Destroyed + ") tried to absorb "
                        + other.ToString() + "(" + other.stackCount + other.Destroyed + ").");
        }
    }
    //    [HarmonyPatch(typeof(StoreUtility), "TryFindBestBetterStoreCellFor")]
    class Debug_TryFindBestBetterStoreCellFor {
        static void Postfix(ref bool __result, Thing t, Pawn carrier, ref IntVec3 foundCell) {
            if (t == null) { Log.Error("-------TRYING TO FIND BEST CELL------NULL thing"); return; }
            //           Log.Error("Trying to find best/better store cell for " + t.stackCount + t.ToString() + " (" +
            //                     ((carrier == null) ? "null carrier" : carrier.ToString()) +
            //                     ((foundCell == null) ? "  Found only null cell :(" : (" Found cell to carry it to: " + foundCell.ToString()))
            //                    );
        }
    }





    //  [StaticConstructorOnStartup]
    static class HarXmonyPatchesXXX
    {
        static void HarXmonyPatchesTest()
        {
            return;
            HarmonyInstance harmony;

            harmony = HarmonyInstance.Create("net.littlewhitemouse.rimworld.deep_storageX");


            var all = BindingFlags.Public
                                           | BindingFlags.NonPublic
                                           | BindingFlags.Instance
                                           | BindingFlags.Static
                                           | BindingFlags.GetField
                                           | BindingFlags.SetField
                                           | BindingFlags.GetProperty
                                           | BindingFlags.SetProperty;
            var p = new ParameterModifier[] { };

            Type[] tf = new Type[] {typeof(UnityEngine.Rect),typeof(float).MakeByRefType(),
                typeof(string).MakeByRefType(),typeof(float), typeof(float)};
            //          var tfm = typeof(Verse.Widgets).GetMethod("TextFieldNumeric", all);//, all, null, tf, p);
            var tfm = AccessTools.Method(typeof(Verse.Widgets), "TextFieldNumeric").MakeGenericMethod(typeof(float));
            //              typeof(Verse.Widgets).GetMethod("TextFieldNumeric", all);//, all, null, tf, p);
            if (tfm == null)
            {
                throw (new Exception("TextFieldNumeric capture failure"));
            }

            //HarmonyMethod prefixMethod1 = new HarmonyMethod(typeof(LWM.DeepStorage.HarmonyPatches).GetMethod("TextFieldNumeric"));

            //          harmony.Patch(tfm, prefixMethod1, null); //UGH, TODO




            //          Type[] x = { typeof(Thing), typeof(ThingOwner), typeof(int), typeof(Thing), typeof(bool) };
            MethodInfo targetMethod = AccessTools.Method(typeof(Verse.ThingOwner),
                                                         "TryTransferToContainer",
                                                         new Type[] { typeof(Thing), typeof(ThingOwner), typeof(int), typeof(Thing).MakeByRefType(), typeof(bool) });
            //      new Type[] { typeof(Thing), typeof(ThingOwner), typeof(int), typeof(bool) });

            //          if (targetMethod == null) { Log.Error("Well, F that"); return; }

            HarmonyMethod prefixMethod = new HarmonyMethod(typeof(LWM.DeepStorage.HarXmonyPatchesXXX).GetMethod("TryTransferToContainer_Prefix"));

            harmony.Patch(targetMethod, prefixMethod, null);
            //          Log.Error("Patched?");

            //          targetMethod = AccessTools.Method(typeof(GenPlace),
            //                                           "TryPlaceDirect")


            return;
        }
        //------------------------------------------------------------//


        static bool TryTransferToContainer_Prefix(Thing item, ThingOwner otherContainer) //???
        {
            Log.Error("Trying to Transfer " + item.ToString() + " to container " + otherContainer.ToString());
            return true;
        }
    }






#endif
}
