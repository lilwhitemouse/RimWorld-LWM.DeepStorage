using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using HarmonyLib;
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
      // and when AddUndraftedOrders->AddJobGiverWorkOrders is run, // not doing this now
      //    It tended to cause crashing etc and didn't gain much?
        Prefix runs
        Prefix sets flag

        Move All Items Away
        (Get basic default orders?)
        For Each Thing
          Move Thing Back
          Call AHlO/AJGWO - only calling AddHumanlikeOrders right now?
          flag is set so runs normally
          Move Thing Away
        Move Things Back
        Combine menu
        return false
      Postfix runs and catches logic, puts together complete, correct menu option list
      So...look, we do the same thing twice!  Function calls!
    */
    //todo1.4 - omg omg omg let's see what happens
    //[HarmonyPatch(typeof(RimWorld.FloatMenuMakerMap), "AddHumanlikeOrders")]
    static class Patch_AddHumanlikeOrders {
        static public bool runVanillaAHlO=false; // should Vanilla(+mods) AddHumanlikeOrders run
        static public bool blockAHlONonItems=false; // if it does run, should TargetsAt() be blocked
        static List<FloatMenuOption> listOfOptionsWeFilled=null; // out master list; see below

        // Thing => where it was originally from before it was stolen away to faerie:
        //   Note: this will only contain weird things...like Pawns walkig past.  or
        //   maybe fire.  I'm not really sure how the fire thing works.  Clearly the
        //   thing to do is to test it on a big shelf of artillery shells.
        static public Dictionary<Thing, IntVec3> thingsInRealmOfFaerie=new Dictionary<Thing,IntVec3>();

        //// Utility dynamic functions to access private fields quickly:
        // set a Thing's positionInt directly (fast):
        static public Action<Thing,IntVec3> SetPosition; // created in Prepare
        // call FloatMenuMakerMap's private AddHumanlikeOrders (fast):
        static public Action<Vector3,Pawn,List<FloatMenuOption>> AHlO; // created in Prepare
        // get the ThingGrid's actual thingGrid for direct manipulation
        //   (reflection, so slightly slower, but only run once, then cached)
        private static readonly FieldInfo thingGridThingList = HarmonyLib.AccessTools.Field(typeof(ThingGrid), "thingGrid");
        private static List<Thing>[] cachedThingGridThingList=null;
        private static Map mapOfCachedThingGrid=null;
        // List<Thing> that is kept empty but used with thingGrid's ThingListAt for building menu:
        private static List<Thing> tmpEmpty=new List<Thing>();

        // Use this to prepare some "dynamic functions" we will use
        //   (for faster performance, b/c apparently the reflection
        //   is sloooooow.  From what i hear.)
        static bool Prepare(Harmony instance) {
            if (ModLister.GetActiveModWithIdentifier("netrve.dsgui")!=null) return false;
            /* Build a dynamic method to do:
             * void RePosition(Thing t, IntVec3 pos) {
             *   t.positionInt=pos;  // directly set internal private field
             * }
             *
             * Use this approach for speed.
             */
            DynamicMethod dm = new DynamicMethod("directly set thing's positionInt",
                                        null, // return type void
                                        new Type[] {typeof(Thing), typeof(IntVec3)},
                                        true // skip JIT visibility checks - which is whole point
                                             //   we want to access a private field!
                );
            ILGenerator il=dm.GetILGenerator();
            // build our function from IL.  Because why not
            il.Emit(OpCodes.Ldarg_0);//put Thing on stack
            il.Emit(OpCodes.Ldarg_1);//put position on stack
            // store field:
            il.Emit(OpCodes.Stfld, typeof(Thing).GetField("positionInt",
                                                          BindingFlags.Instance |
                                                          BindingFlags.GetField |
                                                          BindingFlags.SetField |
                                                          BindingFlags.NonPublic));
            il.Emit(OpCodes.Ret); //don't forget
            // Create the delegate that completes the dynamic method:
            //   (I'm just quoting the MSIL documentation, I don't
            //    actually know what I'm doing)
            SetPosition=(Action<Thing,IntVec3>)dm.CreateDelegate(typeof(Action<,>).MakeGenericType(typeof(Thing), typeof(IntVec3)));
            /*****     Now do the same for AHlO - call the private method directly     *****/
            dm=new DynamicMethod("directly call AddHumanlikeOrders",
                                 null, // return type void
                                 new Type[] {typeof(Vector3), typeof(Pawn), typeof(List<FloatMenuOption>)},
                                 true // skip JIT visibility checks
                );
            il=dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Call, typeof(FloatMenuMakerMap).GetMethod("AddHumanlikeOrders",
                                                       BindingFlags.Static | BindingFlags.NonPublic));
            il.Emit(OpCodes.Ret);
            AHlO=(Action<Vector3,Pawn,List<FloatMenuOption>>)dm
                .CreateDelegate(typeof(Action<,,>).MakeGenericType(typeof(Vector3), typeof(Pawn), typeof(List<FloatMenuOption>)));

            return true;
            Utils.Warn(RightClickMenu, "Loading AddHumanlikeOrders menu code: "
                       +Settings.useDeepStorageRightClickLogic);




            return Settings.useDeepStorageRightClickLogic;
        }
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts) {
            if (!Settings.useDeepStorageRightClickLogic) return true;
//                return Patch_FloatMenuMakerMap.Prefix(clickPos,IntVec3.Invalid,pawn,opts,false,false);

            // if expicitly told to run vanilla, run vanilla.
            //   (note vanilla may have any number of mods attached)
            if (runVanillaAHlO) {
                Utils.Warn(RightClickMenu, "-------Running Vanilla AddHumanlikeOrders"+pawn+"-------");
                return true;
            }
            // if not in storage, don't worry about it:
            IntVec3 clickCell=IntVec3.FromVector3(clickPos);
            if (!Utils.CanStoreMoreThanOneThingAt(pawn.Map, clickCell)) {
                Utils.Warn(RightClickMenu, "-----Running Vanilla AddHumanlikeOrders"+pawn+" - not in storage-----");
                return true;
            }
            Utils.Warn(RightClickMenu, "-----Running Custom AddHumanlikeOrders for "+pawn+"-----");
            // We will fill listOfOptionsWeFilled - this lets us properly handle other mods'
            //   right click modifications (we will use listOfOptionsWeFilled in our postfix
            //   to provide the actual result). In the meantime, we use listOfOptions as our
            //   list - we will move everything over at the end.
            // Why?  Because we have to move over our final result in the Postfix
            List<FloatMenuOption> listOfOptions=new List<FloatMenuOption>();
            // Prepare Faerie to accept pawns/etc.
            thingsInRealmOfFaerie.Clear();

            // get menu with no items.  This will include commands such as "clean dirt."  I think.
            // and dealing with fire.  I think.  And pawns.  Definitely dealing with pawns.

            /*var index = pawn.Map.cellIndices.CellToIndex(cpos.ToIntVec3());
var listArray = (List<Thing>[]) thingListTG.GetValue(pawn.Map.thingGrid);
var origList = listArray[index];

listArray[index] = new List<Thing> {thingList[i]};
rows[i] = new DSGUI_ListItem(pawn, thingList[i], cpos, boxHeight);
listArray[index] = origList;
*/
            //   Clear thingsList
            if (mapOfCachedThingGrid!=pawn.Map) {
                mapOfCachedThingGrid=pawn.Map;
                cachedThingGridThingList=(List<Thing>[]) thingGridThingList.GetValue(pawn.Map.thingGrid);
            }
            var index=pawn.Map.cellIndices.CellToIndex(clickCell);
            List<Thing> origList=cachedThingGridThingList[index];
            cachedThingGridThingList[index]=tmpEmpty;

            runVanillaAHlO=true;
            Utils.Mess(RightClickMenu, "Get menu with no items: invoke vanilla:");
//            List<Thing> thingList=clickCell.GetThingList(pawn.Map);
//            List<Thing> tmpList=new List<Thing>(thingList);
//            thingList.Clear();
            AHlO(clickPos,pawn,listOfOptions);
            /* var origParams=new object[] {clickPos, pawn, listOfOptions};
            AHlO.Invoke(null, origParams);*/
            runVanillaAHlO=false;
            // return things
            cachedThingGridThingList[index]=origList;
            Utils.Mess(RightClickMenu, "  ->"+String.Join("; ", listOfOptions));
//            thingList.AddRange(tmpList);
//            ReturnThingsFromFaerie();
            // get sorted list of all things in dsu
            List<Thing> allThings=clickCell.GetSlotGroup(pawn.Map).HeldThings.OrderBy(t=>t.LabelCap).ToList();
            // Multilpe Options here, depending on size of allThings.
            //   6 or fewer => should just display each set of options
            //   7-12       => show entry for each, with options
            //   LOTS       => open f***ing window w/ search options, etc.
            // for each thing(mod label) in storage, add menu entry
            // ...for now, just print them all:
            for (int i=0; i<allThings.Count; i++) {
                Thing t=allThings[i];
/*                int howManyOfThis=1;
                while (i<allThings.Count-1 && t.Label==allThings[i+1].Label) {
                    howManyOfThis++;
                    i++;
                }
                string label;
                if (howManyOfThis==1)
                    label=t.Label;
                else
                    label=howManyOfThis.ToString()+" x "+t.Label;//TODO: translate
                //TODO: what if no option for this item????
                */
                GeneratePawnOptionsFor(pawn, t, listOfOptions);
//                listOfOptions.Add(MakeMenuOptionFor(pawn, t, howManyOfThis));
            }
            listOfOptionsWeFilled=listOfOptions;
            #if DEBUG
            if (Utils.showDebug[(int)RightClickMenu])
                for (int ijk=0; ijk<1000; ijk++)
                    Log.Message("pausing log...");
            #endif
            return false;
            //return Patch_FloatMenuMakerMap.Prefix(clickPos,IntVec3.Invalid,pawn,opts,false,false);
        }
        [HarmonyPriority(HarmonyLib.Priority.Last)]
        static void Postfix(List<FloatMenuOption> opts) {
            if (!Settings.useDeepStorageRightClickLogic) {
                Patch_FloatMenuMakerMap.Postfix(opts);
                return;
            }
            if (listOfOptionsWeFilled!=null) {
                opts.Clear();
                opts.AddRange(listOfOptionsWeFilled);
                listOfOptionsWeFilled=null;
            }
            return;
        }
        static FloatMenuOption MakeMenuOptionFor(Pawn p, Thing t, int count) {
            // Phase one: simple menu of menus
            Utils.Mess(RightClickMenu, "  Creating Menu Option for "+count+" "+t);
            var label=(count>1?count.ToString()+" x "+t.Label:t.Label);
            return new FloatMenuOption(label, delegate() {
                    #if DEBUG
                    if (Utils.showDebug[(int)RightClickMenu]) Log.ResetMessageCount();
                    #endif
                    Utils.Warn(RightClickMenu, "Opening menu for "+p+" using "+t);
                    var menu=GetFloatMenuFor(p,t);
                    if (menu!=null)
                        Find.WindowStack.Add(menu);
                    #if DEBUG
                    if (Utils.showDebug[(int)RightClickMenu])
                        for (int ijk=0; ijk<1000; ijk++)
                            Log.Message("pausing log");
                    #endif
                },
                t.def);
        }
        static FloatMenu GetFloatMenuFor(Pawn p, Thing t) {
            List<FloatMenuOption> fmo=new List<FloatMenuOption>();
            GeneratePawnOptionsFor(p, t, new List<FloatMenuOption>());
            return new FloatMenu(fmo);
        }
