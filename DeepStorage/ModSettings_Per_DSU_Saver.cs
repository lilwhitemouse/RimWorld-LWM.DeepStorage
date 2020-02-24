using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;


namespace LWM.DeepStorage
{
    public class SettingsPerDSUSaver : IExposable {
        public Dictionary<string, object> saveData=new Dictionary<string, object>();
        // Actual Logic objects:
        //   Default values for DSU objects, so that when saving mod settings, we know what the defaults are.
        //   Only filled in as user changes the values.

        public static Dictionary<string, object> defaultDSUValues=new Dictionary<string, object>();
        public static Dictionary<string,List<string>> additionalCategories=null;//new Dictionary<string,List<string>>();
        public static Dictionary<string,List<string>> additionalDefs=null;
        public static Dictionary<string,object> changedProperties=null;
//        public static List<string> modifiedDefs=null;
        public static List<string> defsUsingCustomFilter;

        public bool AnythingChanged() {
            return defaultDSUValues.Count>0;
        }

        public object GetDefaultValue(ThingDef def, string key) {
            var cp=def.GetCompProperties<LWM.DeepStorage.Properties>();
            var index=def.defName+"_"+key;
            object tmpO;
            switch (key) {
                case "label":
                    return (defaultDSUValues.TryGetValue(index, out tmpO)?tmpO:def.label);
                case "maxNumStacks":
                    return (defaultDSUValues.TryGetValue(index, out tmpO)?tmpO:cp.maxNumberStacks);
                case "maxTotalMass":
                    return (defaultDSUValues.TryGetValue(index, out tmpO)?tmpO:cp.maxTotalMass);
                case "maxMassStoredItem":
                    return (defaultDSUValues.TryGetValue(index, out tmpO)?tmpO:cp.maxMassOfStoredItem);
                case "showContents":
                    return (defaultDSUValues.TryGetValue(index, out tmpO)?tmpO:cp.showContents);
                case "storagePriority":
                    return (defaultDSUValues.TryGetValue(index, out tmpO)?tmpO:def.building.defaultStorageSettings.Priority);
                case "overlayType":
                    return (defaultDSUValues.TryGetValue(index, out tmpO)?tmpO:cp.overlayType);
                case "filter":
                    return (defaultDSUValues.TryGetValue(index, out tmpO)?tmpO:def.building.defaultStorageSettings.filter);
                default:
                    Log.Error("LWM.DeepStorage: Settings tried to get default value for "+key+" but don't know what that is!");
                    return null;
                    //return (defaultDSUValues.TryGetValue(index, out tmpO)?tmpO:cp.maxNumberStacks);
            }
        } // end GetDefaultValue(...)
        public bool IsFilterChanged(ThingDef def) {
            if (additionalCategories!=null && additionalCategories.ContainsKey(def.defName) &&
                additionalCategories[def.defName].Count > 0) return true;
            if (additionalDefs!=null && additionalDefs.ContainsKey(def.defName) &&
                additionalDefs[def.defName].Count > 0) return true;
            return false;
        }




        public ThingFilter GetOriginalFilter(ThingDef def) { //todo ,remove
            if (defaultDSUValues.ContainsKey("DSU_"+def.defName+"_filter"))
                return (ThingFilter)defaultDSUValues["DSU_"+def.defName+"_filter"];
            else
                return def.building.defaultStorageSettings.filter;
        }
        public List<string> GetAdditionalCategoryNames(ThingDef def) {
            if (additionalCategories!=null &&
                additionalCategories.ContainsKey(def.defName)) {
                Utils.Mess(Utils.DBF.Settings, "  additionalCategories contains def "+def.defName+", creating new list");
                return additionalCategories[def.defName];
            } else {
                Utils.Mess(Utils.DBF.Settings, "  additionalCategories does not contain def "+def.defName+", will be null");
                //tmpAdditionalCategories=null;
                return null;
            }
        }

