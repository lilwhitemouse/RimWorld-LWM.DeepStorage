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




}
