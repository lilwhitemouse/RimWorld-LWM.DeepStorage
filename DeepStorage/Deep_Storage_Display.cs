using System;
using Harmony;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using UnityEngine; // because graphics.


namespace LWM.DeepStorage
{
    /*********************************************
     * Display   
     * 
     * Make giant piles of Deep Storage stuff look tider!
     * 
     *********************************************/

    /**********************************
     * SectionLayer_Things's Regnerate()
     *
     * Graphics with MapMeshOnly (for example, stone chunks, slag, ...?)
     * are drawn by the Things SectionLayer itself.  We patch the Regenerate
     * function so that any items in a DSU are invisible.
     *
     * Specifically, we use Harmony Transpiler to change the thingGrid.ThingsListAt 
     * to our own list, that doesn't include any things in storage.
     *
     * TODO?  Only do the transpilation if there are actually any DSUs that
     * store MapMeshOnly items?
     */

    [HarmonyPatch(typeof(SectionLayer_Things), "Regenerate")]
    public static class PatchDisplay_SectionLayer_Things_Regenerate {
        // We change thingGrid.ThingsListAt(c) to DisplayThingList(map, c):
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var flag=false;
            var code = new List<CodeInstruction>(instructions);
            int i = 0;
            var lookingForThisFieldCall = Harmony.AccessTools.Field(typeof(Verse.Map), "thingGrid");
            for (;i<code.Count;i++) {
                if (code[i].opcode != OpCodes.Ldfld || code[i].operand != lookingForThisFieldCall) {
                    yield return code[i];
                    continue;
                }
                flag=true;
                // found middle of List<Thing> list = base.Map.thingGrid.ThingsListAt(c);
                // We are at the original instruction .thingGrid
                //   and we have the Map on the stack
                i++;  // go past thingGrid instruction
                // Need the location c on the stack, but that's what happens next in original code - loading c
                yield return code[i];
                i++; // now past c
                // Next code instruction is to call ThingsListAt.
                i++; // We want our own list
                yield return new CodeInstruction(OpCodes.Call, Harmony.AccessTools.Method(
                                                     "LWM.DeepStorage.PatchDisplay_SectionLayer_Things_Regenerate:DisplayThingList"));
                break; // that's all we need to change!
            }
            for (;i<code.Count; i++) {
                yield return code[i];
            }
            if (!flag) Log.Error("LWM Deep Storage: Haromony Patch for SectionLayer display failed.  This is display-only, the game is still playable.");
            yield break;
        }