        // defName can be null to mean everything.
        public void ResetDSUToDefaults(string defName) {
            var allKeys=new List<string>(defaultDSUValues.Keys);
            Utils.Warn(Utils.DBF.Settings, "Resetting DSU to default: "+(defName==null?"ALL":defName)
                       +" ("+allKeys.Count+" defaults to search)");
            if (additionalCategories!=null) {
                if (defName!=null) additionalCategories.Remove(defName);
                //else additionalCategories.Clear(); // todo: defaults?
                else additionalCategories=null;
            }
            if (defsUsingCustomFilter!=null) {
                if (defName!=null) defsUsingCustomFilter.Remove(defName);
                else defsUsingCustomFilter=null;
            }
            while (allKeys.Count > 0) {
                var key=allKeys.Last();
                var value = defaultDSUValues[key];
                string s=key.Remove(0,4); // string off first DSU_
                var t=s.Split('_');
                string prop=t[t.Length-1]; // "LWM_Big_Shelf_label" ->  grab "label"
                string keyDefName=string.Join("_", t.Take(t.Length-1).ToArray()); // put defName back together
                Utils.Mess(Utils.DBF.Settings, "Checking key "+key+" (defName of "+keyDefName+")");
                if (defName==null || defName=="" || defName==keyDefName) {
                    Log.Message("LWM.DeepStorage: Resetting "+keyDefName+"'s "+prop+" to default: "+value);
                    var def=DefDatabase<ThingDef>.GetNamed(keyDefName);
                    if (prop=="label") {
                        def.label=(string)(value);
                    } else if (prop=="maxNumStacks") {
                        def.GetCompProperties<Properties>().maxNumberStacks=(int)(value);
                    } else if (prop=="maxTotalMass") {
                        def.GetCompProperties<Properties>().maxTotalMass=(float)(value);
                    } else if (prop=="maxMassStoredItem") {
                        def.GetCompProperties<Properties>().maxMassOfStoredItem=(float)(value);
                    } else if (prop=="showContents") {
                        def.GetCompProperties<Properties>().showContents=(bool)(value);
                    } else if (prop=="storagePriority") {
                        def.building.defaultStorageSettings.Priority=(StoragePriority)(value);
                    } else if (prop=="overlayType") {
                        def.GetCompProperties<Properties>().overlayType=(LWM.DeepStorage.GuiOverlayType)(value);
                    } else if (prop=="filter") {
                        def.building.fixedStorageSettings.filter=(ThingFilter)(value);
                    } else {
                        Log.Error("LWM.DeepStorage: FAILED TO RESET OPTION TO DEFAULT: "+key);
                    }
                    defaultDSUValues.Remove(key);
                }
                allKeys.RemoveLast();
            }
        }
        public bool IsDSUChanged(ThingDef d) {//todo
            if (additionalCategories!=null && additionalCategories.ContainsKey(d.defName) && //todo: Do I really need this?
                !additionalCategories[d.defName].NullOrEmpty()) return true;
            foreach (string k in defaultDSUValues.Keys) {
                string s=k.Remove(0,4); // strip off DSU_
                var t=s.Split('_');
                string keyDefName=string.Join("_", t.Take(t.Length-1).ToArray());
                if (keyDefName==d.defName) return true;
            }
            return false;
        }
        public void ResetAllToDefaults() {
            ResetDSUToDefaults(null);
        }



