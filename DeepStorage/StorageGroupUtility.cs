using System;
using RimWorld;
using Verse;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using System.Collections.Generic;
using System.Linq;
using UnityEngine; // for icon for gizmo

namespace LWM.DeepStorage
{
    public static class DSStorageGroupUtility
    {
        public static IEnumerable<Gizmo> GetDSStorageGizmos()
        {
            Thing firstThing = null;
            Thing t;
            string disabled = null;
            if (Find.Selector.NumSelected == 1)
            {
                firstThing = Find.Selector.SingleSelectedThing;
                if (!IsValidDSStorageThing(firstThing)) yield break;
            }
            else
            {         // More than 1:
                int i = 0;
                // Find first IStorageGroupMember in selected things
                for (; i < Find.Selector.NumSelected; i++)
                {
                    t = Find.Selector.SelectedObjectsListForReading[i] as Thing;
                    if (IsValidDSStorageThing(t))
                    {
                        firstThing = t;
                        break;
                    }
                }
                if (firstThing == null) yield break; // nothing to work with, scram
                // Make sure selected things don't contain any other things that aren't in the same group:
                StorageGroup firstGroup = GetSTorageGroupFor(firstThing);
                for (i++; i < Find.Selector.NumSelected; i++)
                {
                    t = Find.Selector.SelectedObjectsListForReading[i] as Thing;
                    if (IsValidDSStorageThing(t))
                    {
                        if (firstGroup == null || !(firstGroup == GetSTorageGroupFor(t)))
                        {
                            disabled = "LWM.SettingsDiabledNotInGroup".Translate();
                            break; // Things not in original group, don't keep looking
                        }
                    }
                }
            }
            var a = new Command_Action
            {
                //icon = settingsIcon,
                icon = ContentFinder<Texture2D>.Get("LWM.menu_2_2_2"),
                // icon = has_Ideology?UI/Abilities/WorkDrive :
                // icon = ContentFinder<Texture2D>.Get("Things/Mote/Thought", true),
                //  icon = ContentFinder<Texture2D>.Get("UI/Commands/RenameZone", true),
                // icon = ContentFinder<Texture2D>.Get("Things/Item/Unfinished/UnfinishedGun", true),
                // as you can see, I tried a bunch of icons before settling on my own, because meh.
                defaultLabel = "LWM.StorageUnitSettings".Translate(),
                defaultDesc = "LWM.StorageUnitSettingsDesc".Translate(
                                GetDefaultLabelFor((ThingWithComps)firstThing) ?? "thing".Translate()),
                action = delegate ()
                {
                    Find.WindowStack.Add(new Dialog_CompSettings((ThingWithComps)firstThing));
                }
            };
            if (disabled != null) a.Disable(disabled);
            yield return a;
        }
        static bool IsValidDSStorageThing(Thing thing)
        {   // reusing code, reusing code!
            if (thing as ThingWithComps == null) return false;
            return GetOrTryMakeCompFrom(thing as ThingWithComps) != null;
        }