        static public List<Thing> DisplayThingList(Map map, IntVec3 loc) {
            CompDeepStorage cds;
            ThingWithComps building;
            SlotGroup slotGroup=loc.GetSlotGroup(map);

            if (slotGroup==null || (building=(slotGroup.parent as ThingWithComps))==null ||
                (cds=(slotGroup.parent as ThingWithComps).TryGetComp<CompDeepStorage>())==null||
                cds.showContents)
            {
                return map.thingGrid.ThingsListAt(loc);
            }
            // only return non-storable things to be drawn:
            return map.thingGrid.ThingsListAt(loc).FindAll(t=>!t.def.EverStorable(false));
        }
    } // end Patch SectionLayer_Things Regenerate()

    /*****************************************
     * Making non-mesh things invisible:
     * 
     * We add checks to: Notify_ReceivedThing and to SpawnSetup,
     * and remove items from various lists:
     *   Drawable list
     *   tooltip Giver List
     *   GUI Overlay list
     * (These settings gathered from DeSpawn() and from other modders')
     *
     * Note that we don't have to do much to make them visible again!
     * When an item leaves Deep Storage, it deSpawns, and an identical one
     * is created.
     * If the DSU itself despawns, however, we need to make sure everything
     * is visible!
     * TODO: if DSU despawns, dirty map mesh, add things back to lists, etc.
     */

    
    // Make non-mesh things invisible: they have to be de-registered on being added to a DSU:
    [HarmonyPatch(typeof(Building_Storage),"Notify_ReceivedThing")]
    public static class PatchDisplay_Notify_ReceivedThing {
        public static void Postfix(Building_Storage __instance,Thing newItem) {
            CompDeepStorage cds;
            if ((cds = __instance.TryGetComp<CompDeepStorage>()) == null) return;
            if (cds.showContents) return;

            if (newItem.def.drawerType != DrawerType.MapMeshOnly) {
                __instance.Map.dynamicDrawManager.DeRegisterDrawable(newItem);
            }
            __instance.Map.tooltipGiverList.Notify_ThingDespawned(newItem);
            
            // Note: not removing linker here b/c I don't think it's applicable to DS?
            
            // Remove gui overlay - this includes number of stackabe item, quality, etc
            __instance.Map.listerThings.ThingsInGroup(ThingRequestGroup.HasGUIOverlay).Remove(newItem);

            newItem.DirtyMapMesh(newItem.Map); // for items with a map mesh
        }
    }

    // Make non-mesh things invisible when loaded in Deep Storage
    [HarmonyPatch(typeof(Building_Storage), "SpawnSetup")]
    public static class PatchDisplay_SpawnSetup {
        public static void Postfix(Building_Storage __instance, Map map) {
            CompDeepStorage cds;
            if ((cds = __instance.TryGetComp<CompDeepStorage>()) == null) return;
            if (cds.showContents) return;
            
            foreach (IntVec3 cell in __instance.AllSlotCells()) {
                foreach (Thing thing in map.thingGrid.ThingsAt(cell)) {
                    if (!thing.Spawned || !thing.def.EverStorable(false)) continue; // don't make people walking past be invisible...
                    if (thing.def.drawerType != DrawerType.MapMeshOnly) {
                        map.dynamicDrawManager.DeRegisterDrawable(thing);
                    }
                    map.tooltipGiverList.Notify_ThingDespawned(thing);

                    // Don't need to thing.DirtyMapMesh(map); because of course it's dirty on spawn setup ;p

                    // Do remove gui overlay - this includes number of stackabe item, quality, etc
                    map.listerThings.ThingsInGroup(ThingRequestGroup.HasGUIOverlay).Remove(thing);
                }
            }
        }
    }

    /**************** GUI Overlay ***************/
    [HarmonyPatch(typeof(Thing),"DrawGUIOverlay")]
    static class Add_DSU_GUI_Overlay {
        static IntVec2 vOne = new IntVec2(1,1);
        static bool Prefix(Thing __instance) {
            Building_Storage DSU = __instance as Building_Storage;
            if (DSU == null) return true;
            CompDeepStorage cds = DSU.GetComp<CompDeepStorage>();
            if (cds == null) return true;
            if (cds.cdsProps.guiOverlay == LWM.DeepStorage.GuiOverlayType.Normal) return true;

            if (cds.cdsProps.guiOverlay == GuiOverlayType.CountOfItems) {
                // probably Weapons Lockers, Armor Racks, Clothing Racks etc...
                if (true||__instance.def.size == vOne) {
                    List<Thing> things=__instance.Map.thingGrid.ThingsListAtFast(__instance.Position)
                        .FindAll(t=>t.def.EverStorable(false));
                    if (AllSameType(things)) {
                        GenMapUI.DrawThingLabel(__instance,"x"+things.Count.ToStringCached());
                    } else {
                        GenMapUI.DrawThingLabel(__instance,"[ "+things.Count.ToStringCached()+" ]");
                    }
                    return false;
                }
                // size is not (1,1)
//                foreach (IntVec3 c in ...  maybe not?  Hmmm....
            }
            if (cds.cdsProps.guiOverlay == GuiOverlayType.TotalCount) {
                // probably food baskets, skips, etc...
                //   TODO: if (size is 1,1), etc??
                List<Thing>things;
                if (__instance.def.size == vOne) {
                    things=__instance.Map.thingGrid.ThingsListAtFast(__instance.Position);
                } else {
                    things=new List<Thing>();
                    foreach (IntVec3 c in DSU.slotGroup.CellsList){
                        things.AddRange(__instance.Map.thingGrid.ThingsListAtFast(__instance.Position)
                                        .FindAll(t=>t.def.EverStorable(false)));
                    }
                }
                // if all same type?
                return false;
            }

            return true; // TODO :p
        }
        private static bool AllSameType(List<Thing> l) {
            if (l.Count < 1) return true;
            for (int i=1; i<l.Count;i++) {
                if (l[i].def != l[0].def) return false;
            }
            return true;
        }
    }


}
