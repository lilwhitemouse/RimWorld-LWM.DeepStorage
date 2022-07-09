using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Linq;
//using HarmonyLib;
//using System.Reflection;
//using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using UnityEngine;
using static LWM.DeepStorage.Utils.DBF; // trace utils


namespace LWM.DeepStorage
{
    public class CompDeepStorage : ThingComp, IHoldMultipleThings.IHoldMultipleThings {
        //public float y=0f;
        public override IEnumerable<Gizmo> CompGetGizmosExtra() {
            foreach (Gizmo g in base.CompGetGizmosExtra()) {
                yield return g;
            }
            yield return new Command_Action
			{
				icon = ContentFinder<Texture2D>.Get("UI/Commands/RenameZone", true),
				defaultLabel = "CommandRenameZoneLabel".Translate(),
				action = delegate()
				{
					Find.WindowStack.Add(new Dialog_RenameDSU(this));
				},
				hotKey = KeyBindingDefOf.Misc1
			};
            #if DEBUG
            yield return new Command_Toggle {
                defaultLabel="Use RClick Logic",
                defaultDesc="Toggle use of custom Right Click logic",
                isActive=(()=>Settings.useDeepStorageRightClickLogic),
                toggleAction=delegate() {
                    Settings.useDeepStorageRightClickLogic=!Settings.useDeepStorageRightClickLogic;
                }
            };
            yield return new Command_Action {
                defaultLabel="Items in Region",
                action=delegate() {
                    Log.Warning("ListerThings for "+parent+" (at region at position "+parent.Position+")");
                    foreach (var t in parent.Position.GetRegion(parent.Map).ListerThings
                             .ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.HaulableEver))) {
                        Log.Message("  "+t);
                    }
                }
            };
            #endif

            #if false
            yield return new Command_Action {
                defaultLabel="Y-=.1",
                action=delegate () {
                    y-=0.1f;
                    Messages.Message("Offset: "+y,MessageTypeDefOf.NeutralEvent);
                }
            };
            yield return new Command_Action {
                defaultLabel="Y+=.1",
                action=delegate () {
                    y+=0.1f;
                    Messages.Message("Offset: "+y,MessageTypeDefOf.NeutralEvent);
                }
            };
            #endif

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

        public override string TransformLabel(string label) {
            if (this.buildingLabel=="") return label;
            return buildingLabel;
        }

