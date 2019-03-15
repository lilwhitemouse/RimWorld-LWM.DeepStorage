using System;
using System.Collections.Generic;
using Harmony;
using RimWorld;
using System.Linq;
using Verse;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler

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
        static bool Prepare(HarmonyInstance instance) {
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
            if (Utils.CanStoreMoreThanOneThingAt(map, c)) return;
            object[] args = { true, c, map, thing }; // could use __result instead of true, but same effect
            oldPatch.Invoke(null, args);
            __result = (bool)args[0]; // Not sure why C# does it this way, but whatever.
        }
    }

    /*****************  RimWorld CommonSense (avil, aka CGFighter)  **********
     * Fix a problem with pawns trying to merge foodstuff that cannot merge
     *   when the foodstuff is in DeepStorage
     */
    [HarmonyPatch(typeof(WorkGiver_Merge), "JobOnThing")]
    static class Compatibility_RimWorld_CommonSense {
        public static bool Prepare(HarmonyInstance instance) {
            // Look for RimWorld_CommonSense...hope there isn't anyone else naming one Common Sense...
            if (!ModLister.HasActiveModWithName("Common Sense")) {
                return false;
            }
            Log.Message("LWM DeepStorage: Activating Compatibility Patch for CGFighter's Common Sense");
            return true;
        }
        // Replace WorkGiver_Merge's JobOnThing's test of "if (thing.def==t.def) ..." with
        //                                                "if (thing.CanStackWith(t)) ..."
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var code = new List<CodeInstruction>(instructions);
            var i = 0; // using two for loops
            while (i<code.Count) {
                yield return code[i];
                i++;
                if (code[i].opcode == OpCodes.Ldfld &&
                    code[i].operand == typeof(Verse.Thing).GetField("def")) {
                    if (code[i+2]?.opcode == OpCodes.Ldfld &&
                        code[i+2].operand == typeof(Verse.Thing).GetField("def")) {
                        // Found it!
                        // Instead of loading the two defs and checking if they are equal,
                        i++;  // advance past the .def call
                        yield return code[i]; // put the second thing on the stack
                        i=i+2; // advance past the 2nd thing(we just added it) and its .def call
                        // Call thing.CanStackWith(t);
                        yield return new CodeInstruction(OpCodes.Callvirt, typeof(Thing).GetMethod("CanStackWith"));
                        
                        // i now points to "branch if equal"
                        CodeInstruction c = new CodeInstruction(OpCodes.Brtrue);
                        c.operand = code[i].operand; // grab it's target
                        yield return c;
                        i++; // advance past bre
                        // continue returning everything:
                        break;
                    }
                }
            } // end first for loop
            for (; i<code.Count; i++) {
                yield return code[i];
            }
        } // end Transpler

    } // end Compatibility Patch for Common Sense

    /* A cheap cute way to post a Message, for when an xml patch operation is done :p */
    public class PatchMessage : PatchOperation {
        protected override bool ApplyWorker(System.Xml.XmlDocument xml) {
            Log.Message(message);
            return true;
        }
        protected string message;
    }

}
