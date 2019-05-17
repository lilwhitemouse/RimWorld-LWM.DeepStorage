using System;
using RimWorld;
using Verse;
using Verse.AI;
using Harmony;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/*   If a DSU is above capacity, the extra items should get moved */


namespace LWM.DeepStorage
{
/********************
 * Patch RimWorld's StoreUtility's TryFindBestBetterStoreCellFor
 *   Add a check to see if the item fits (maybe it's in a DSU but over capacity!).  
 *   If it doesn't, then the test for storage priority automatically fails...
 *
 * In the code, right after
 *   StoragePriority storagePriority = currentPriority;
 * Add
 *   bool overCapacity = Patch_Etc.OverCapacity(map, thing, ref storagePriority);
 *
 * Then when vanilla checks if (...||  priority <= currentPriority), we change that
 * to if (...|| (!overCapacity && priority <= currentPriority)), so that if we are 
 * overCapacity, we continue to search for a better place to put it.
 */
    [HarmonyPatch(typeof(StoreUtility), "TryFindBestBetterStoreCellFor")]
    static class Patch_TryFindBestBetterStoreCellFor {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
                                                       ILGenerator generator) {
            List<CodeInstruction> code=instructions.ToList();
            /* First, we need to set up a local variable: */
            LocalBuilder overCapacity=generator.DeclareLocal(typeof(bool));
            int i=0;
            for (i; i<code.Count;i++) {
                yield return code[i];
                if (code[i].opcode==OpCodes.Stloc_1){ // StoragePriority storagePriority = currentPriority;
                    yield return new CodeInstruction(OpCodes.Ldarg_2); // map
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // thing
                    yield return new CodeInstruction(OpCodes.Ldloca_S,1); // ref to storagePriority
                    yield return new CodeInstruction(OpCodes.Call,
                                                     Harmony.AccessTools.Method(typeof(Patch_TryFindBestBetterStoreCellFor),"OverCapacity"));
                    yield return new CodeInstruction(OpCodes.Stloc_S, overCapacity);//overCapacty=OverCapacity(map,thing,ref storagePriority)
                    i++; // move past Stloc_1; we aleady did that
                    break;
                }
            }
            if (i==code.Count) Log.Error("LWM.DeepStorage: TryFindBestBetterStoreCellFor transpile failed(0)");
            // Replace:
            /*
              if (priority < storagePriority || priority <= currentPriority)
              {
                break;
              }
              * with
              if (priority < storagePriority || (!overCapacity && priority <= currentPriority))
              */
            for (;i<code.Count;i++) { // Go until find the storage priority for slotGroup (priority) and StLoc it:
                if (code[i].opcode==OpCodes.Stloc_S && ((LocalBuilder)code[i].operand).LocalIndex==7) {
                    break;
                }
                yield return code[i];
            }
            if (i==code.Count) Log.Error("LWM.DeepStorage: TryFindBestBetterStoreCellFor transpile failed(1)");
            // skip only a few lines, but I want to make sure I catch
            //     priority <= currentPriority
            for (;i<code.Count;i++) {
                if (code[i].opcode==OpCodes.Ldloc_S && ((LocalBuilder)code[i].operand).LocalIndex==7 &&
                    code[i+1].opcode==OpCodes.Ldarg_3 && code[i+2].opcode==OpCodes.Bgt) {
                    yield return new CodeInstruction(OpCodes.Ldloc_S,overCapacity);
                    yield return new CodeInstruction(OpCodes.Brtrue,code[i+2].operand);
                    break;
                }
                yield return code[i];
            }
            if (i==code.Count) Log.Error("LWM.DeepStorage: TryFindBestBetterStoreCellFor transpile failed(2)");
            // All done!
            for (;i<code.Count;i++)
                yield return code[i];
        }

        static bool OverCapacity(Map map, Thing thing, ref StoragePriority storagePriority) {
            if (!thing.Spawned) return false;
            if (storagePriority == StoragePriority.Unstored) return false;                
            CompDeepStorage cds = (thing.Position.GetSlotGroup(map)?.parent as ThingWithComps)?.GetComp<CompDeepStorage>();
            if (cds == null) return false;
            
            if (cds.limitingFactorForItem > 0f) {
                if (thing.GetStatValue(cds.stat) > cds.limitingFactorForItem) {
                    storagePriority=StoragePriority.Unstored;
                    return true;
                }
            }
            float totalWeightStoredHere=0f;  //mass, or bulk, etc.
            var stacksStoredHere=0;

            List<Thing> l=map.thingGrid.ThingsListAt(thing.Position);
            for (int i=0; i<l.Count;i++) {
                Thing thingInStorage=l[i];
                // TODO: Decide how to handle weight: if thing is stackable, do I want to take some out, if it goes over?
                if (thing == thingInStorage) return false; // it's here, and not over capacity yet!
                if (thingInStorage.def.EverStorable(false)) { // an "item" we care about
                    stacksStoredHere++;
                    if (cds.limitingTotalFactorForCell > 0f) {
                        totalWeightStoredHere+=thingInStorage.GetStatValue(cds.stat)*thingInStorage.stackCount;
                        if (totalWeightStoredHere > cds.limitingTotalFactorForCell &&
                            stacksStoredHere >= cds.minNumberStacks) {
                            storagePriority=StoragePriority.Unstored;
                            return true;  // this takes us to capacity, and we haven't hit thing
                        }
                    }
                    if (stacksStoredHere >= cds.maxNumberStacks) { // breaks if minNumberStacks > maxNumberStacks. I'm okay with this
                        Log.Message("---------------------LWM.DeepStorage: "+thing+" is over capacity at "+thing.Position);
                        storagePriority=StoragePriority.Unstored;
                        return true;  // this takes us to capacity, and we haven't hit thing
                    }
                } // if storable
            } // end list
            return false;
        }
        static void Postfix(Thing t, bool __result) {
            Log.Message("    Found better place for "+t+"? "+__result);
        }
    }
    [HarmonyPatch(typeof(StoreUtility), "IsInValidBestStorage")]
    static class Patch_IsInValidBestStorage {
        static void Postfix(ref bool __result, Thing t) {
            Log.Message("Is "+t+" in Valid Best Storage? "+__result);
        }

    }
    [HarmonyPatch(typeof(StoreUtility), "TryFindBestBetterStoreCellForWorker")]
    static class Patch_TryFindBestBetterStoreCellForWorker {
        static void Postfix(Thing t) {
            Log.Message("TFBBSCFWorker: "+t);
        }
        static void PostfixXX(Thing t, SlotGroup slotGroup, StoragePriority foundPriority) {
            Log.Message("TryFindBestBetterStoreCellForWorker found "+t+" a place in "+slotGroup+" at "+foundPriority);
        }
    }

}
