using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using Harmony;
using RimWorld;
using UnityEngine;
using Verse;

using System.Linq;
using Verse.AI;

using static LWM.DeepStorage.Utils.DBF; // trace utils

namespace LWM.DeepStorage
{
    /*
      Desired sequence of events:
      User right-clicks with pawn selected
      When AddHumanlikeOrders is run,
      and when AddUndraftedOrders->AddJobGiverWorkOrders is run,
        Prefix runs
        Prefix sets flag
      
        Move All Items Away
        (Get basic default orders?)
        For Each Thing
          Move Thing Back
          Call AHlO/AJGWO
          flag is set so runs normally
          Move Thing Away
        Move Things Back
        Combine menu
        return false
      Postfix runs and catches logic, puts together complete, correct menu option list
      So...look, we do the same thing twice!  Function calls!
    */
    [HarmonyPatch(typeof(FloatMenuMakerMap), "AddHumanlikeOrders")]
    static class Patch_AddHumanlikeOrders {
        static bool Prepare(HarmonyInstance instance) {
            Utils.Warn(RightClickMenu, "Loading AddHumanlikeOrders menu code: "
                       +Settings.useDeepStorageRightClickLogic);
            return Settings.useDeepStorageRightClickLogic;
        }
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts) {
            return Patch_FloatMenuMakerMap.Prefix(clickPos,IntVec3.Invalid,pawn,opts,false,false);
        }
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(List<FloatMenuOption> opts) {
            Patch_FloatMenuMakerMap.Postfix(opts);
        }
    }
