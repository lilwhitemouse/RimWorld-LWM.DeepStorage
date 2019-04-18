using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Linq;
using Harmony;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using UnityEngine;
using static LWM.DeepStorage.Utils.DBF; // trace utils

namespace LWM.DeepStorage
{
    ///////////////////////////////////////////////////////////////////////
    /// Deep Storage:
    /// 
    /// So many things need to be added or patched to allow more than one object 
    ///    in Deep Storage units:
    /// 1. Deep_Storage.cs:
    ///    Adds CompProperties LWM.DeepStorage.Properties
    ///      and assosciated ThingComp CompDeepStorage
    ///    also some utility functions
    /// 2. Deep_Storage_CanCarryItemsTo.cs
    ///    Patches RimWorld.StoreUtility's NoStorageBlockersIn - no longer block
    ///    if the storage unit isn't full yet
    ///    Also, disallow pets from hauling to Deep Storage Units
    /// 3. Deep_Storage_Jobs.cs
    ///    Verse.AI.HaulAIUtility's HaulToCellStorageJob - let pawns know how many
    ///    of stackable things (e.g., wood) they can carry to deep storage
    /// 4. Deep_Storage_Pause.cs
    ///    Verse/AI/Toils_Haul.cs's Toil PlaceHauledThingInCell(...) - 
    ///    change the toil to having a bit of a wait while putting into a DSU
    /// 5. Deep_Storage_PutInto.cs
    ///    Verse.GenPlace's TryPlaceDirect - allow putting down stuff
    ///      in deep storage, even if there's already stuff there.
    ///      also, tidy up stackable stuff
    ///    Verse.GenSpawn's Spawn - allow putting 2 or more items in the same place
    /// 6. Deep_Storage_TakeFrom.cs
    ///    Verse.Ai.Toil_Haul's StartCarryThing - the delegate() function, so
    ///    Pawns picking up 50 of something from Deep Storage pick up more than
    ///    just the 7 in the top stack. (otherwise, they have reserved the spot
    ///    so can't get anything else lying there)
    /// 7. Deep_Storage_SaveLoad.cs
    ///    And then there is the loading, which requires 3 more patches to make sure
    ///    items can spawn on top of each other...and not pretty patches.  Why?  B/c
    ///    buildings spawn after items do...so checking if an item is in DeepStorage
    ///    isn't possible during game load. TODO: Bug still exists here
    /// 8. Deep_Storage_UI.cs
    ///    Because no one wants to click 9 times to finally select the building.
    /// 9. ModCompatibility.cs
    ///    Makes Deep Storage play well with:
    ///      RimWorld Search Agency (Hauling Hysteresis)
    ///////////////////////////////////////////////////////////////////////

    /***********************************************************************/

    public class Utils {
        static bool[] showDebug ={
            true,  // "Testing" will always be true
            
            false, // No Storage Blockers In
            false, // Haul To Cell Storage Job
            false, // Try Place Direct
            false, // Spawn (edit directly?)
            false, // Tidy Stacks Of
            false, // Deep_Storage_Job
            false, // Place Hauled Thing In Cell (wait functionaliy)
        };

        public enum DBF // DeBugFlag
        {
            Testing, NoStorageBlockerseIn, HaulToCellStorageJob, TryPlaceDirect, Spawn, TidyStacksOf,
            Deep_Storage_Job, PlaceHauledThingInCell
        }

        // Nifty! Won't even be compiled in if not DEBUG
        [System.Diagnostics.Conditional("DEBUG")]
        public static void Warn(DBF l, string s) {
            if (showDebug[(int)l])
                Log.Warning("LWM." + l.ToString() + ": " + s);
        }
        [System.Diagnostics.Conditional("DEBUG")]
        public static void Err(DBF l, string s) {
            if (showDebug[(int)l])
                Log.Error("LWM." + l.ToString() + ": " + s);
        }

