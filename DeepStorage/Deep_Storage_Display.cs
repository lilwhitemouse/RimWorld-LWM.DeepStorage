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
                                                     "LWM.DeepStorage.PatchDisplay_SectionLayer_Things_Regenerate:ThingListToDisplay"));
                break; // that's all we need to change!
            }
            for (;i<code.Count; i++) {
                yield return code[i];
            }
            if (!flag) Log.Error("LWM Deep Storage: Haromony Patch for SectionLayer display failed.  This is display-only, the game is still playable.");
            yield break;
        }

        static public List<Thing> ThingListToDisplay(Map map, IntVec3 loc) {
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

            /****************** Put DSU at top of list *******************/
            /*  This is important for selecting next objects?  I think?  */
            List<Thing> list = newItem.Map.thingGrid.ThingsListAt(newItem.Position);
//TODO: SpawnSetup, too...
            list.Remove(__instance);
            list.Add(__instance);
//            list.Insert(0, __instance);

            /****************** Set display for items correctly *******************/

            if (cds.cdsProps.overlayType != GuiOverlayType.Normal || !cds.showContents) {
                // Remove gui overlay - this includes number of stackabe item, quality, etc
                __instance.Map.listerThings.ThingsInGroup(ThingRequestGroup.HasGUIOverlay).Remove(newItem);
            }
            
            if (cds.showContents) return;

            if (newItem.def.drawerType != DrawerType.MapMeshOnly) {
                __instance.Map.dynamicDrawManager.DeRegisterDrawable(newItem);
            }
            __instance.Map.tooltipGiverList.Notify_ThingDespawned(newItem); // should this go with guioverlays?
            
            // Note: not removing linker here b/c I don't think it's applicable to DS?
            
            newItem.DirtyMapMesh(newItem.Map); // for items with a map mesh; probably unnecessary?
        }
    }

    // Make non-mesh things invisible when loaded in Deep Storage
    [HarmonyPatch(typeof(Building_Storage), "SpawnSetup")]
    public static class PatchDisplay_SpawnSetup {
        public static void Postfix(Building_Storage __instance, Map map) {
            CompDeepStorage cds;
            if ((cds = __instance.TryGetComp<CompDeepStorage>()) == null) return;
            
            foreach (IntVec3 cell in __instance.AllSlotCells()) {
                List<Thing> list = map.thingGrid.ThingsListAt(cell);
                foreach (Thing thing in list) {
                    if (!thing.Spawned || !thing.def.EverStorable(false)) continue; // don't make people walking past be invisible...

                    if (cds.cdsProps.overlayType != GuiOverlayType.Normal || !cds.showContents) {
                        // Remove gui overlay - this includes number of stackabe item, quality, etc
                        map.listerThings.ThingsInGroup(ThingRequestGroup.HasGUIOverlay).Remove(thing);
                    }
                    if (thing.def.drawerType != DrawerType.MapMeshOnly) {
                        map.dynamicDrawManager.DeRegisterDrawable(thing);
                    }
                    if (cds.showContents) {
                        map.dynamicDrawManager.RegisterDrawable(thing);
                        continue;
                    }


                    map.tooltipGiverList.Notify_ThingDespawned(thing); // should this go with guioverlays?

                    // Don't need to thing.DirtyMapMesh(map); because of course it's dirty on spawn setup ;p
                } // end cell
                // Now put the DSU at the top of the ThingsList here:
                list.Remove(__instance);
//                list.Insert(0, __instance);
                list.Add(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(Verse.Thing),"get_DrawPos")]
    static class Ensure_Top_Item_In_DSU_Draws_Correctly {
        static void Postfix(Thing __instance, ref Vector3 __result) {
            CompDeepStorage cds;
            if (__instance.Map == null || __instance.Position == IntVec3.Invalid || (__instance is Building) ||
                (cds=((__instance.Position.GetSlotGroup(__instance.Map)?.parent) as ThingWithComps)?.TryGetComp<CompDeepStorage>())==null)
                return;
            // Is it the last thing in the list of stored things?
            List<Thing> l = __instance.Map.thingGrid.ThingsListAt(__instance.Position);
            for (int i=l.Count-1; i>0; i--) {
                if (l[i] is Building) continue;
                if (l[i] == __instance) {
                    __result.y+=0.05f;
                }
                if (l[i].def.EverStorable(false)) return; // not last.
            }
        }
    }

    
    /**************** GUI Overlay *****************/
    [HarmonyPatch(typeof(Thing),"DrawGUIOverlay")]
    static class Add_DSU_GUI_Overlay {
        static bool Prefix(Thing __instance) {
            if (Find.CameraDriver.CurrentZoom != CameraZoomRange.Closest) return false;
            Building_Storage DSU = __instance as Building_Storage;
            if (DSU == null) return true;
            CompDeepStorage cds = DSU.GetComp<CompDeepStorage>();
            if (cds == null) return true;
            if (cds.cdsProps.overlayType == LWM.DeepStorage.GuiOverlayType.Normal) return true;

            List<Thing> things;
            String s;
            if (cds.cdsProps.overlayType == GuiOverlayType.CountOfAllStacks) {
                // probably Armor Racks, Clothing Racks, Weapon Lockers etc...
                things = new List<Thing>();
                foreach (IntVec3 c in DSU.AllSlotCellsList()) {
                    things.AddRange(__instance.Map.thingGrid.ThingsListAtFast(c).FindAll(t=>t.def.EverStorable(false)));
                }

                if (things.Count ==0) {
                    if (cds.cdsProps.showContents) return false;  // If it's empty, player will see!
                    s="LWM_DS_Empty".Translate();
                } else if (things.Count ==1)
                    s=1.ToStringCached(); // Why not s="1";?  You never know, someone may be playing in...
                else if (AllSameType(things))
                    s="x"+things.Count.ToStringCached();
                else
                    s="[ "+things.Count.ToStringCached()+" ]";
                GenMapUI.DrawThingLabel(GenMapUI.LabelDrawPosFor(__instance,0f),s,GenMapUI.DefaultThingLabelColor);
                return false;
            }

            if (cds.cdsProps.overlayType == GuiOverlayType.CountOfStacksPerCell) {
                // maybe Armor Racks, Clothing Racks?
                foreach (IntVec3 c in DSU.AllSlotCellsList()) {
                    things=__instance.Map.thingGrid.ThingsListAtFast(c).FindAll(t=>t.def.EverStorable(false));
                    if (things.Count ==0) {
                        if (cds.cdsProps.showContents) continue; // if it's empty, player will see!
                        s="LWM_DS_Empty".Translate();
                    } else if (things.Count ==1)
                        s=1.ToStringCached(); // ..a language that doesn't use arabic numerals?
                    else if (AllSameType(things))
                        s="x"+things.Count.ToStringCached();
                    else
                        s="[ "+things.Count.ToStringCached()+" ]";
                    GenMapUI.DrawThingLabel(GenMapUI.LabelDrawPosFor(c),s,GenMapUI.DefaultThingLabelColor);
                }
                return false;
            }
            if (cds.cdsProps.overlayType == GuiOverlayType.SumOfAllItems) {
                // probably food baskets, skips, etc...
                things=new List<Thing>();
                foreach (IntVec3 c in DSU.slotGroup.CellsList){
                    things.AddRange(__instance.Map.thingGrid.ThingsListAtFast(c)
                                    .FindAll(t=>t.def.EverStorable(false)));
                }

                if (things.Count ==0) {
                    if (cds.cdsProps.showContents) return false;  // if it's empty, player will see
                    s="LWM_DS_Empty".Translate();
                } else {
                    int count=things[0].stackCount;
                    bool allTheSame=true;
                    for (int i=1; i<things.Count; i++) {
                        if (things[i].def != things[0].def) allTheSame=false;
                        count+=things[i].stackCount;
                    }
                    if (allTheSame)
                        s=count.ToStringCached();
                    else
                        s="[ "+count.ToStringCached()+" ]";
                }
                GenMapUI.DrawThingLabel(GenMapUI.LabelDrawPosFor(__instance,0f),s,GenMapUI.DefaultThingLabelColor);                
                return false;
            }
            Log.Warning("LWM DeepStorage: could not find GuiOverlayType of "+cds.cdsProps.overlayType);
            return true;
        }
        private static bool AllSameType(List<Thing> l) {
            if (l.Count < 2) return true;
            for (int i=1; i<l.Count;i++) {
                if (l[i].def != l[0].def) return false;
            }
            return true;
        }
    }


}