        // Scribe_Collections is great, but it does not handle nested collections
        //   (at least, the 1.0 version doesn't)  So, we save the keys separately
        //   from the values.  It's okay.
        static void ExposeDefDict(string label, ref Dictionary<string, List<string>> dict) {
            if (Scribe.mode!=LoadSaveMode.LoadingVars) {
                // for saving:
                Utils.Err(Utils.DBF.Settings, "ExposeDSUSettings: Saving dict with label "+label);
                if (dict!=null && dict.Count>0) {
                    // store as defName1|defName2|...
                    string s=String.Join("|", dict.Keys.ToArray());
                    Scribe_Values.Look(ref s, "defsWith"+label.CapitalizeFirst());
                    // save each list under its own name
                    foreach (var kvp in dict) {
                        string key=label+"_"+kvp.Key; // e.g.,  additionalCategories_LWM_Skip
                        List<string> list=kvp.Value;
                        Scribe_Collections.Look<string>(ref list, key, LookMode.Value, new object[0]);
                    }
                }
            } else {
                // for loading:
                Utils.Err(Utils.DBF.Settings, "ExposeDSUSettings: Loading dict with label "+label);
                string s=null;
                Scribe_Values.Look(ref s, "defsWith"+label.CapitalizeFirst());
                if (s!=null) {
                    dict=new Dictionary<string,List<string>>();
                    foreach (string defName in s.Split('|')) {
                        List<string> tmpList=null;
                        Scribe_Collections.Look<string>(ref tmpList, label+"_"+defName, LookMode.Value, new object[0]);
                        dict[defName]=tmpList;
                    }
                } else { dict=null; }
            }
        } // ExposeDefDict

