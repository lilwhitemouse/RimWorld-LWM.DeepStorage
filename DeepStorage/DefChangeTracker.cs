using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace LWM.DeepStorage {
    //   Default values for defs, so that when saving mod settings, we know what the defaults are.
    public class DefChangeTracker {
        private static Dictionary<string, object> defaultDefValues=new Dictionary<string, object>();

        public bool HasAnyDefaultValues { get { return defaultDefValues.Count > 0; } }

        public void AddDefaultValue(string defName, string keylet, object defaultValue) {
            Utils.Mess(Utils.DBF.Settings,"Adding default value for "+defName+"'s "+keylet);
            defaultDefValues[defName+"_"+keylet]=defaultValue;
        }

        public T GetDefaultValue<T>(string defName, string keylet) where T : class {
            return GetDefaultValue<T>(defName, keylet, null);
        }

        public T GetDefaultValue<T>(string defName, string keylet, T defaultValue) {
            if (defaultDefValues.ContainsKey(defName+"_"+keylet))
                return (T)defaultDefValues[defName+"_"+keylet];
            return defaultValue;
        }

        public bool HasDefaultValueFor(string defName, string keylet) => defaultDefValues.ContainsKey(defName+"_"+keylet);

        public bool IsChanged(string defName) {
            foreach (string key in defaultDefValues.Keys) {
                // strip keylet off of the key
                var t=key.Split('_');
                // get only defname
                string keyDefName=string.Join("_", t.Take(t.Length-1).ToArray());
                if (keyDefName==defName) return true;
            }
            return false;
        }

        public void UpdateToNewValue<T>(string defName, string keylet, T value, ref T refToChange) where T : IComparable {
            if (value.CompareTo(refToChange) == 0) {
                Utils.Mess(Utils.DBF.Settings,"  Def "+defName+" tried to change: "+keylet+" but it's the same!");
                return;
            }
            Utils.Mess(Utils.DBF.Settings,"changing value for "+defName+"'s "+keylet+" from "+refToChange+" to "+value);
            // The value in refToChange may not be the original value: user could already have changed it once.  So:
            //    (this IS assignment by value, right?)
            T defaultValue=GetDefaultValue<T>(defName, keylet, refToChange);
            refToChange = value;
            // if the user reset/changed to original defaul value, remove the default values key
            if (defaultValue.CompareTo(value)==0) {
                defaultDefValues.Remove(defName+"_"+keylet);
                Utils.Mess(Utils.DBF.Settings, "  removing default record for item "+keylet+" ("+defName+")");
            } else if (!defaultDefValues.ContainsKey(defName+"_"+keylet)) {
                defaultDefValues[defName+"_"+keylet]=defaultValue;
                Utils.Mess(Utils.DBF.Settings, "  creating default record for item "+keylet+" ("+defName+")");
            }
        }

        public bool GetFirstDefaultValue(out string defName, out string keylet, out object o) {
            if (defaultDefValues==null || defaultDefValues.Count==0) {
                defName=null;
                keylet=null;
                o=null;
                return false;
            }
            var first=defaultDefValues.First();
            var t=first.Key.Split('_');
            // get only defName
            defName=string.Join("_", t.Take(t.Length-1).ToArray());
            // and the keylet
            keylet=t[t.Length-1];
            o=first.Value;
            return true;
        }
        public bool GetFirstDefaultValueFor(string defName, out string keylet, out object o) {
            if (defaultDefValues!=null && defaultDefValues.Count>0) {
                foreach (var entry in defaultDefValues) {
                    var t=entry.Key.Split('_');
                    string dN=string.Join("_", t.Take(t.Length-1).ToArray());
                    if (dN==defName) {
                        keylet=t[t.Length-1];
                        o=entry.Value;
                        return true;
                    }
                }
            }
            // nothing found
            keylet=null;
            o=null;
            return false;
        }

        public IEnumerable<T> GetAllWithKeylet<T>(string keylet) {
            foreach (var entry in defaultDefValues) {
                var t =entry.Key.Split('_');
                if (t[t.Length-1]==keylet) {
                    yield return (T)entry.Value;
                }
            }
            yield break;
        }

        public bool Remove(string defName, string keylet) {
            return defaultDefValues.Remove(defName+"_"+keylet);
        }

        public void ExposeSetting<T>(string defName, string keylet, ref T value) where T : IComparable {
            if (Scribe.mode==LoadSaveMode.LoadingVars) {
                T defaultValue=Settings.defTracker.GetDefaultValue(defName, keylet, value);
                Scribe_Values.Look(ref value, "DSU_"+defName+"_"+keylet, defaultValue);
                if (defaultValue.CompareTo(value)!=0) {
                    Utils.Mess(Utils.DBF.Settings,"  loaded value for "+defName+"'s "+keylet+" from settings: "+value);
                    defaultDefValues[defName+"_"+keylet]=defaultValue;
                } else {
                    if (defaultDefValues.Remove(defName+"_"+keylet)) {
                        Utils.Mess(Utils.DBF.Settings, "  loaded default value for "+defName+"'s "+keylet+" and removed record");
                    }
                }
            }
            if (Scribe.mode==LoadSaveMode.Saving) {
                if (!IsDefaultValue(defName, keylet, value)) {
                    Utils.Mess(Utils.DBF.Settings, "  Saving "+defName+"'s new value "+value);
                    Scribe_Values.Look(ref value, "DSU_"+defName+"_"+keylet, value, true); // force save
                }
            }
        }

        public void ExposeSettingDeep<T>(string defName, string keylet, ref T value) where T : class {
            if (defaultDefValues.ContainsKey(defName+"_"+keylet)) {
                Utils.Mess(Utils.DBF.Settings, "  default "+keylet+" recorded, doing Scribe_Deep");
                // if saving, save the value, all is well.
                // if loading, default valueis already in our dictionary, all is well...
                Scribe_Deep.Look(ref value, "DSU_"+defName+"_"+keylet, null);
                if (value == null) { // we were loading/resetting, loaded null
                    // ...unless it wasn't saved some how??
                    Utils.Mess(Utils.DBF.Settings, "  ----> "+keylet+" is now null!");
                    value=(T)defaultDefValues[defName+"_"+keylet];
                    defaultDefValues.Remove(defName+"_"+keylet);
                }
            } else { // no default currently saved
                T tmp=null;
                Scribe_Deep.Look(ref tmp, "DSU_"+defName+"_"+keylet, null);
                // either we loaded a new default, or we saved nothing.
                if (tmp!=null) {
                    Utils.Mess(Utils.DBF.Settings, "  Found "+keylet+", applying to "+defName);
                    defaultDefValues[defName+"_filter"]=value;
                    value=tmp;
                }
            }
        } // end ExposeSettinsgDeep

        public bool IsDefaultValue<T>(string defName, string keylet, T value) where T : IComparable {
            string key=defName+'_'+keylet;
            if (!defaultDefValues.ContainsKey(key)) return true;
            T cur=(T)defaultDefValues[key];
            return value.CompareTo(cur)==0;
        }

    }


}
