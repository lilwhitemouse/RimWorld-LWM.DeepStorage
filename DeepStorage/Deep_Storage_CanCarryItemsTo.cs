using System;  // for delegate(), Type, Func<>() stuff
using System.Collections.Generic;
using System.Reflection.Emit;
using RimWorld;
using Verse;
using HarmonyLib;

using static LWM.DeepStorage.Utils.DBF; // debug trace


/******************************************
 * A set of patches to see whether items
 * can be carried to Deep Storage
 *
 * Allowing multiple items to be carried to
 * the same square (via the all important
 * NoStorageBlockersIn), and then restricting
 * who can carry there (also IsGoodStoreCell)
 */





namespace LWM.DeepStorage
{
    /****** See also Deep_Storage_Jobs.cs, which patches the
     * HaulToCellStorage Job to get the correct count of how
     * many to carry to Deep Storage
     */

    /**********************************************************************
     * RimWorld/StoreUtility.cs:  NoStorageBlockersIn(...)
     * This allows the hauling AI to see a partly-filled deep storage area as open/empty
     *
     * We patch via prefix by first checking if the slotGroup in question is part of a
     * Deep Storage unit.  If it is, then we take over (and duplicate a little bit of code)
     *
     * Possible ways to make this better:
     *   Use Transpiler to inject a variable maxStacks=1 into the original
     *   Update maxStacks if it's a DeepStorage object
     *   Check maxStacks in a loop.
     * This was easier.
     **************************************/
     //todo1.4: OMG, this all goes away:D
//todo1.4    [HarmonyPatch(typeof(RimWorld.StoreUtility), "NoStorageBlockersIn")]
    class Patch_NoStorageBlockersIn {
        protected static bool Prefix(IntVec3 c, Map map, Thing thing, ref bool __result) {
            Utils.Err(NoStorageBlockerseIn, "Looking for blockers for " + thing + " at " + c);
            // Check if storage location is in an uber-storage building:
            SlotGroup slotGroup = c.GetSlotGroup(map);
            CompDeepStorage cds = null;
            if (slotGroup == null || !(slotGroup?.parent is ThingWithComps) ||
                (cds = (slotGroup.parent as ThingWithComps).TryGetComp<CompDeepStorage>()) == null) {
                //                Log.Warning("  ...letting vanilla handle it.");
                return true; // normal spot, NoStorageBlockersIn() will handle it
            }
            //TODO: Make this IHoldMultipleThings
//            __result = false; // NoStorageBlockersIn returns false if there's a blocker
                              // Default to having a blocker unless EVERYTHING is okay
                              //  (We return false from this Patch function to skip original method)
            __result=cds.StackableAt(thing, c, map);
            Utils.Warn(NoStorageBlockerseIn, "Final result for "+thing+" at "+c+": "+__result);
            return false;
#if false
            // If there is a maximum size of items that will fit in the unit, quit:
            if (cds.limitingFactorForItem > 0f) {
                if (thing.GetStatValue(cds.stat) > cds.limitingFactorForItem) {
                    Utils.Warn(NoStorageBlockerseIn, "Thing has " + cds.stat + " of " +
                        thing.GetStatValue(StatDefOf.Mass) + " but max allowed is " +
                        cds.limitingFactorForItem);
                    return false;
                }
            }
            // We will usually care how many stacks can fit here:
            var maxStacks = cds.MaxNumberStacks;
            // If maxTotalMass is set, we will keep track of how much "room" we have as well:
            float totalAmountHereSoFar=0f;
            if (cds.limitingTotalFactorForCell > 0f) {
                totalAmountHereSoFar = thing.GetStatValue(cds.stat);
            }

            var objInStack = 0;
            List<Thing> list = map.thingGrid.ThingsListAt(c);
            for (int i = 0; i < list.Count; i++) { //loop thru cell's contents
                Thing thing2 = list[i];
                Utils.Warn(NoStorageBlockerseIn, "  ...checking: does " + thing2 + " block?");
                if (thing2.def.EverStorable(false)) { // an "item" as it were
                    if (cds.limitingTotalFactorForCell > 0f) {
                        totalAmountHereSoFar += (thing2.GetStatValue(cds.stat)*thing2.stackCount);
                        Utils.Warn(NoStorageBlockerseIn, "  added 'mass' " + thing2.GetStatValue(cds.stat) +
                                   " to running total " + totalAmountHereSoFar + " / " +cds.limitingTotalFactorForCell
                                   + "(" + (objInStack+1)+" v "+cds.minNumberStacks+")");
                        if (totalAmountHereSoFar > cds.limitingTotalFactorForCell &&
                            objInStack + 1 >= cds.minNumberStacks) { // Must accept minimum (haven't incremented objInStack yet)
                                                                     // but if reached minimum and over capacity, cannot store here
                            Utils.Warn(NoStorageBlockerseIn, "  BLOCKS: Over mass limit ("
                                               + cds.limitingTotalFactorForCell + ")");
                            return false;
                        }
                    }
                    if (!thing2.CanStackWith(thing)) {
                        objInStack++;
                    } else if (thing2.stackCount >= thing.def.stackLimit) {
                        objInStack++;
                    } else // it can stack and there's room in the stack for more...
                      { // go ahead and get out of here with the good news!
                        __result = true;
                        Utils.Warn(NoStorageBlockerseIn, thing.ToString() + " at " + c.ToString() + ": " + __result);
                        return false;
                    }
                    if (objInStack >= maxStacks) { return false; }
                    continue;
                }
                if (thing2.def.entityDefToBuild != null && thing2.def.entityDefToBuild.passability != Traversability.Standable) {
                    Utils.Warn(NoStorageBlockerseIn, thing.ToString() + " at " + c.ToString() + ": " + __result);
                    return false;
                }
                if (thing2.def.surfaceType == SurfaceType.None && thing2.def.passability != Traversability.Standable) {
                    Utils.Warn(NoStorageBlockerseIn, thing.ToString() + " at " + c.ToString() + ": " + __result);
                    return false;
                }
            }
            //You know what I can't get running in Linux?  Monodevelop's debugger.
            //Log.Warning("No storage blockers for "+thing.ToString()+" in "+slotGroup.ToString());
            __result = true; // no blockers after all!
            Utils.Warn(NoStorageBlockerseIn, thing.ToString() + " at " + c.ToString() + ": " + __result);
            return false;
#endif
        }
    }