        // Only ONE DSU Setting:
        private static void ExposeDSUSetting<T>(string keylet, ref T value) where T : IComparable {
            string key = "DSU_"+keylet;
            T defaultValue=(defaultDSUValues.ContainsKey(key)?(T)defaultDSUValues[key]:value);
            if (defaultValue.CompareTo(value)!=0) {
                Utils.Mess(Utils.DBF.Settings, "  Expose Setting: "+key+" (current value: "
                           +value+"; default Value: "+defaultValue+")");
            }
            Scribe_Values.Look(ref value, key, defaultValue);
            if (defaultValue.CompareTo(value)!=0) {
                Utils.Mess(Utils.DBF.Settings, "  after scribing: "+key+" (current value: "
                           +value+"; default Value: "+defaultValue+")");
            }
            if (defaultValue.CompareTo(value) != 0 && !defaultDSUValues.ContainsKey(key)) {
//                Log.Message("-->"+key+" storing default value of "+defaultValue);//TODO
//                Log.Warning("        Current value: "+value);
                defaultDSUValues[key]=defaultValue;
            }
        } // Expose DSU Setting
        public static void OnDefsLoaded() {
            // Apply per DSU settings to the actual DSUs now that defs are loaded:
            if (changedProperties!=null && changedProperties.Count >0) {
                Utils.Warn(Utils.DBF.Settings, "Applying DSU user settings for "+changedProperties.Count+" properties");
                foreach (string key in changedProperties.Keys) { // DSU_LWM_Big_Shelf_label
                    string s=key.Remove(0,4); // string off first DSU_
                    var t=s.Split('_');
                    string prop=t[t.Length-1]; // "LWM_Big_Shelf_label" ->  grab "label"
                    string defName=string.Join("_", t.Take(t.Length-1).ToArray()); // put defName back together
                    Utils.Mess(Utils.DBF.Settings, "Applying DSU user setting for "+defName+": key "+key);
                    var def=DefDatabase<ThingDef>.GetNamed(defName, false);
                    DeepStorage.Properties cp=def?.GetCompProperties<Properties>();
                    if (def==null || cp==null) {
                        Log.Warning("LWM.DeepStorage: tried to modified property "+key+", but could not find def's comp!");
                        continue;
                    }
                    if (prop=="label") {
                        TestAndUpdate<string>(def, prop, (string)changedProperties[key], ref def.label);
                    } else if (prop=="maxNumStacks") {
                        TestAndUpdate<int>(def, prop, (int)changedProperties[key], ref cp.maxNumberStacks);
                    } else if (prop=="maxTotalMass") {
                        TestAndUpdate<float>(def, prop, (float)changedProperties[key], ref cp.maxTotalMass);
                    } else if (prop=="maxMassStoredItem") {
                        TestAndUpdate<float>(def, prop, (float)changedProperties[key], ref cp.maxMassOfStoredItem);
                    } else if (prop=="showContents") {
                        TestAndUpdate<bool>(def, prop, (bool)changedProperties[key], ref cp.showContents);
                    } else if (prop=="storagePriority") {
                        StoragePriority tmpSP=def.building.defaultStorageSettings.Priority; // hard to access private field directly
                        TestAndUpdate<StoragePriority>(def, prop, (StoragePriority)changedProperties[key], ref tmpSP);
                        def.building.defaultStorageSettings.Priority=tmpSP;
                    } else if (prop=="overlayType") {
                        TestAndUpdate(def, prop, (DeepStorage.GuiOverlayType)changedProperties[key], ref cp.overlayType);
                    } else {
                        Log.Warning("LWM.DeepStorage: Tried to load setting "+key+" but it doesn't apply to anything!");
                    }
                }


            }
            if (additionalCategories!=null && additionalCategories.Count>0) {
                foreach (string defName in additionalCategories.Keys) {
                    ThingDef def=DefDatabase<ThingDef>.GetNamed(defName, false);
                    if (def==null) {
                        Log.Warning("LWM.DeepStorage: Tried to modify allowed filter (categories) for "+defName+" but it doesn't exist");
                    } else if (additionalCategories[defName].NullOrEmpty()) {
                        Log.Warning("LWM.DeepStorage: categories were saved for "+defName+" but are empty?");
                    } else {
                        foreach (string catName in additionalCategories[defName]) {
                            ThingCategoryDef cDef=DefDatabase<ThingCategoryDef>.GetNamed(catName, false);
                            if (cDef==null)
                                Log.Warning("LWM.DeepStorage: Trying to add category "+catName+" to "+defName+", but cannot find it.");
                            else
                                def.building.fixedStorageSettings.filter.SetAllow(cDef, true);
                        }
                    }
                }
            }
            if (additionalDefs!=null && additionalDefs.Count>0) {
                //todo
            }
        } // end OnDefsLoaded()
        /* Set ref origValue to value, making sure defaultDSUValues[keylet] is updated correctly */
        public static void TestAndUpdate<T>(ThingDef def, string keylet, T value, ref T origValue) where T : IComparable {
                string key="DSU_"+def.defName+"_"+keylet;
                if (value.CompareTo(origValue)==0) {
                    Utils.Mess(Utils.DBF.Settings,"  No change: "+key);
                    return;
                }
                Utils.Mess(Utils.DBF.Settings,"changing value for "+key+" from "+origValue+" to "+value);
                // "origValue" may be suspect - user could already have changed it once.  So:
                //    (this IS assignment by value, right?)
                T defaultValue=(defaultDSUValues.ContainsKey(key)?(T)defaultDSUValues[key]:origValue);
                origValue=value;
                // if the user reset to original default values, remove the default values key
                if (defaultValue.CompareTo(origValue)==0 && defaultDSUValues.ContainsKey(key)) {
                    defaultDSUValues.Remove(key);
                    Utils.Mess(Utils.DBF.Settings, "  removing default record for item "+keylet+" ("+def.defName+")");
                } else if (!defaultDSUValues.ContainsKey(key)) {
                    defaultDSUValues[key]=defaultValue;
                    Utils.Mess(Utils.DBF.Settings, "  creating default record for item "+keylet+" ("+def.defName+")");
                }
            }