//        public static List<FloatMenuOption> floatingList=new List<FloatMenuOption>();
        static void GeneratePawnOptionsFor(Pawn p, Thing t, List<FloatMenuOption> opts) {
            Utils.Mess(RightClickMenu, "Generating Options for "+p+" using "+t);
//            if (!t.Spawned) return null;
            if (!t.Spawned) return;
            // if not cached, build
            Map map=p.Map;
            /*** Step 1: Clear ThingsList ***/
            var index=map.cellIndices.CellToIndex(t.Position);
            if (mapOfCachedThingGrid!=map) {
                mapOfCachedThingGrid=map;
                cachedThingGridThingList=(List<Thing>[]) thingGridThingList.GetValue(map.thingGrid);
            }
            List<Thing> origList=cachedThingGridThingList[index];
            cachedThingGridThingList[index]=tmpEmpty;
            // Add the thing we are interested in to the new thinglist:
            tmpEmpty.Add(t); // (remember to empty afterwards)

/*            IntVec3 c=t.Position;
            var list=t.Map.thingGrid.ThingsListAtFast(c);
            foreach (Thing bye in list) {
                if (bye==t) continue;
                SendToFaerie(bye);
            }
            // get rid of non-item things (e.g., pawns?)
            */

            //XXX:
            // We are doing UI stuff, which often uses the click-position
            //    instead of the map position, so that's annoying and may
            //    be risky, but I'm not sure there's much choice. We also
            //    grab everything via TargetingParameters, which includes
            //    all the Things on the ground as well as pawns, fire, &c
            Vector3 clickPos=t.Position.ToVector3();
            TargetingParameters TPeverything=new TargetingParameters();
            TPeverything.canTargetBuildings=true; // be thorough here
            TPeverything.canTargetItems=false; // We already moved the thingsList
            TPeverything.canTargetFires=true; // not that pawns SHOULD be able to grab things from burning lockers...
            TPeverything.canTargetPawns=true;
            TPeverything.canTargetSelf=true; // are we sure?

            foreach (LocalTargetInfo anotherT in GenUI.ThingsUnderMouse(clickPos,
                                                                        0.8f /*see TargetsAt*/, TPeverything)) {
                Thing tmpT=anotherT.Thing;
                if (tmpT!=null && tmpT!=t) {
//                    Utils.Mess(RightClickMenu, "  moving away "+tmpT);
//                    SendToFaerie(tmpT);
                }
            }
            /*var index = pawn.Map.cellIndices.CellToIndex(cpos.ToIntVec3());
var listArray = (List<Thing>[]) thingListTG.GetValue(pawn.Map.thingGrid);
var origList = listArray[index];

listArray[index] = new List<Thing> {thingList[i]};
rows[i] = new DSGUI_ListItem(pawn, thingList[i], cpos, boxHeight);
listArray[index] = origList;
*/
            // get orders for Thing t!
//            List<Thing> thingList=t.Position.GetThingList(t.Map);
//            List<Thing> tmpList=new List<Thing>(thingList);
//            thingList.Clear();
//            thingList.Add(t);
            runVanillaAHlO=true;
            blockAHlONonItems=true;
//            floatingList.Clear();
            Utils.Mess(RightClickMenu, "  running vanilla (but target-blocked) AHlO");
            AHlO(clickPos,p,opts);
            blockAHlONonItems=false;
//            AHlO(clickPos,p,floatingList);
//            AHlO.Invoke(null, new object[] { clickPos, p, floatingList}); //todo
            runVanillaAHlO=false;
            // Restore Things:
            Utils.Mess(RightClickMenu, "  returning things");
            ReturnThingsFromFaerie();
            // Restor ThingList:
            cachedThingGridThingList[index]=origList;
            tmpEmpty.Clear(); // clean up after ourselves!
//            thingList.Clear();
//            thingList.AddRange(tmpList);
//      Log.Warning("List size: "+floatingList.Count);
//            return floatingList;
//            Log.Warning("List size: "+opts.Count);
        }
        [HarmonyPriority(Priority.Last)]
        public static void PostfixXXX(List<FloatMenuOption> opts) {
            Patch_FloatMenuMakerMap.Postfix(opts);
        }
        public static void SendToFaerie(Thing t) {
            thingsInRealmOfFaerie[t]=t.Position;
            SetPosition(t, IntVec3.Invalid);
        }
        public static void ReturnThingsFromFaerie() {
            foreach (KeyValuePair<Thing, IntVec3> kvp in thingsInRealmOfFaerie) {
                //kvp.Key.Position=kvp.Value;
                SetPosition(kvp.Key, kvp.Value);
            }
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var genUITargetsAt = HarmonyLib.AccessTools.Method(typeof(Verse.GenUI), "TargetsAt");
            var ourTargetsAt = HarmonyLib.AccessTools.Method(typeof(Patch_AddHumanlikeOrders), "OurTargetsAt");
            foreach (CodeInstruction c in instructions) {
                if (c.opcode==OpCodes.Call && (MethodInfo)c.operand == genUITargetsAt)
                    yield return new CodeInstruction(OpCodes.Call, ourTargetsAt);
                else yield return c;
            }
        }
        public static IEnumerable<LocalTargetInfo> OurTargetsAt(Vector3 clickPos,
                                                                TargetingParameters clickParams, 
                                                                bool thingsOnly = false,
                                                                ITargetingSource source = null) {
            if (blockAHlONonItems) return Enumerable.Empty<LocalTargetInfo>();//yield break;
            return GenUI.TargetsAt(clickPos, clickParams, thingsOnly, source);
        }
        // Allow directly setting Position of things.  And setting it back.
