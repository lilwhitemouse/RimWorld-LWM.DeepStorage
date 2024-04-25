using System;
using RimWorld;
using Verse;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using System.Collections.Generic;
using System.Linq;

namespace LWM.DeepStorage
{
    static class Patch_StorageGroupUtility_SetStorageGroup
    {
        [HarmonyPatch(typeof(RimWorld.StorageGroupUtility), "SetStorageGroup")]
        public static class Patch_IStorageGroupMember_SetStorageGroup
        {
            public static void Prefix(StorageGroup newGroup, IStorageGroupMember member)
            {
                // We don't care if storage is leaving a storage group; we don't do anything in that case!
                // Plus, Frame will sometimes set storage groups to null for despawned objects, which will
                // cause problems for us if we aren't careful...
                if (newGroup != null)
                {
                    Utils.Warn(Utils.DBF.StorageGroup, "SetStorageGroup for " + member + " to group that has: " +
                                  newGroup.members.Join());
                    var cds = DSStorageGroupUtility.GetOrTryMakeCompFromGroupMember(member);
                    if (cds != null && newGroup.members.Count > 0)
                    {
                        Utils.Mess(Utils.DBF.StorageGroup, "  Copying settings from " + newGroup.members[0]);
                        cds.CopySettingsFrom(DSStorageGroupUtility.GetOrTryMakeCompFromGroupMember(newGroup.members[0]));
                    }
                }
            }
        }
    }
}