        // This gets checked a lot.  Sometimes the test is done in-place (if will 
        //   need to use the slotGroup later, for example), but when using Harmony 
        //   Transpiler, tests are easier via function call
        // Most of the bulk here is debugging stuff
        public static bool CanStoreMoreThanOneThingAt(Map map, IntVec3 loc) {
            SlotGroup slotGroup = loc.GetSlotGroup(map);
            if (slotGroup == null || !(slotGroup?.parent is ThingWithComps) ||
                (slotGroup.parent as ThingWithComps).TryGetComp<CompDeepStorage>() == null)
            {
                return false;
                #pragma warning disable CS0162 // Unreachable code detected
                Log.Warning("CanStoreMoreThanOneThingAt: " + loc + "? false");
                return false;
                if (slotGroup == null) Log.Warning("  null slotGroup");
                else if (slotGroup.parent == null) Log.Warning("  null slotGroup.parent");
                else if (!(slotGroup.parent is ThingWithComps)) Log.Warning("  slotGroup.parent is not ThingWithComps");
                else Log.Warning("  no CompDeepStorage");
                Log.Warning("Just for the record, " + (Scribe.mode == LoadSaveMode.LoadingVars) +
                            (Scribe.mode == LoadSaveMode.PostLoadInit) +
                            Scribe.mode);
                List<Thing> l = map.thingGrid.ThingsListAt(loc);
                foreach (Thing t in l)
                {
                    Log.Error("Did find a " + t.ToString() + " here at " + loc.ToString());
                }
                return false;
            }
            //            Log.Warning("CanStoreMoreThanOneThingAt: " + loc.ToString() + "? true");
            return true;
            Log.Warning("CanStoreMoreThanOneThingAt: " + loc.ToString() + "? true!");
            List<Thing> lx = map.thingGrid.ThingsListAt(loc);
            foreach (Thing t in lx)
            {
                Log.Error("Did find a " + t.ToString() + " here at " + loc.ToString());
            }
            return true;
            #pragma warning restore CS0162 // Unreachable code detected
        }

        // Sometimes it's very important to not have 3 stacks of Brick with
        //   5 in one stack, 72 in another, and 45 in the last.
        // This will likely get called any time Pawns put something into Deep Storage
        //   - tidying is part of the time cost of using it!
        // Note that this ignores all other stacks (e.g., Wheat, Wood, &c);
        //   if that's ever needed, will have to add it.
        public static void TidyStacksOf(Thing thing)
        {
            if (thing == null || !thing.Spawned || thing.Destroyed || thing.Map == null
                || thing.Position == IntVec3.Invalid) {// just in case
                #if DEBUG
                if (Utils.showDebug[(int)DBF.TidyStacksOf]) {
                    if (!thing.Spawned) Log.Warning("Cannot tidy stack of unspawned " + thing.ToString());
                    else if (thing.Destroyed) Log.Warning("Cannot tidy stack of destroyed " + thing.ToString());
                    else if (thing.Map == null) Log.Warning("Cannot tidy stack on null map for " + thing.ToString());
                    else if (thing.Position == IntVec3.Invalid) Log.Warning("Cannot tidy invalid position for " + thing.ToString());
                }
                #endif
                return;
            }
            // If stack limit is 1, it cannot stack
            var stackLimit = thing.def.stackLimit;
            if (stackLimit <= 1) { return; }
            // Get everything and start tidying!
            var thingStacks = thing.Map.thingGrid.ThingsAt(thing.Position)
                                   .Where(t => t.def == thing.def).ToList();
            int extra = 0; // "extra" Sheep we pick up from wherever
            for (int i = 0; i < thingStacks.Count; i++)
            {
                if (thingStacks[i] == null || thingStacks[i].Destroyed) { break; } // maybe we emptied it already
                if (thingStacks[i].stackCount == stackLimit) { continue; }
                if (thingStacks[i].stackCount > stackLimit)
                {
                    // should never happen?
                    Log.Warning("LWM.DeepStorage found " + thingStacks[i].stackCount + " of " +
                                thingStacks[i].ToString() + " when the stackLimit is " +
                                stackLimit + ". This should never happen?");
                    extra = thingStacks[i].stackCount - stackLimit;
                    thingStacks[i].stackCount = stackLimit;
                    continue;
                }
                // If we get to here, we have something like
                //   74 sheep, and the stacklimit is 75
                if (extra > 0)
                { // maybe there were some extra lying around?
                    int x = Math.Min(stackLimit - thingStacks[i].stackCount, extra);
                    thingStacks[i].stackCount += x;
                    extra -= x;
                    if (thingStacks[i].stackCount == stackLimit) { continue; }
                }
                // Now try to pick up any extra sheep from remaining stacks of sheep
                // (All stacks ab)ve here already have full stacks)
                // Start looking at the end:
                for (int j = thingStacks.Count - 1; j > i; j--)
                {
                    if (thingStacks[j] == null) { continue; } //maybe already empty
                    if (thingStacks[j].stackCount <= (stackLimit - thingStacks[i].stackCount))
                    {
                        // can absorb all
                        thingStacks[i].TryAbsorbStack(thingStacks[j], true);
                        if (thingStacks[i].stackCount >= stackLimit) { break; }
                    }
                    else
                    {
                        // more than enough
                        int x = stackLimit - thingStacks[i].stackCount;
                        thingStacks[i].stackCount = stackLimit;
                        thingStacks[j].stackCount -= x;
                        break;
                    }
                } // continue on big loop if we filled it...
                if (thingStacks[i].stackCount < stackLimit)
                {
                    break; // we can't clean up any more...continue looking for unfull stacks (probably this one)
                }
            } // end loop thru ThingsAt the location
            while (extra > 0)
            {
                //weird?  Shouldn't happen?
                int x = Math.Min(stackLimit, extra);
                extra -= x;
                Thing t = GenSpawn.Spawn(thing.def, thing.Position, thing.Map);
                t.stackCount = x;
            }
        } // end TidyStacksOf(thing)

