using System.Collections.Generic;
using RimWorld;
using Verse;
using Harmony;

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
    class PatchNoStorageBlockersIn {
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
            __result = false; // NoStorageBlockersIn returns false if there's a blocker
                              // Default to having a blocker unless EVERYTHING is okay
                              //  (We return false from this Patch function to skip original method)
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
                        Utils.Warn(NoStorageBlockerseIn, "  Checking 'mass' " + thing2.GetStatValue(cds.stat) +
                                   " vs running total " + totalAmountHereSoFar);
                        if (totalAmountHereSoFar > cds.limitingTotalFactorForCell &&
                            objInStack + 1 > cds.minNumberStacks) { // Must accept minimum (haven't incremented objInStack yet)
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
        static void Postfix(ref bool __result, IntVec3 c, Map map, Pawn carrier) {
            if (__result == false) return;
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
