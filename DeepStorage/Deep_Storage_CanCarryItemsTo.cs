using System;  // for delegate(), Type, Func<>() stuff
using System.Collections.Generic;
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
     * man to carry to Deep Storage
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
    [HarmonyPatch(typeof(RimWorld.StoreUtility), "NoStorageBlockersIn")]
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
            var maxStacks = cds.maxNumberStacks;
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
    // TODO: make an option
    //   TODO: Option could include NonHumanlikeOrWildMan OR AnimalOrWildMan
    //   maybe p.RaceProps.Animal or p.RaceProps.HumanLike
    [HarmonyPatch(typeof(StoreUtility), "IsGoodStoreCell")]
    class Patch_IsGoodStoreCell {
        public static Intelligence NecessaryIntelligenceToUseDeepStorage=Intelligence.Humanlike;
        // A way to specify some pawns can use Storage no matter what:
        static System.Func<Pawn,bool> specialTest=null;
        static bool Prepare(Harmony instance) {
            // Prepare: See if there are any mods that should be able to haul to storage even
            //   tho they don't meet normal criteria:
            if (!Settings.robotsCanUse) return true;
            Type classMiscRobots=null;
            Type classBaseRobots=null;
            if (ModLister.HasActiveModWithName("Misc. Robots")) {
                // From Haplo:  From my point of view they are normal drones with a kind of
                //    robot arm (for hauling) somewhere and a simple (job-specific) AI
                // Good enough for me!  A robot arm can manipulate things, an any AI that can
                // handle lifting random objects can probably handle latches.
                classMiscRobots=Type.GetType("AIRobot.X2_AIRobot, AIRobot");
                if (classMiscRobots==null) {
                    Log.Error("LWM's Deep Storage tried to find the Type 'AIRobot.X2_AIRobot, AIRobot', but failed even tho Misc. Robots is loaded.\n"+
                              "Please let LWM know.");
                } else {
                    Log.Message("LWM: activating compatibility logic for Misc. Robots");
                }
            }
            if (ModLister.HasActiveModWithName("Base Robots")) {
                classBaseRobots=Type.GetType("BaseRobot.ArcBaseRobot, BaseRobot");
                if (classBaseRobots==null) {
                    Log.Error("LWM's Deep Storage tried to find the Type 'BaseRobot.ArcBaseRobot, BaseRobot', but failed even tho Base Robots is loaded.\n"+
                              "Please let LWM know.");
                } else {
                    Log.Message("LWM: activating compatibility logic for Base Robots");
                }
            }
            if (classMiscRobots != null) {
                if (classBaseRobots != null) { // Are these even compatible?  Someone will try
                    specialTest=delegate(Pawn p) {
                        if (classMiscRobots.IsAssignableFrom(p?.def.thingClass)) return true;
                        return classBaseRobots.IsAssignableFrom(p?.def.thingClass);
                    };
                } else { // only MiscRobots
                    specialTest=delegate(Pawn p) {
                        return classMiscRobots.IsAssignableFrom(p?.def.thingClass);
                    };
                }
            } else if (classBaseRobots != null) {
                    specialTest=delegate(Pawn p) {
                        return classBaseRobots.IsAssignableFrom(p?.def.thingClass);
                    };
            }
            return true; // I have too much to do to look up whether Prepare(...) can be a void, so return true
        }
        static void Postfix(ref bool __result, IntVec3 c, Map map, Pawn carrier) {
            if (__result == false) return;
            if (specialTest !=null && specialTest(carrier)) return; // passes specialTest?
            if (carrier?.RaceProps == null) return;
            if (carrier.RaceProps.intelligence >= NecessaryIntelligenceToUseDeepStorage)
                return; // smart enough to use whatever.
            // okay, potentially need to see if we're looking at deep storage after all:
            if (LWM.DeepStorage.Utils.CanStoreMoreThanOneThingAt(map, c)) {
                __result = false;
            }
            return;
        }
    }


}