        public static HashSet<Thing> TopThingInDeepStorage = new HashSet<Thing>(); // for display
        
    } // End Utils class
    
    /******************* the custom Comp class and XML LWM.DeepStorage.Properties *****/
    public class Properties : CompProperties {
        public Properties() {
            this.compClass = typeof(LWM.DeepStorage.CompDeepStorage);
        }

        public int minNumberStacks = 1;
        public int maxNumberStacks = 2;
        public int timeStoringTakes = 1000; // measured in ticks
        public float maxTotalMass = 0f;
        public float maxMassOfStoredItem = 0f;
        public StatDef altStat=null;
        public bool showContents=true;

        public GuiOverlayType overlayType=GuiOverlayType.Normal;
    }
    
    public enum GuiOverlayType : byte {
        Normal,
        CountOfAllStacks,       // Centered on DSU
        CountOfStacksPerCell,   // Standard overlay position for each cell
        SumOfAllItems,          // Centered on DSU
        //SumOfItemsPerCell,      // etc. TODO?  For e.g., Big Shelf?
        None,                   // Some users may want this
    }

    public class CompDeepStorage : ThingComp, IHoldMultipleThings.IHoldMultipleThings {
        public override IEnumerable<Gizmo> CompGetGizmosExtra() {
            foreach (Gizmo g in base.CompGetGizmosExtra()) {
                yield return g;
            }

            // I left this lovely testing code in - oops.
            //yield return new Command_Action
            //{
            //    defaultLabel = "Minus One",
            //    action=delegate ()
            //    {
            //        foreach (var cl in parent.GetSlotGroup().CellsList)
            //        {
            //            foreach (Thing t in parent.Map.thingGrid.ThingsAt(cl))
            //            {
            //                if (t.def.category == ThingCategory.Item)
            //                {
            //                    if (t.stackCount > 1)
            //                    {
            //                        Log.Warning("Lowering " + t.ToString());
            //                        t.stackCount--;
            //                    }
            //                }// item
            //            }// each thing
            //        }// each cell
            //    },// end action
            //};
        }

        public int minNumberStacks {
            get {
                return ((Properties)this.props).minNumberStacks;
            }
        }
        public int maxNumberStacks {
            get {
                return ((Properties)this.props).maxNumberStacks;
            }
        }
        public int timeStoringTakes {
            get {
                return ((Properties)this.props).timeStoringTakes;
            }
        }
        public bool showContents {
            get {
                return ((Properties)this.props).showContents;
            }
        }

        public Properties cdsProps { // b/c I hate typing :p
            get {
                return ((Properties)this.props);
            }
        }
        


        public StatDef stat = StatDefOf.Mass;
        /*******  For only one limiting stat: (mass, or bulk for CombatExtended)  *******/
        public float limitingFactorForItem=0f;
        public float limitingTotalFactorForCell=0f;
        /*******  Viable approach if anyone ever wants to limit storage based on >1 stat:
         *          We can revisit this is anyone ever requests it
         *          (this approach would need a for loop in _CanCarryItemsTo.cs, etc)
        public float[] maxStatOfStoredItem = { };
        public StatDef[] statForStoredItem = { };
        public float[] maxTotalStat = { };
        public StatDef[] statToTotal = { };
        */