/*        public static void SetPosition(Thing t, IntVec3 p) {
            fieldPosition.SetValue(t, p);
        }*/
        /****************** Black Magic ***************/
        // Allow calling AddHumanlikeOrders
        static MethodInfo AHlO_old = typeof(FloatMenuMakerMap).GetMethod("AddHumanlikeOrders",
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


    }
    #if false
    static class Patch_AddHumanlikeOrders_Orig {
        static bool Prepare(Harmony instance) {
            if (ModLister.GetActiveModWithIdentifier("netrve.dsgui")!=null) return false;
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
    #endif
    #if false
//    [HarmonyPatch(typeof(FloatMenuMakerMap), "AddJobGiverWorkOrders")]
    static class Patch_AddJobGiverWorkOrders {
        static bool Prepare(HarmonyInstance instance) {
            if (ModLister.GetActiveModWithIdentifier("netrve.dsgui")!=null) return false;
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
    #endif
    static class Patch_FloatMenuMakerMap {
        static bool runningPatchLogic=false;
        static List<FloatMenuOption> realList=new List<FloatMenuOption>();
        static int failsafe=0;
        static Vector3 clickPos;
        public static bool Prepare() {
            if (ModLister.GetActiveModWithIdentifier("netrve.dsgui")!=null) return false;
            return true;
        }

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