        public static CompDeepStorage GetOrTryMakeCompFromGroupMember(IStorageGroupMember sgm)
        {
            if (!(sgm is ThingWithComps thing)) return null;
            return GetOrTryMakeCompFrom(thing);
        }
        public static CompDeepStorage GetOrTryMakeCompFrom(ThingWithComps thing)
        {
            Utils.MessQueue(Utils.DBF.StorageGroup, "Trying to get comp for " + (thing == null ? "NULL THING" : thing.ToString() +
                       (thing.Spawned ? "; stored comps for: " + thing.Map.GetComponent<MapComponentDS>().settingsForBlueprintsAndFrames.
                       Keys.Join(k => k.ToString()) : ", which is not spawned")));
            if (thing is Building_Storage)
            {
                Utils.Mess(Utils.DBF.StorageGroup, "Returning comp from Building_Storage");
                return (thing as Building_Storage).GetComp<CompDeepStorage>();
            }
            if (!thing.Spawned)
            {
                // For storage settings, a building can remain in a group even on a different map - as long as
                //   it hasn't been spawned. So carried on a caravan, still okay. ...I think we're okay there,
                //   because only buidlings_storage can be carried on a caravan, not blueprints or frames.
                //   And once a storage building is spawned, it will only stay in the group if it's on the right map
                Utils.Mess(Utils.DBF.StorageGroup, "Thing " + thing + " is not spawned; cannot get comp for it....");
                return null;
            }
            CompDeepStorage comp = thing.Map.GetComponent<MapComponentDS>().settingsForBlueprintsAndFrames.TryGetValue(thing);
            if (comp != null)
            {
                Utils.Mess(Utils.DBF.StorageGroup, "Found Map stored comp");
                return comp;
            }
            Properties compProp = (thing as Blueprint_Storage)?.BuildDef?.comps?.
                                    OfType<DeepStorage.Properties>().FirstOrDefault();
            if (compProp == null)
            { // try again with Frame:
                Utils.Mess(Utils.DBF.StorageGroup, "Wasn't Blueprint_Storage, looking for frame");
                compProp = ((thing as Frame)?.def.entityDefToBuild as ThingDef)?.comps?.
                                    OfType<DeepStorage.Properties>()?.FirstOrDefault();
            }
            if (compProp != null)
            {
                Utils.Mess(Utils.DBF.StorageGroup, "Found comp properties; creating new comp, storing, and returning");
                comp = new CompDeepStorage();
                comp.Initialize(compProp);
                comp.parent = thing; // can't cause any harm
                thing.Map.GetComponent<MapComponentDS>().settingsForBlueprintsAndFrames[thing] = comp;
                return comp;
            }
            Utils.Warn(Utils.DBF.StorageGroup, "Could not make a comp!!!!!!!!!!!!!");
            return null;
        }
        static StorageGroup GetSTorageGroupFor(Thing t)
        {
            if (t is IStorageGroupMember isgm) return isgm.Group;
            return (t as Frame)?.storageGroup;
        }

        public static IEnumerable<CompDeepStorage> GetDSCompsPossiblyInGroupFrom(ThingWithComps parent)
        {
            if (parent is IStorageGroupMember sgm)
            {
                if (sgm.Group == null)
                {
                    yield return GetOrTryMakeCompFromGroupMember(sgm);
                    yield break;
                }
                else
                {
                    foreach (var c in GetDSCompsFromGroup(sgm.Group)) yield return c;
                    yield break;
                }
            }
            if (parent is Frame f)
            {
                if (f.storageGroup == null)
                {
                    yield return GetOrTryMakeCompFrom(f);
                    yield break;
                }
                else
                {
                    foreach (var c in GetDSCompsFromGroup(f.storageGroup)) yield return c;
                    yield break;
                }
            }

        }

        public static IEnumerable<CompDeepStorage> GetDSCompsFromGroup(StorageGroup group)
        {
            foreach (var b in group?.members)
            {
                yield return GetOrTryMakeCompFromGroupMember(b);
            }
            // FRAMES
            // Frames are not IStorageGroupMembers (??....)
            // They DO store the StorageGroup in a field, so we check all frames in our settings for that group:
            // (Once frames are added to a storage group, they have to be in our list)
            foreach (var o in group.Map.GetComponent<MapComponentDS>().settingsForBlueprintsAndFrames.Keys)
            {
                if (o is Frame f && f.storageGroup == group)
                {
                    yield return group.Map.GetComponent<MapComponentDS>().settingsForBlueprintsAndFrames[o];
                }
            }
        }

        public static CompDeepStorage GetCompFor(IStorageGroupMember m)
        {
            if (m is Blueprint_Storage bps) return bps.Map.GetComponent<MapComponentDS>()
                         .settingsForBlueprintsAndFrames.TryGetValue(bps, null);
            if (m is Frame f) return f.Map.GetComponent<MapComponentDS>()
                         .settingsForBlueprintsAndFrames.TryGetValue(f, null);
            return (m as ThingWithComps)?.TryGetComp<CompDeepStorage>();
        }

        public static String GetDefaultLabelFor(ThingWithComps thing)
        {
            if (thing is Blueprint_Storage bps)
            {
                return bps.BuildDef.label;
            }
            if (thing is Frame frame)
            {
                return frame.LabelEntityToBuild;
            }
            return thing.def.label;
        }

    }
}