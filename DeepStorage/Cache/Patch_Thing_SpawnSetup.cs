using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LWM.DeepStorage
{
    /// <summary>
    /// Patch to stop the process of spawning if the underlying thing is DeSpawned because it is absorbed into stack.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.SpawnSetup))]
    public class Patch_Thing_SpawnSetup
    {
        private static readonly MethodInfo _notifyMethod =
            typeof(ISlotGroupParent).GetMethod(nameof(ISlotGroupParent.Notify_ReceivedThing));

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codeInstructions, ILGenerator ilGenerator) {

            List<CodeInstruction> code = new List<CodeInstruction>(codeInstructions);
            Label retLabel = ilGenerator.DefineLabel();
            code.Last().labels.Add(retLabel);

            for (int i = 0; i < code.Count; i++)
            {
                yield return code[i];
                if (code[i].opcode == OpCodes.Callvirt && code[i].OperandIs(_notifyMethod))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Thing
                    yield return new CodeInstruction(OpCodes.Call, typeof(Thing).GetProperty(nameof(Thing.Spawned)).GetMethod); // Thing.Spawned()
                    yield return new CodeInstruction(OpCodes.Brfalse, retLabel);
                }
            }
        }
    }
}
