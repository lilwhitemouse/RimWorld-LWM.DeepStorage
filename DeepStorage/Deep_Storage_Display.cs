using System;
using Harmony;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler


namespace LWM.DeepStorage
{
    /*********************************************
     * Display   
     * 
     * Make giant piles of Deep Storage stuff look tider!
     * 
     *********************************************/
    [HarmonyPatch(typeof(SectionLayer_Things), "Regenerate")]
    public static class PatchDisplay_SectionLayer_Things_Regenerate {
        public static void Prefix (SectionLayer_Things __instance, Section ___section) {
            Log.Error("Regenerate: "+__instance.ToString()+___section.botLeft);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            // Once a cell is loaded (c), before we do anything else, we test to see if in
            //   Deep Storage.  If so, we want to jump to the next ienumerator & skip this loop
            var code = new List<CodeInstruction>(instructions);
            var i = 0; // ???
            for (;i<code.Count; i++) {
                yield return code[i];
                if (code[i].opcode == OpCodes.Stloc_2) {
                    // Just stored the enumerator for IntVec3 (foreach c in this.section.CellRect)                    
                    i++;
                    // now pointing at br to the code to get the next c
                    // We want the branch label.
                    System.Reflection.Emit.Label branchLabel;
                    if (code[i].opcode != OpCodes.Br) {
                        Log.Error("LWM Deep Storage: Transpiling SectionLayer_Things's Regenerate failed ;_;");
                        throw new Exception("LWM Deep Storage: failed to find branch label; maybe incompatable patching?");
                    }
                    branchLabel = (Label)code[i].operand;
                    yield return code[i]; // jump to the branch to start the foreach loop
                    i++;
                    // Now, fast forward until we store to location 1 - that's storing IntVec3 we want to test
                    for (; i<code.Count; i++) {
                        yield return code[i];
                        if (code[i].opcode == OpCodes.Stloc_1) {
                            i++;
                            break;
                        }
                    }
                    // just found "foreach (IntVec3 c in this.section.CellRect) {..."
                    // now insert: "if (PatchDisplay_SectionLayer_Things_Regenerate(this,base.Map,c) { continue; }
                    // Get this SectionLayer_Things:
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    // Get map:
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    var gm=Harmony.AccessTools.Method("Verse.SectionLayer:get_Map"); // (AccessTools happily finds private getters)
                    if (gm==null) {
                        Log.Error("LWM Deep Storage failed to get Map from Verse.SectionLayer");
                    }
                    yield return  new CodeInstruction(OpCodes.Call, gm);
                    // get location
                    yield return  new CodeInstruction(OpCodes.Ldloc_1); // Our cell
                    // call our check:
                    yield return  new CodeInstruction(OpCodes.Call, Harmony.AccessTools.Method(
                                "LWM.DeepStorage.PatchDisplay_SectionLayer_Things_Regenerate:DeepStorage_WillHandleDrawing"));
                    // if true, skip this WHOOOOOLE section
                    yield return  new CodeInstruction(OpCodes.Brtrue, branchLabel);
                    break;
                    // i has already been ++ed
                }
            }
            for (;i<code.Count;i++) {
                yield return code[i];
            }
            yield break;
        } // end transpiler

        public static bool DoesItCount(Thing t) {
            return t.def.EverStorable(false);
        }
        public static bool DeepStorage_WillHandleDrawing(SectionLayer_Things layer, Map map, IntVec3 loc) {
            CompDeepStorage cds;
            ThingWithComps t;
            SlotGroup slotGroup=loc.GetSlotGroup(map);
            if (slotGroup==null || (t=(slotGroup.parent as ThingWithComps))==null ||
                (cds=(slotGroup.parent as ThingWithComps).TryGetComp<CompDeepStorage>())==null)
            {
                return false;
            }
            // TODO: check logic for whether this DSU hides contents:
            // TODO: power?  Non-haulables?  Ugh.

            // We will handle drawing here:
            List<Thing> list = map.thingGrid.ThingsListAt(loc);
            if (Utils.CanStoreMoreThanOneThingAt(map, loc)) {
                list.RemoveAll(th=>th.def.EverStorable(false));
            }
            int count = list.Count;
            for (int i = 0; i < count; i++) {
                Log.Error("Hi");
            }
            
            //   Last check, because DSUs that take up multiple spaces might already be drawn:
            if (loc.x == t.Position.x && loc.z == t.Position.z) {
                // Code taken from SectionLayer_ThingsGeneral
                try
                {
                    t.Print(layer);
                }
                catch (Exception ex)
                {
                    Log.Error(string.Concat(new object[] {
                            "LWM:Exception printing ", t, " at ", t.Position, ": ", ex.ToString()
                        }), false);
                }
            }
            return true;
        }
    }


    // Make non-mesh things invisible: they have to be de-registered on being added to a DSU:
    [HarmonyPatch(typeof(Building_Storage),"Notify_ReceivedThing")]
    public static class PatchDisplay_Notify_ReceivedThing {
        public static void Postfix(Building_Storage __instance,Thing newItem) {
            CompDeepStorage cds;
            if ((cds = __instance.TryGetComp<CompDeepStorage>()) == null) return;
            // TODO: test whether this DSU hides things:
            if (newItem.def.drawerType != DrawerType.MapMeshOnly) {
                __instance.Map.dynamicDrawManager.DeRegisterDrawable(newItem);
            }
            __instance.Map.tooltipGiverList.Notify_ThingDespawned(newItem);
            // Leaving linker b/c I don't think it's applicable?
            // Remove gui overlay - this includes number of stackabe item, quality, etc
//            __instance.Map.listerThings.ThingsInGroup(ThingRequestGroup.HasGUIOverlay).Remove(newItem);
            
        }
    }

    // Make non-mesh things invisible when loaded in Deep Storage
    [HarmonyPatch(typeof(Building_Storage), "SpawnSetup")]
    public static class PatchDisplay_SpawnSetup {
        public static void Postfix(Building_Storage __instance, Map map) {
            CompDeepStorage cds;
            if ((cds = __instance.TryGetComp<CompDeepStorage>()) == null) return;

//            __instance.DirtyMapMesh(map);

            foreach (IntVec3 cell in __instance.AllSlotCells()) {
                foreach (Thing thing in map.thingGrid.ThingsAt(cell)) {
                    if (!thing.Spawned || !thing.def.EverStorable(false)) continue; // don't make people walking past be invisible...
                    if (thing.def.drawerType != DrawerType.MapMeshOnly) {
                        map.dynamicDrawManager.DeRegisterDrawable(thing);
                    } else {
//                        thing.def.EverTransmitsPower
//                        thing.def
                    }
                    map.tooltipGiverList.Notify_ThingDespawned(thing);
                }
            }
        }
    }


}