        // If the player has renamed the item, show the old name (e.g., label)
        //   in the window so player can see "Oh, it's a masterwork uranium shelf" etc
        public override string CompInspectStringExtra() {
            if (this.buildingLabel=="") {
                return null;
            }
            // in case you could not tell, I was tired when I wrote this:
            string s=buildingLabel;
            buildingLabel="";
            string origLabel=parent.Label;
            buildingLabel=s;
            return origLabel;
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

        public virtual int TimeStoringTakes(Map map, IntVec3 cell, Pawn pawn) {
            if (cdsProps.minTimeStoringTakes <0) {
                // Basic version
                return ((Properties)this.props).timeStoringTakes;
            }
            Thing thing=pawn?.carryTracker?.CarriedThing;
            if (thing==null) {
                Log.Error("LWM.DeepStorage: null CarriedThing");
                return 0;
            }
            // having a minTimeStoringTakes, adjusted:
            // TODO: additionTimeEachDef
            int t=cdsProps.minTimeStoringTakes;
            var l=map.thingGrid.ThingsListAtFast(cell).FindAll(x=>x.def.EverStorable(false));
            bool thingToPlaceIsDifferentFromAnythingThere=false; // Do I count storing thing as a separate def?
            if (l.Count>0) {
                thingToPlaceIsDifferentFromAnythingThere=true;
            }
            // additional Time for Each Stack:
            for (int i=0; i<l.Count; i++) {
                t+=cdsProps.additionalTimeEachStack;
                if (cdsProps.additionalTimeEachDef>0 &&
                    l[i].CanStackWith(thing)) {
                    // some defs cannot stack with themselves (esp under other mods,
                    //   for example, common sense doesn't allow meals with and w/o
                    //   insect meat to stack)
                    // Note: As far as I know, this works for items with stack sizes of 1, too.
                    thingToPlaceIsDifferentFromAnythingThere=false;
                }
            }
            // additional Time for Each Def (really for each thing that doesn't stack)
            if (cdsProps.additionalTimeEachDef>0) {
                if (thingToPlaceIsDifferentFromAnythingThere) t+=cdsProps.additionalTimeEachDef;
                // l2=l mod CanStackWith()
                // That is, l2 is a maximal list of objects that cannot stack with each other from l.
                // That is, l2 is l with all things that can stack together reduced to one item.
                List<Thing> l2=new List<Thing>(l);
                int i=0;
                for (; i<l2.Count; i++) {
                    int j=i+1;
                    while (j<l2.Count) {
                        if (l2[i].CanStackWith(l2[j])) {
                            l2.RemoveAt(j);
                        } else {
                            j++;
                        }
                    }
                }
                // now l2 is prepared
                if (l2.Count > 1) {
                    t+=(cdsProps.additionalTimeEachDef*(l2.Count-1));
                }
            }
            // additional Time Stack Size
            if (Settings.storingTimeConsidersStackSize && cdsProps.additionalTimeStackSize>0f) {
                float factor=1f;
                if (thing.def.smallVolume || // if it's small (silver, gold)
                    (   // or on the list (compost for Fertile Fields?)
                        (!cdsProps.quickStoringItems.NullOrEmpty()) &&
                        cdsProps.quickStoringItems.Contains(thing.def)
                        )
                    ) {
                    factor=0.05f;
                }
                t+=(int)(cdsProps.additionalTimeStackSize *
                    pawn.carryTracker.CarriedThing.stackCount *
                    factor);
            }
            return t;
        } // end TimeStoringTakes

        static List<Thing> listOfStoredItems=new List<Thing>();
        static System.Text.StringBuilder headerStringB=new System.Text.StringBuilder();
        public List<Thing> getContentsHeader(out string header, out string tooltip) {
            listOfStoredItems.Clear();
            headerStringB.Length=0;
            tooltip=null; // TODO: add more information via tooltip for DSUs with minNumStacks above 2

            bool flagUseStackInsteadOfItem=false; // "3/4 Items" vs "3/4 Stacks"
            int numCells=0;
            float itemsTotalMass = 0; // or Bulk for CE ;p
            int cellsBelowMin=0;
            int cellsAtAboveMin=0;
            foreach (IntVec3 storageCell in (parent as Building_Storage).AllSlotCells()) {
                int countInThisCell=0;
                numCells++;
                foreach (Thing t in parent.Map.thingGrid.ThingsListAt(storageCell)) {
                    if (t.Spawned && t.def.EverStorable(false)) {
                        listOfStoredItems.Add(t);
                        itemsTotalMass += t.GetStatValue(this.stat, true) * (float)t.stackCount;
                        if (t.def.stackLimit > 1) flagUseStackInsteadOfItem=true;
                        countInThisCell++;
                    }
                }
                if (countInThisCell >= this.minNumberStacks) cellsAtAboveMin++;
                else cellsBelowMin++;
            }
            // We want to give user inforation about mass limits and how close we are, if they exist
            // TODO: Maybe use prop's kg() to translate everywhere, for better readability if using
            //       bulk.  Or maybe just leave it as is; CE will live.
            if (this.limitingTotalFactorForCell > 0f) {
                // If minNumberStacks > 2, this really really complicates things.
                // For example, if one cell has 1 SUPER HEAVY item in it, and the other cell has 7 light items...
                // What do we say?  It's over the total mass limit....but each cell can get more things!
                if (this.minNumberStacks > 2) {
                    // Easy case: if each cell has at least minimum number of stacks:
                    // TODO: if min is 5 and there are 4 with below mass limit, also go here:
                    if (cellsAtAboveMin == numCells) { //////////////// NO cells below minimum
                        // Simple header that includes mass:  12/20 stacks with total mass of 2.3/5 - as below
                        headerStringB.Append("LWM.ContentsHeaderMaxMass".Translate(listOfStoredItems.Count,
                                // 3 stacks or 3 items:
                                (flagUseStackInsteadOfItem?"LWM.XStacks":"LWM.XItems").Translate(maxNumberStacks*numCells),
                                stat.ToString().ToLower(), itemsTotalMass.ToString("0.##"),
                                (limitingTotalFactorForCell*numCells).ToString("0.##")));
                    } else if (cellsBelowMin==numCells) { ///////////// ALL cells below minimum
                        // 3/10 items, max 20, with total mass 0.45
                        headerStringB.Append("LWM.ContentsHeaderMinMax".Translate(listOfStoredItems.Count,
                                (flagUseStackInsteadOfItem?"LWM.XStacks":"LWM.XItems").Translate(minNumberStacks*numCells),
                                maxNumberStacks*numCells, stat.ToString().ToLower(), itemsTotalMass.ToString("0.##")));
                    } else { ////////////////////////////////////////// SOME cells are below the minimum
                        if (flagUseStackInsteadOfItem) // 11 stacks, max 20, limited with total mass 8.2
                            headerStringB.Append("LWM.ContentsHeaderStacksMax".Translate(listOfStoredItems.Count,
                                maxNumberStacks*numCells, stat.ToString().ToLower(), itemsTotalMass.ToString("0.##")));
                        else
                            headerStringB.Append("LWM.ContentsHeaderItemsMax".Translate(listOfStoredItems.Count,
                                maxNumberStacks*numCells, stat.ToString().ToLower(), itemsTotalMass.ToString("0.##")));
                    }
                } else { // Simple header that includes mass:  4/8 stacks with total mass of 12/20
                    headerStringB.Append("LWM.ContentsHeaderMaxMass".Translate(listOfStoredItems.Count,
                            (flagUseStackInsteadOfItem?"LWM.XStacks":"LWM.XItems").Translate(maxNumberStacks*numCells),
                            stat.ToString().ToLower(), itemsTotalMass.ToString("0.##"),
                            (limitingTotalFactorForCell*numCells).ToString("0.##")));
                }
            } else { // No limiting mass factor per cell
                // 4/8 stacks with total mass of 12kg
                headerStringB.Append("LWM.ContentsHeaderMax".Translate(listOfStoredItems.Count,
                             // 3 stacks or 3 items:
                             (flagUseStackInsteadOfItem?"LWM.XStacks":"LWM.XItems").Translate(maxNumberStacks*numCells),
                             stat.ToString().ToLower(), itemsTotalMass.ToString("0.##")));
            }
            ///////////////////////////// Max mass per item?
            if (limitingFactorForItem>0f) { // (Cannot store items above mass of X kg)
                headerStringB.Append('\n').Append("LWM.ContentsHeaderMaxSize".Translate(
                                      stat.ToString().ToLower(),
                                      limitingFactorForItem.ToString("0.##")));
            }
            AddPawnReservationsHeader((Building_Storage)parent); // seriously, don't add this comp to anything else.
            header=headerStringB.ToString();
            return listOfStoredItems;
        }
        public static List<Thing> genericContentsHeader(Building_Storage storage, out string header, out string tooltip) {
            headerStringB.Length=0;
            listOfStoredItems.Clear();
            tooltip=null;
            bool flagUseStackInsteadOfItem=false; // "3/4 Items" vs "3/4 Stacks"
            float itemsTotalMass = 0; // not Bulk here
            int numCells=0;
            foreach (IntVec3 storageCell in storage.AllSlotCells()) {
                foreach (Thing t in storage.Map.thingGrid.ThingsListAt(storageCell)) {
                    if (t.Spawned && t.def.EverStorable(false)) {
                        listOfStoredItems.Add(t);
                        itemsTotalMass += t.GetStatValue(StatDefOf.Mass, true) * (float)t.stackCount;
                        if (t.def.stackLimit > 1) flagUseStackInsteadOfItem=true;
                    }
                }
                numCells++;
            }
            // 4/8 stacks with total mass of 12kg (as above)
            headerStringB.Append("LWM.ContentsHeaderMax"
                     .Translate(listOfStoredItems.Count,
                                // 3 stacks or 3 items:
                                (flagUseStackInsteadOfItem?"LWM.XStacks":"LWM.XItems").Translate(numCells),
                                StatDefOf.Mass, itemsTotalMass.ToString("0.##")));
            AddPawnReservationsHeader(storage);
            header=headerStringB.ToString();
            return listOfStoredItems;
        }
        ///////////////////////////// Pawn reservations
        //             Displaying who is using the storage building has cut
        //             down on questions in the Steam thread. Can I get a wahoo?
        // (adds directly to headerStringB)
        static List<string> listOfReservingPawns=new List<string>();
        static void AddPawnReservationsHeader(Building_Storage storage) {
            List<Pawn>pwns=storage.Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
            if (pwns.Count > 0) {
                listOfReservingPawns.Clear();
                foreach (IntVec3 c in storage.AllSlotCells()) {
                    Pawn p=storage.Map.reservationManager.FirstRespectedReserver(c, pwns[0]);
                    if (p!=null) {
                        // (p can possibly be animals)
                        listOfReservingPawns.Add(p.LabelShort);
                    }
                }
                if (listOfReservingPawns.Count > 0) {
                    headerStringB.Append('\n');
                    if (listOfReservingPawns.Count==1) {
                        headerStringB.Append("LWM.ContentsHeaderPawnUsing".Translate(listOfReservingPawns[0]));
                    } else {
                        headerStringB.Append("LWM.ContentsHeaderPawnsUsing".Translate(
                                          String.Join(", ", listOfReservingPawns.ToArray())));
                    }
                }
            }
        } // end checking pawn reservations
        public virtual bool showContents {
            get {
                return ((Properties)this.props).showContents;
            }
        }

        public Properties cdsProps { // b/c I hate typing :p
            get {
                return ((Properties)this.props);
            }
        }




        public override void Initialize(CompProperties props) {
            base.Initialize(props);
            // Remove duplicate entries and ensure the last entry is the only one left
            //   This allows a default abstract def with the comp
            //   and child def to change the comp value:
            CompDeepStorage[] list = this.parent.GetComps<CompDeepStorage>().ToArray();
            // Remove everything but the last entry in both this and original def:
            // Don't ask why I made the choice to allow two <comps> entries.  Probably a bad idea.
            if (list.Length > 1) {
                for (var i = 0; i < list.Length - 1; i++) {
                    this.parent.AllComps.Remove(list[i]);
                }
                var l2=this.parent.def.comps.Where(cp => ((cp as Properties)!=null)).ToArray();
                for (var i=0; i< l2.Length -1; i++) {
                    this.parent.def.comps.Remove(l2[i]);
                }
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

        // IMPORTANT NOTE: some of the following logic is in the patch for TryFindBestBetterStoreCellFor
        //   (ShouldRemoveFrom logic).  TODO: it should probably be here

        public virtual int CapacityToStoreThingAt(Thing thing, Map map, IntVec3 cell, bool calledFromPatch = false) {
            Utils.Warn(CheckCapacity, "Checking Capacity to store "+thing.stackCount+thing+" at "
                       +(map?.ToString()??"NULL MAP")+" "+cell);
            int capacity = 0;
            /* First test, is it even light enough to go in this DS? */
            //      No rocket launchers in jewelry boxes?
            if (this.limitingFactorForItem > 0f) {
                if (thing.GetStatValue(this.stat) > this.limitingFactorForItem) {
                    Utils.Warn(CheckCapacity, "  Cannot store because "+stat+" of "
                               +thing.GetStatValue(stat)+" > limit of "+limitingFactorForItem);
                    return 0;
                }
            }
            float totalWeightStoredHere=0f;  //mass, or bulk, etc.

            List<Thing> list = map.thingGrid.ThingsListAt(cell);
            var imax = list.Count;
            var stacksStoredHere=0;
            var listarray = list.ToArray();

            for (int i=0; i< imax; i++) {
                Thing thingInStorage = listarray[i];
                //EverStorable checks if the item can be stored
                //since it is already stored lets not check it again
                //Might cause the building itself to be included but asuming no mass limit that should not be an issue
                if (calledFromPatch || thingInStorage.def.EverStorable(false)) { // an "item" we care about
                    stacksStoredHere+=1;
                    Utils.Mess(CheckCapacity, "  Checking against "+thingInStorage.stackCount+thingInStorage);
                    if (this.limitingTotalFactorForCell > 0f) {
                        totalWeightStoredHere +=thingInStorage.GetStatValue(this.stat)*thingInStorage.stackCount;
                        Utils.Mess(CheckCapacity, "    "+stat+" increased to "+totalWeightStoredHere+ " / "+
                                   limitingTotalFactorForCell);
                        if (totalWeightStoredHere > this.limitingTotalFactorForCell &&
                            stacksStoredHere >= this.minNumberStacks) {
                            Utils.Warn(CheckCapacity, "  "+thingInStorage.stackCount+thingInStorage+" already over mass!");
                            return 0;
                        }
                    }
                    if (thingInStorage==thing) {
                        Utils.Mess(CheckCapacity, "Found Item!");
                        if (stacksStoredHere > maxNumberStacks) {
                            // It's over capacity :(
                            Utils.Warn(CheckCapacity, "  But all stacks already taken: "+(stacksStoredHere-1)+" / "+maxNumberStacks);
                            return 0;
                        }
                        return thing.stackCount;
                    }
                    if (thingInStorage.CanStackWith(thing)) {
                        if (thingInStorage.stackCount < thingInStorage.def.stackLimit) {
                            Utils.Warn(CheckCapacity, "  has stackCount of only "+thingInStorage.stackCount+
                                       " so it can hold more");
                            capacity += thingInStorage.def.stackLimit - thingInStorage.stackCount;
                            if (calledFromPatch) return capacity;
                        }
                    }
                    //if (stacksStoredHere >= maxNumberStacks) break; // may be more stacks with empty space?
                } // item
            } // end of cell's contents...
            // Count empty spaces:
            if (this.limitingTotalFactorForCell > 0f) {
                if (stacksStoredHere <= minNumberStacks) {
                    capacity+=(minNumberStacks-stacksStoredHere)*thing.def.stackLimit;
                    Utils.Mess(CheckCapacity, "Adding capacity for minNumberStacks: "+stacksStoredHere+"/"+
                               minNumberStacks+" - capacity now: "+capacity);
                    totalWeightStoredHere+=(minNumberStacks-stacksStoredHere)*thing.GetStatValue(this.stat)*thing.def.stackLimit;
                    stacksStoredHere=minNumberStacks;
                }
                // reuse variable totalWeightStoredHere as totalWeightStorableHere
                totalWeightStoredHere = this.limitingTotalFactorForCell - totalWeightStoredHere;
                if (totalWeightStoredHere <= 0f) {
                    Utils.Mess(CheckCapacity, "No storage available by mass: above total by "+totalWeightStoredHere);
                    if (stacksStoredHere > this.minNumberStacks) return 0;
                    Utils.Mess(CheckCapacity, "  but minNumberStacks not passed, so available capacity is "+capacity);
                    return capacity;
                }
                if (stacksStoredHere < maxNumberStacks) {
                    capacity+=Math.Min(
                        ((maxNumberStacks-stacksStoredHere)*thing.def.stackLimit),   // capacity available by count
                        ((int)(totalWeightStoredHere/thing.GetStatValue(this.stat))) // capacity available by weight
                        );
                }
                Utils.Mess(CheckCapacity, "Total available mass for additional storage: "
                           +totalWeightStoredHere+"; final capacity: "+capacity);
                return capacity;
            }
            if (this.maxNumberStacks > stacksStoredHere) {
                Utils.Mess(CheckCapacity, ""+(maxNumberStacks-stacksStoredHere)+" free stacks: adding to available capacity");
                capacity+=(this.maxNumberStacks-stacksStoredHere)*thing.def.stackLimit;
            }
            Utils.Mess(CheckCapacity, "Available capacity: "+capacity);
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
            return this.CapacityToStoreThingAt(thing,map,cell,true) > 0;
        }
        /*********************************************************************************/
        public override void PostExposeData() { // why not call it "ExposeData" anyway?
            Scribe_Values.Look<string>(ref buildingLabel, "LWM_DS_DSU_label", "", false);
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
        public string buildingLabel="";

    } // end CompDeepStorage




}
