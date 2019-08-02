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
        /******************* the custom Comp class and XML LWM.DeepStorage.Properties *****/
    public class Properties : CompProperties {
        public Properties() {
            this.compClass = typeof(LWM.DeepStorage.CompDeepStorage);
        }

        /************************* Stat window (information window) ***********************/
        // This will hopefully reduce the number of annoying questions in the discussion thread
        //   "What does this store?  Can I put X in there?"
        private static StatCategoryDef DeepStorageCategory=null;
        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req) {
            foreach (StatDrawEntry s in base.SpecialDisplayStats(req)) {
                yield return s;
            }
            if (DeepStorageCategory==null) {
                DeepStorageCategory=DefDatabase<StatCategoryDef>.GetNamed("LWM_DS_Stats");
                if (DeepStorageCategory == null) {
                    Log.Warning("LWM.DeepStorage: Stat Category FAILED to load.");
                    yield break;
                }
            }
            yield return new StatDrawEntry(DeepStorageCategory, "LWM_DS_maxNumStacks".Translate(),
                                           size>1?
                                           "LWM_DS_TotalAndPerCell".Translate(maxNumberStacks*size,maxNumberStacks)
                                           :maxNumberStacks.ToString(),
                                           10 /*display priority*/, "LWM_DS_maxNumStacksDesc".Translate());
            if (maxTotalMass > 0f) yield return new StatDrawEntry(DeepStorageCategory, "LWM_DS_maxTotalMass".Translate(),
                                       size>1?
                                        "LWM_DS_TotalAndPerCell".Translate(kg(maxTotalMass*size),kg(maxTotalMass))
                                       :kg(maxTotalMass),
                                        9, "LWM_DS_maxTotalMassDesc".Translate());
            if (maxMassOfStoredItem > 0f) yield return new StatDrawEntry(DeepStorageCategory, "LWM_DS_maxMassOfStoredItem".Translate(),
                                                                         kg(maxMassOfStoredItem),
                                                                         8, "LWM_DS_maxMassOfStoredItemDesc".Translate());
            if (AllowedCategoriesString!="") yield return new StatDrawEntry(DeepStorageCategory, "LWM_DS_allowedCategories".Translate(),
                                                                            AllowedCategoriesString,
                                                                            7, "LWM_DS_allowedCategoriesDesc".Translate());
            if (AllowedDefsString!="") yield return new StatDrawEntry(DeepStorageCategory, "LWM_DS_allowedDefs".Translate(),
                                                                      AllowedDefsString,
                                                                      6, "LWM_DS_allowedDefsDesc".Translate());
            if (DisallowedString!="") yield return new StatDrawEntry(DeepStorageCategory, "LWM_DS_disallowedStuff".Translate(),
                                                                     DisallowedString,
                                                                     5, "LWM_DS_disallowedStuffDesc".Translate());
//            if (parent?.building?.fixedStorageSettings?.filter
            yield break;
        }
        private string kg(float s) {
            if (altStat==null) {
                return "LWM_DS_kg".Translate(s);
            }
            return "LWM_DS_BulkEtcOf".Translate(s, altStat.label);
        }
        /************************* Done with Stat window ***********************/

        
        public override void ResolveReferences(ThingDef parentDef) {
            base.ResolveReferences(parentDef);
            parent=parentDef; // no way to actually get this via def :p
            size=parentDef.Size.Area;
        }
        
        public string AllowedCategoriesString {
            get {
                if (categoriesString==null) {
                    categoriesString="";
                    ThingFilter tf=parent?.building?.fixedStorageSettings?.filter;
                    if (tf==null) {
                        Log.Warning("LWM.DeepStorage:could not find filter for "+parent.defName);
                        return "";
                    }
                    var c=(List<string>)Harmony.AccessTools.Field(typeof(ThingFilter), "categories").GetValue(tf);
                    if (c.NullOrEmpty()) return "";
                    foreach (var x in c) {
                        if (categoriesString!="") categoriesString+="\n";
                        categoriesString+=DefDatabase<ThingCategoryDef>.GetNamed(x, true).LabelCap;
                    }
                }
                return categoriesString;
            }
        }

        public string AllowedDefsString {
            get {
                if (defsString==null) {
                    defsString="";
                    ThingFilter tf=parent?.building?.fixedStorageSettings?.filter;
                    if (tf==null) {
                        Log.Warning("LWM.DeepStorage:could not find filter for "+parent.defName);
                        return "";
                    }
                    var d=(List<ThingDef>)Harmony.AccessTools.Field(typeof(ThingFilter), "thingDefs").GetValue(tf);
                    if (d.NullOrEmpty()) return "";
                    foreach (var x in d) {
                        if (defsString!="") defsString+="\n";
                        defsString+=x.LabelCap;
                    }
                }
                return defsString;
            }
        }
        public string DisallowedString{
            get {
                if (disallowedString==null) {
                    disallowedString="";
                    ThingFilter tf=parent?.building?.fixedStorageSettings?.filter; // look familiar yet?
                    if (tf==null) {
                        Log.Warning("LWM.DeepStorage:could not find filter for "+parent.defName);
                        return "";
                    }
                    var c=(List<string>)Harmony.AccessTools.Field(typeof(ThingFilter), "disallowedCategories").GetValue(tf);
                    if (!(c.NullOrEmpty())) {
                        foreach (var x in c) {
                            if (disallowedString!="") disallowedString+="\n";
                            disallowedString+=DefDatabase<ThingCategoryDef>.GetNamed(x, true).LabelCap;
                        }
                    }
                    var d=(List<ThingDef>)Harmony.AccessTools.Field(typeof(ThingFilter), "disallowedThingDefs").GetValue(tf);
                    if (!(d.NullOrEmpty())) {
                        foreach (var x in d) {
                            if (defsString!="") defsString+="\n";
                            defsString+=x.LabelCap;
                        }
                    }
                }
                return disallowedString;
            }
        }


        public int minNumberStacks = 1;
        public int maxNumberStacks = 2;
        public int timeStoringTakes = 1000; // measured in ticks
        public int minTimeStoringTakes =-1;
        public int additionalTimeEachStack=0; // extra time to store for each stack already there
        public int additionalTimeEachDef=0;   // extra time to store for each different type of object there
        public float additionalTimeStackSize=0f; // item with stack size 75 may take longer to store
        public List<ThingDef> quickStoringItems=null;
        public float maxTotalMass = 0f;
        public float maxMassOfStoredItem = 0f;
        public StatDef altStat=null;
        public bool showContents=true;
        public GuiOverlayType overlayType=GuiOverlayType.Normal;
                
        public int size=0;
        public ThingDef parent=null; // :p  Have to keep track of this myself

        private string categoriesString=null; // for the Stats window (information window)
        private string defsString=null;
        private string disallowedString=null;
    }
    
    public enum GuiOverlayType : byte {
        Normal,
        CountOfAllStacks,       // Centered on DSU
        CountOfStacksPerCell,   // Standard overlay position for each cell
        SumOfAllItems,          // Centered on DSU
        SumOfItemsPerCell,      // For e.g., Big Shelf
        None,                   // Some users may want this
    }

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
//				defaultDesc = "CommandRenameZoneDesc".Translate(),
				action = delegate()
				{
					Find.WindowStack.Add(new Dialog_RenameDSU(this));
				},
				hotKey = KeyBindingDefOf.Misc1
			};

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
/*        public int timeStoringTakes() {
            return ((Properties)this.props).timeStoringTakes;
        }        */
        public int TimeStoringTakes(Map map, IntVec3 cell, Pawn pawn) {
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