        public override void Initialize(CompProperties props) {
            base.Initialize(props);
            // Remove duplicate entries and ensure the last entry is the only one left
            //   This allows a default abstract def with the comp
            //   and child def to change the comp value:
            CompDeepStorage[] list = this.parent.GetComps<CompDeepStorage>().ToArray();
            // Remove everything but the last entry:
            for (var i = 0; i < list.Length - 1; i++) {
                this.parent.AllComps.Remove(list[i]);
            }

            /*******  For only one limiting stat: (mass, or bulk for CombatExtended)  *******/
            if (((Properties)props).altStat != null) stat = ((Properties)props).altStat;
            if (((Properties)props).maxTotalMass > 0f) //for floating arithmetic, just to be safe
                limitingTotalFactorForCell = ((Properties)props).maxTotalMass + .0001f;
            if (((Properties)props).maxMassOfStoredItem > 0f)
                limitingFactorForItem = ((Properties)props).maxMassOfStoredItem + .0001f;
            /*******  Viable approach if anyone ever wants to limit storage based on >1 stat:
            if (((Properties)props).maxMassOfStoredItem > 0f) {
                statForStoredItem[0] = StatDefOf.Mass;
                maxStatOfStoredItem[0] = ((Properties)props).maxMassOfStoredItem;
            }
            if (((Properties)props).maxTotalMass > 0f) {
                statToTotal[0] = StatDefOf.Mass;
                maxTotalStat[0] = ((Properties)props).maxTotalMass;
            }
            */

        }

        public int CapacityToStoreThingAt(Thing thing, Map map, IntVec3 cell) {
            int capacity = 0;
            /* First test, is it even light enough to go in this DS? */
            //      No rocket launchers in jewelry boxes?
            if (this.limitingFactorForItem > 0f) {
                if (thing.GetStatValue(this.stat) > this.limitingFactorForItem)
                    return 0;
            }
            float totalWeightStoredHere=0f;  //mass, or bulk, etc.

            List<Thing> list = map.thingGrid.ThingsListAt(cell);
            var stacksStoredHere=0;
            for (int i=0; i<list.Count;i++) {
                Thing thingInStorage = list[i];
                if (thingInStorage.def.EverStorable(false)) { // an "item" we care about
                    stacksStoredHere+=1;
                    if (this.limitingTotalFactorForCell > 0f) {
                        totalWeightStoredHere +=thingInStorage.GetStatValue(this.stat)*thingInStorage.stackCount;
                        if (totalWeightStoredHere > this.limitingTotalFactorForCell &&
                            stacksStoredHere >= this.minNumberStacks) {
                            return 0;
                        }
                    }
                    if (thingInStorage.CanStackWith(thing)) {
                        if (thingInStorage.stackCount < thingInStorage.def.stackLimit) {
                            capacity += thingInStorage.def.stackLimit - thingInStorage.stackCount;
                        }
                    }
                } // item
            } // end of cell's contents...
            // Count empty spaces:
            if (this.maxNumberStacks > stacksStoredHere) {
                capacity+=(this.maxNumberStacks-stacksStoredHere)*thing.def.stackLimit;
            }
            if (this.limitingTotalFactorForCell > 0f) {
                totalWeightStoredHere = this.limitingTotalFactorForCell - totalWeightStoredHere; // now storable here :p
                if (totalWeightStoredHere <= 0f) {
                    return 0;
                }
                capacity = Math.Min(capacity, (int)(totalWeightStoredHere/thing.GetStatValue(this.stat)));
            }
            return capacity;
        }
        /************************** IHoldMultipleThings interface ************************/
        /* For compatibility with Mehni's PickUpAndHaul                                  */
        public bool CapacityAt(Thing thing, IntVec3 cell, Map map, out int capacity) {
            capacity = this.CapacityToStoreThingAt(thing, map, cell);
            if (capacity > 0) return true;
            return false;
        }
        public bool StackableAt(Thing thing, IntVec3 cell, Map map) {
            return this.CapacityToStoreThingAt(thing,map,cell) > 0;
        }
    } // end CompDeepStorage


} // close LWM.DeepStorage namespace.  Thank you for reading!  =^.^=
