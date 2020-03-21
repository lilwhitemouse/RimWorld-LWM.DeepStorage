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
            if (Utils.CanStoreMoreThanOneThingAt(map, c)) return;
            object[] args = { true, c, map, thing }; // could use __result instead of true, but same effect
            oldPatch.Invoke(null, args);
            __result = (bool)args[0]; // Not sure why C# does it this way, but whatever.
        }
    }

    /* A way to head off problems - this showed up with Avil's Common Sense
     *  See https://github.com/catgirlfighter/RimWorld_CommonSense/blob/master/Source/CommonSense/CommonSense/WorkGiver_Merge_Patch.cs
     * If a different mod changes how CanStackWith works, the merging code will put pawns in a loop where they
     * will think they can merge things in Deep Storage (because merging code looks at the defs) and then when
     * the put it into storage, they don't stack, so there's extra lying around, so the pawn picks it up because
     * they think it can be merged (because....) &c &c &c.
     * In vanilla this works because there can only be 1 stack in a cell, so the HaulToCell job fails (because
     *   they cannot stack), so the problem gets short circuited.
     * Not so in Deep Storage, where the job gets created and the loop started...
     */
    /* I originally created this patch for Common Sense, and Avil adopted the patch there.  However, on
     * further reflection....I should have it here, too...perhaps even more than there.  Rather than remove
     * it there, I'll add it here and make sure it doesn't double-patch */
    [HarmonyPatch(typeof(WorkGiver_Merge), "JobOnThing")]
    static class Fix_Vanilla_WorkGiver_Merge {
        // Replace WorkGiver_Merge's JobOnThing's test of "if (thing.def==t.def) ..." with
        //                                                "if (thing.CanStackWith(t)) ..."
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            var i = 0; // using two for loops
            while (i < code.Count) {
                yield return code[i];
                i++;
                if (code.Count-i>5 && // We may not find this if someone else has patched WorkGiver_Merge
                    code[i].opcode == OpCodes.Ldfld &&
                    (FieldInfo)code[i].operand == typeof(Verse.Thing).GetField("def") &&
                    code[i + 2]?.opcode == OpCodes.Ldfld &&
                    (FieldInfo)code[i + 2].operand == typeof(Verse.Thing).GetField("def") &&
                    code[i + 3].opcode == OpCodes.Beq) {
                    // Found it!
                    // Instead of loading the two defs and checking if they are equal,
                    i++;  // advance past the .def call
                    yield return code[i]; // put the second thing on the stack
                    i = i + 2; // advance past the 2nd thing(we just added it) and its .def call
                    // Call thing.CanStackWith(t);
                    yield return new CodeInstruction(OpCodes.Callvirt, typeof(Thing).GetMethod("CanStackWith"));
                    
                    // i now points to "branch if equal"
                    CodeInstruction c = new CodeInstruction(OpCodes.Brtrue);
                    c.operand = code[i].operand; // grab its target
                    yield return c;
                    i++; // advance past beq
                    // continue returning everything:
                    break;
                }
            } // end first for loop
            for (; i < code.Count; i++)
            {
                yield return code[i];
            }
        } // end Transpler
    } // end fix for WorkGiver_Merge's JobOnThing
    
    /* A cheap cute way to post a Message, for when an xml patch operation is done :p */
    public class PatchMessage : PatchOperation {
        protected override bool ApplyWorker(System.Xml.XmlDocument xml) {
            Log.Message(message);
            return true;
        }
        protected string message;
    }

}