//    [HarmonyPatch(typeof(FloatMenuMakerMap), "AddJobGiverWorkOrders")]
    static class Patch_AddJobGiverWorkOrders {
        static bool Prepare(HarmonyInstance instance) {
            Utils.Warn(RightClickMenu, "Loading AddJobGiverWorkOrders menu code: "
                       +Settings.useDeepStorageRightClickLogic);
            return Settings.useDeepStorageRightClickLogic;
        }
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(IntVec3 clickCell, Pawn pawn, List<FloatMenuOption> opts, bool drafted) {
            try {
                Log.Message("About to try....");
//                Patch_FloatMenuMakerMap.AJGWO.Invoke(null, new object[] {clickCell, pawn, opts, drafted});
            }
            catch (Exception e) {
                Log.Error("well, THAT failed: "+e);
                return true;
            }

            return true;          
            return false;
            return Patch_FloatMenuMakerMap.Prefix(Vector3.zero,clickCell,pawn,opts,true,drafted);
        }
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(List<FloatMenuOption> opts) {
            Log.Message("Skipping...");
            return;
            Patch_FloatMenuMakerMap.Postfix(opts);
        }
    }
    [StaticConstructorOnStartup]    
    static class Patch_FloatMenuMakerMap {
        static Patch_FloatMenuMakerMap() {
            if (AJGWO==null) {
                Log.Error("AJGWO is null :(");
                return;
            } else Log.Error("-----__About to test!");
            try {
                AJGWO.Invoke(null, new object[] {IntVec3.Invalid,null, new List<FloatMenuOption>(),false});
            } catch (Exception e) {
                Log.Error("Oops. Exception: "+e);
            }

            
        }
        
        static bool runningPatchLogic=false;
        static List<FloatMenuOption> realList=new List<FloatMenuOption>();
        static int failsafe=0;
        static Vector3 clickPos;
        
        // We have to run as Prefix, because we need to intercept the incoming List.
        public static bool Prefix(Vector3 clickPosition, IntVec3 c, Pawn pawn, List<FloatMenuOption> opts,
                                  bool runningAJGWO, bool drafted /*only if runningAJGWO*/) {
            Utils.Mess(RightClickMenu,""+(runningAJGWO?"AddJobGiverWorkOrders":"AddHumanlikeOrders")+
                       " called.  Currently "
                       +(runningPatchLogic?"":" not ")+"running special Patch Logic");
            if (runningAJGWO) return true;
            if (failsafe++>500) runningPatchLogic=false;
            if (runningPatchLogic) return true;
            // Only give nice tidy menu if items are actually in Deep Storage: otherwise, they
            //   are a jumbled mess on the floor, and pawns can only interact with what's on
            //   top until they've cleaned up the mess.
            // I *could* do better and throw away all items below, but whatev's this is good enuf.
            if (!runningAJGWO) {
                clickPos=clickPosition;
                c = IntVec3.FromVector3(clickPos);
            }
            if (((c.GetSlotGroup(pawn.Map)?.parent)as ThingWithComps)?.AllComps
                .FirstOrDefault(x=>x is IHoldMultipleThings.IHoldMultipleThings)==null) {
                Utils.Warn(RightClickMenu, "Location "+c+" is not in any DSU; continuing.");
                return true; // out of luck, so sorry!
                // Note: also need to handle this case in Postfix!
            }
            failsafe=0;

            Utils.Err(RightClickMenu, "Testing Location "+c);

            runningPatchLogic=true;

            // TODO: get default set of menus and tidy them away somehow?  This seems to be unnecessary so far.
            /************* Move all things away **************/
            // ThingsListAt:
            List<Thing> workingThingList=c.GetThingList(pawn.Map);
            List<Thing> origThingList=new List<Thing>(workingThingList);
            workingThingList.Clear();
            // ...other ...things.
            Dictionary<Thing, IntVec3> origPositions=new Dictionary<Thing, IntVec3>();
            TargetingParameters TPeverything=new TargetingParameters();
            TPeverything.canTargetBuildings=false; //already got it
            TPeverything.canTargetItems=false; //already got it
            TPeverything.canTargetFires=true; //??
            TPeverything.canTargetPawns=true;
            TPeverything.canTargetSelf=true;
            foreach (var localTargetInfo in GenUI.TargetsAt(clickPos, TPeverything)) {
                if (localTargetInfo.Thing==null) {
                    Log.Warning("LWM.DeepStorage: got null target but should only have things?");
                    continue;
                }
                origPositions.Add(localTargetInfo.Thing, localTargetInfo.Thing.Position);
                Utils.Warn(RightClickMenu, "Adding position information for LocalTargetInfo "
                           +localTargetInfo.Thing);
                SetPosition(localTargetInfo.Thing, IntVec3.Invalid);
            }
            /*****************  Do magic ****************/
            object[] origParams;
            if (runningAJGWO) {
                origParams=new object[] { c, pawn, opts, drafted };
            } else {
                origParams=new object[] { clickPos, pawn, opts };
            }
            foreach (var k in origPositions) {
                SetPosition(k.Key, k.Value);
                Utils.Mess(RightClickMenu, "  Doing Menu for Target "+k.Key);
                if (runningAJGWO)
                    AJGWO.Invoke(null, origParams);
                else
                    AHlO.Invoke(null, origParams);
                //showOpts(opts);
                SetPosition(k.Key, IntVec3.Invalid);
            }
            foreach (var t in origThingList) {
                workingThingList.Add(t);
                Utils.Mess(RightClickMenu, "  Doing Menu for Item "+t);
                AHlO.Invoke(null, origParams);
                //showOpts(opts);
                workingThingList.Remove(t);
            }

            /************ Cleanup: Put everything back! ***********/
            workingThingList.Clear();
            foreach (var t in origThingList) {
                workingThingList.Add(t);
            }
            foreach (var t in origPositions) {
                SetPosition(t.Key, t.Value);
            }
            runningPatchLogic=false;
            realList.Clear();
            foreach (var m in opts) {
                realList.Add(m); // got to store it in case anything adjusts it in a different Postfix
            }
            return false;
        } // end Prefix
        public static void Postfix(List<FloatMenuOption> opts) {
            if (runningPatchLogic) return;
            if (realList.Count == 0) return; // incidentally breaks out of logic here in case not in a DSU
            opts.Clear();
            foreach (var m in realList) opts.Add(m);
            realList.Clear();
            Utils.Mess(RightClickMenu, "Final Menu:\n    "+String.Join("\n    ",
                                                   opts.Select(o=>o.Label).ToArray()));
            //showOpts(opts);
        }

        /******************* Utility Functions *******************/

        /*private static void showOpts(List<FloatMenuOption> opts) { //Unused
            System.Text.StringBuilder s=new System.Text.StringBuilder(500);
            foreach (var m in opts) {
                s.Append("     ");
                s.Append(m.Label);
                s.Append("\n");
            }
            Log.Message(s.ToString());
        }*/
        // Allow directly setting Position of things.  And setting it back.
        public static void SetPosition(Thing t, IntVec3 p) {
            fieldPosition.SetValue(t, p);
        }
        /****************** Black Magic ***************/
        // Allow calling AddHumanlikeOrders
        static MethodInfo AHlO = typeof(FloatMenuMakerMap).GetMethod("AddHumanlikeOrders",
                                               BindingFlags.Static | BindingFlags.NonPublic);
        // Allow calling AddJobGiverWorkOrders
        static public MethodInfo AJGWO = typeof(FloatMenuMakerMap).GetMethod("AddJobGiverWorkOrders",
                                               BindingFlags.Static | BindingFlags.NonPublic);

        // Allow directly setting Position of things.  And setting it back.
        static FieldInfo fieldPosition=typeof(Thing).GetField("positionInt",
                                                              BindingFlags.Instance |
                                                              BindingFlags.GetField |
                                                              BindingFlags.SetField |
                                                              BindingFlags.NonPublic);
    } // end patch class    
}