    /******************************************************
     * Pets cannot manage Deep Storage
     *   (rationale: organizing is too advanced for them)
     *
     * StoreUtility.cs's IsGoodStoreCell
     * Add a simple Postfix to check if a nonhuman is
     * hoping to carry to Deep Storage, and if so, don't
     * allow it.
     */
    // TODO: transpile?  faster, surely?
    // Other options for who is allowed to use could include
    //   NonHumanlikeOrWildMan OR AnimalOrWildMan
    //   maybe p.RaceProps.Animal or p.RaceProps.HumanLike
    // Set to ToolUser for 1.4, as mechanoid carriers are now
    //   a vanilla thing.
    [HarmonyPatch(typeof(StoreUtility), "IsGoodStoreCell")]
    class Patch_IsGoodStoreCell
    {
        // This is modified by mod settings on startup (and when changed):
        public static Intelligence NecessaryIntelligenceToUseDeepStorage = Intelligence.ToolUser;
        // A way to specify some pawns can use Storage no matter what:
        static System.Func<Pawn, bool> specialTest = null;
        // Prepare: Check to see if there are any robot/drone mods - if there are,
        //   prepare special logic to allow them to haul to storage no matter what:
        static bool Prepare(Harmony instance)
        {
            if (!Settings.robotsCanUse || specialTest != null) return true;

            var types = new List<Type>();

            Type tmp;

            // General procedure to get "close enough" classes:
            //   "NameSpace.ClassName, AssemblyName(=.dllName)"
            if (ModLister.HasActiveModWithName("Misc. Robots"))
            {
                // From Haplo:  From my point of view they are normal drones with a kind of
                //    robot arm (for hauling) somewhere and a simple (job-specific) AI
                // Good enough for me!  A robot arm can manipulate things, an any AI that can
                // handle lifting random objects can probably handle latches.
                tmp = Type.GetType("AIRobot.X2_AIRobot, AIRobot");
                if (tmp == null)
                {
                    Log.Error("LWM's Deep Storage tried to find the Type 'AIRobot.X2_AIRobot, AIRobot', but failed even tho Misc. Robots is loaded.\n" +
                              "Please let LWM know.");
                }
                else
                {
                    Log.Message("LWM: activating compatibility logic for Misc. Robots");
                    types.Add(tmp);
                }
            }
            if (ModLister.HasActiveModWithName("Base Robots"))
            {
                tmp = Type.GetType("BaseRobot.ArcBaseRobot, BaseRobot");
                if (tmp == null)
                {
                    Log.Error("LWM's Deep Storage tried to find the Type 'BaseRobot.ArcBaseRobot, BaseRobot', but failed even tho Base Robots is loaded.\n" +
                              "Please let LWM know.");
                }
                else
                {
                    Log.Message("LWM: activating compatibility logic for Base Robots");
                    types.Add(tmp);
                }
            }
            if (ModLister.HasActiveModWithName("Project RimFactory Revived") ||
                ModLister.HasActiveModWithName("Project RimFactory Lite"))
            { // They use the same .dll
                tmp = Type.GetType("ProjectRimFactory.Drones.Pawn_Drone, ProjectRimFactory");
                if (tmp == null)
                {
                    Log.Error("LWM's Deep Storage tried to find the Type 'ProjectRimFactory.Drones.Pawn_Drone', but failed even tho PRF is loaded.\n" +
                              "Please let LWM know.");
                }
                else
                {
                    Log.Message("LWM: activating compatibility logic for Project RimFactory");
                    types.Add(tmp);
                }
            }

            if (types.Count == 0) return true;
            // The fun part:
            //   Built a function from IL to test if a pawn is a robot:
            var dm = new DynamicMethod("Check if Pawn is a robot",
                                       typeof(bool),
                                       new Type[] { typeof(Pawn) });
            var il = dm.GetILGenerator();
            // Build from IL:
            //     if p is t1 goto isRobotLabel;
            //     if p is t2 goto isRobotLabel; //maybe
            //     ...
            //     return false;
            //    isRobotLabel:
            //     return true;
            var isRobotLabel = il.DefineLabel();
            foreach (var t in types)
            {
                // test (p is t)
                il.Emit(OpCodes.Ldarg_0); // put pawn p on stack
                il.Emit(OpCodes.Isinst, t);
                // Note: cannot return result of Isinst as a bool value
                //   (maybe I could cast it?  Whatever)
                il.Emit(OpCodes.Brtrue, isRobotLabel);
            }
            // return false
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);
            il.MarkLabel(isRobotLabel);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Ret);

            specialTest = (System.Func<Pawn, bool>)dm.CreateDelegate(typeof(System.Func<Pawn, bool>));
            return true; // I have too much to do to look up whether Prepare(...) can be a void, so return true
        }
        static void Postfix(ref bool __result, IntVec3 c, Map map, Pawn carrier)
        {
            if (__result == false) return;
            if (specialTest != null && specialTest(carrier)) return; // passes specialTest?
            if (carrier?.RaceProps == null) return;
            if (carrier.RaceProps.intelligence >= NecessaryIntelligenceToUseDeepStorage)
                return; // smart enough to use whatever.
            // okay, potentially need to see if we're looking at deep storage after all:
            if (LWM.DeepStorage.Utils.CanStoreMoreThanOneThingAt(map, c))
            {
                __result = false;
            }
            return;
        }
    }


}