        public static void ExposeDSUSettings(List<ThingDef> units) {
            // additionalCategories - use custom Dictionary<string,List<string>> saver:
            ExposeDefDict("additionalCats", ref additionalCategories);
            ExposeDefDict("additionalDefs", ref additionalDefs);


            if (Scribe.mode!=LoadSaveMode.LoadingVars) { // saving
                // Convert everything to changedProperties, and then save that:
                // Everything will have a format of DSU_defName_propertyName
                // e.g., DSU_LWM_Skip_maxNumStacks
                changedProperties=new Dictionary<string, object>();
                foreach (string key in defaultDSUValues.Keys) {
                    // defaultDSUValues's keys are already the correct format, but we have to take it apart anyway.
                    string s=key.Remove(0,4); // string off first DSU_
                    var t=s.Split('_');
                    string prop=t[t.Length-1]; // "LWM_Big_Shelf_label" ->  grab "label"
                    string defName=string.Join("_", t.Take(t.Length-1).ToArray()); // put defName back together
                    //Utils.Mess(Utils.DBF.Settings, "Checking key "+key+" (defName of "+keyDefName+")");
                    if (prop == "filter") continue; // handled differently
                    DeepStorage.Properties cp=DefDatabase<ThingDef>.GetNamed(defName, false)?.GetCompProperties<Properties>();
                    if (cp==null) {
                        Log.Error("LWM.DeepStorage: tried to save modified property "+key+", but could not find comp!");
                        continue;
                    }
                    // For the record, I think switch statements are usually stupid.
                    if (prop=="label") {
                        changedProperties[key]=DefDatabase<ThingDef>.GetNamed(defName).label;
                    } else if (prop=="maxNumStacks") {
                        changedProperties[key]=cp.maxNumberStacks;
                    } else if (prop=="maxTotalMass") {
                        changedProperties[key]=cp.maxTotalMass;
                    } else if (prop=="maxMassStoredItem") {
                        changedProperties[key]=cp.maxMassOfStoredItem;
                    } else if (prop=="showContents") {
                        changedProperties[key]=cp.showContents;
                    } else if (prop=="storagePriority") {
                        // Save as an int - type data isn't saved, but we can convert from an int back to the Enum:x
                        changedProperties[key]=(int)(DefDatabase<ThingDef>.GetNamed(defName).building.defaultStorageSettings.Priority);
                    } else if (prop=="overlayType") {
                        // Same as above
                        changedProperties[key]=(int)(cp.overlayType);
                    } else {
                        Log.Error("LWM.DeepStorage: Trying to save "+key+" but have no idea what "+prop+" is.");
                    }
                } // looped through all things with a default value stored
                if (changedProperties.Count > 0) {
                    // I really hope this works.  But everything should be saveable in LookMode.Value?
                    Utils.Mess(Utils.DBF.Settings, "Expose DSU Settings: writing "+changedProperties.Count+" user-modified properties");
                    Scribe_Collections.Look(ref changedProperties, "changedProps", LookMode.Value, LookMode.Value);
                }
                changedProperties=null; // don't need it anymore
            } else {                                     // loading
                changedProperties=null;
                Utils.Mess(Utils.DBF.Settings, "Expose DSU Settings: loading user-modified properties:");
                Scribe_Collections.Look(ref changedProperties, "changedProps", LookMode.Value, LookMode.Value);
                Utils.Mess(Utils.DBF.Settings, (changedProperties==null || changedProperties.Count==0)?
                           "  No user-modified properties":
                           ("  Found "+changedProperties.Count+":\n    "
                            +string.Join("\n    ", changedProperties.Select(kv=>kv.Key+"="+kv.Value).ToArray())));
                // Actually applying these changes is done when the Defs are Loaded.  (OnDefsLoaded)//todo: what is it called?
            }
            if (Scribe.mode==LoadSaveMode.PostLoadInit) {
                try {
                var d=DefDatabase<ThingDef>.GetNamed("LWM_Clothing_Rack");
                if (d!=null) {
                    Log.Error("!!!!!!!!!!!!!!!!! I am an idiot");
                    Dictionary<string,int> cp=null;
                    Scribe_Collections.Look<string,int>(ref cp, "changedProps");
//                    int x=0;
//                    Scribe_Values.Look(ref x, "DSU_LWM_Clothing_Rack_overlayType", 0);
                    if (cp.ContainsKey("DSU_LWM_Clothing_Rack_overlayType")) {
                        Log.Error("Found "+cp["DSU_LWM_Clothing_Rack_overlayType"]);

                    }
                }
                } catch (Exception e) {

                }
                try {
                    var dd=DefDatabase<ThingDef>.AllDefsListForReading;
                    Log.Error("-------------Has "+dd.Count);
                } catch (Exception e) {

                }

            }
            return; // todo - clean this up:
//            Utils.Warn(Utils.DBF.Settings, "ExposeDSUSettings: Looking at defsUsingCustomFilter.");
//            Scribe_Collections.Look<string>(ref defsUsingCustomFilter, "defsUsingCustomFilter", LookMode.Value, new object[0]);
//            foreach (ThingDef u in units) {
            foreach (ThingDef u in new List<ThingDef>()) {
                Utils.Mess(Utils.DBF.Settings, "Expose DSU Settings: "+u.defName+" ("+Scribe.mode+")");
                string k1=u.defName;
                ExposeDSUSetting<string>(k1+"_label",ref u.label);
                ExposeDSUSetting(k1+"_maxNumStacks", ref u.GetCompProperties<Properties>().maxNumberStacks);
                ExposeDSUSetting(k1+"_maxTotalMass", ref u.GetCompProperties<Properties>().maxTotalMass);
                ExposeDSUSetting(k1+"_maxMassStoredItem", ref u.GetCompProperties<Properties>().maxMassOfStoredItem);
                ExposeDSUSetting(k1+"_showContents", ref u.GetCompProperties<Properties>().showContents);
                ExposeDSUSetting(k1+"_overlayType", ref u.GetCompProperties<Properties>().overlayType);
                StoragePriority tmpSP=u.building.defaultStorageSettings.Priority; // hard to access private field directly
                ExposeDSUSetting<StoragePriority>(k1+"_storagePriority", ref tmpSP);
                u.building.defaultStorageSettings.Priority=tmpSP;
                /*              if (defaultDSUValues.ContainsKey("DSU_"+u.defName+"_filter")) {
                    Utils.Mess(Utils.DBF.Settings, "  default filter recorded, doing Scribe_Deep");
                    Scribe_Deep.Look(ref u.building.fixedStorageSettings.filter, "DSU_"+u.defName+"_filter", null);
                    if (u.building.fixedStorageSettings.filter==null) { // we were loading/resetting, looks like
                        Utils.Mess(Utils.DBF.Settings, "  ----> default filter is now null!");
                        u.building.fixedStorageSettings.filter=(ThingFilter)defaultDSUValues["DSU_"+u.defName+"_filter"];
                        defaultDSUValues.Remove("DSU_"+u.defName+"_filter");
                    }
                } else {
                    ThingFilter tmp=null;
                    Scribe_Deep.Look(ref tmp, "DSU_"+u.defName+"_filter", null);
                    if (tmp!=null) {
                        Utils.Mess(Utils.DBF.Settings, "  Found Filter, applying.");
                        defaultDSUValues["DSU_"+u.defName+"_filter"]=u.building.fixedStorageSettings.filter;
                        u.building.fixedStorageSettings.filter=tmp;
                    }
                } */
/*                    string key="DSU_"+u.defName+"_label";
                      string value=u.label;
                      string defaultValue=(defaultDSUValues.ContainsKey(key)?(string)defaultDSUValues[key]:value);
                      Scribe_Values.Look(ref value, key, defaultValue);
                      if (defaultValue != value && !defaultDSUValues.ContainsKey(key)) {
                      defaultDSUValues[key]=defaultValue;
                      }
*/
            }
        } // ExposeDSUSettings


        public class SettingsForOneDSU : IExposable {
            SettingsForOneDSU(ThingDef def) {
                this.def=def;

            }
            ThingDef def;

            void ExposeData() {


            }
        }

    } // end SettingsPerDSUSaver

}
