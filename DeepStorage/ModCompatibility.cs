using System;
using HarmonyLib;
using RimWorld;
using System.Linq;
using Verse;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Emit;  // Transpiler OpCodes

namespace LWM.DeepStorage
{
    /*****************************************************
     *               Compatibility                       *
     *    Because sometimes mods step on each others'    *
     *    toes, or fail to meet up with each other.      *
     *                                                   *
     *****************************************************/

    /*****************  RimWorld Search Agency  **********/
    /*
     * RSA contains Hauling Hysteresis, which also messes
     * with NoStorageBlockersIn.  If the hysteresis critera
     * are not met (e.g., 55 sheep in a stack, when it is
     * set to 50), NoStorageBlockersIn is forced to return
     * false...  Which is good, except for Deep Storage
     * (or other storage mods)
     *
     * If RSA is active, we unpatch NoStorageBlockersIn and
     * apply our own patch for the RSA effect.
     */

     [HarmonyPatch(typeof(StoreUtility), "NoStorageBlockersIn")]
     static class Fix_RSA_Incompatibilit_NoStorageBlockersIn {
        public static MethodInfo oldPatch;
        static bool Prepare(Harmony instance) {
            // can we find the RSA mod?
            var rsaAssembly = AppDomain.CurrentDomain.GetAssemblies().ToList()
              .Find(x => x.FullName.Split(',').First() == "RSA");
            if (rsaAssembly == null) return false; // No RSA...
            var method = AccessTools.Method(typeof(StoreUtility), "NoStorageBlockersIn");
            Log.Message("LWM DeepStorage: Activating Compatibility Patch for RimWorld Search Agency");
            oldPatch = AccessTools.Method(rsaAssembly.GetType("RSA.StoreUtility_NoStorageBlockersIn"), "FilledEnough");
            if (oldPatch == null) {
                Log.Error("LWM DeepStorage: Thought I had RimWorld Search Agency, but no FilledEnough. Patch failed, please let author (LWM) know.");
                return false;
            }
            LongEventHandler.ExecuteWhenFinished(delegate // why not unpatch now? ...at least this works?
            {
                instance.Unpatch(method, oldPatch);
            });
            return true;
        }
        static void Postfix(ref bool __result, IntVec3 c, Map map, Thing thing) {
            if (!__result) return;
            // Simply disable hauling hysterisis for DeepStorage:
            // TODO: more complicated fix would be good:
            if (Utils.GetDeepStorageOnCell(c, map, out CompDeepStorage comp))
            {
                __result = comp.StackableAt(thing, c, map);
                return;
            }

            object[] args = { true, c, map, thing }; // could use __result instead of true, but same effect
            oldPatch.Invoke(null, args);
            __result = (bool)args[0]; // Not sure why C# does it this way, but whatever.
        }
    }

    /* A cheap cute way to post a Message, for when an xml patch operation is done :p */
    public class PatchMessage : PatchOperation {
        protected override bool ApplyWorker(System.Xml.XmlDocument xml) {
            Log.Message(message);
            return true;
        }
        protected string message;
    }

}
