using System;
using HarmonyLib;
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
     * Multiple aspects:
     *
     * 1.  Make items invisisble if the DSU makes them invisible
     *     (patch Regenerate and modify adding items to DeepStorage
     *      to deregister drawing them)
     * 2.  Make it so the last item added to the Deep Storage,
     *     the item on top, shows on top.
     *     Why?
     *     Because the way Unity works, the very first mesh drawn
     *     is "over" later meshes drawn.  As far as I can tell,
     *     the meshes get drawn once, and then get put down
     *     multiple times - once for each object.  So the mesh for
     *     Simple meals will ALWAYS cover the mesh for Fine meals.
     *     Even if in a specific case, it is "Draw()n" later.
     *     Fix
     *     Create a HashSet (in Utils) to keep track of what
     *     items are on top (I think that was fastest).  When an
     *     item is added to DeepStorage, make sure the correct
     *     item is in the HashSet.  When an item gets removed,
     *     take it out of the HashSet and make sure the correct
     *     item IS in there.
     *     Patch Thing's DrawPos getter, so that when an item
     *     is on "top" of the DeepStorage pile, its draw height
     *     (altitude, "y", whatever) is sliiiightly higher than
     *     normal.  Viola!  It gets drawn on top.
     * 3.  Do a pretty - and useful - GUI overlay on DSUs, as
     *     appropriate.
     *     (on adding to DSU, turn off normal GUI overlay;
     *     make a gui overlay for the DSU)
     *
     * Note:  The way the game handles storage, we don't have to
     *     re-register items to be drawn, turn back on their GUI,
     *     or any of that.
     *     Why?
     *     The game *unspawns the old item* and then moves it to
     *     the pawn's inventory.  When the pawn puts it down?
     *     The item re-registers to be drawn on spawn.
     * Note2: When an item is added to DS, we put the DSU at the
     *     end of the thingsListAt.
     *     Vanilla behavior, pre-save:
     *       stuff-in-storage (at end of list)
     *       Shelf
     *     Vanilla behavior, post-save:
     *       Shelf (at end of list)
     *       stuff-in-storage
     *     It is better to ensure a DSU is at the end of the
     *     list than risk DSU being in the middle of the list
     *     after a save and reload.
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
            var lookingForThisFieldCall = HarmonyLib.AccessTools.Field(typeof(Verse.Map), "thingGrid");
            for (;i<code.Count;i++) {
                if (code[i].opcode != OpCodes.Ldfld || 
                    (System.Reflection.FieldInfo)code[i].operand != lookingForThisFieldCall) {
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
                yield return new CodeInstruction(OpCodes.Call, HarmonyLib.AccessTools.Method(
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
     */

    /* NOTE: an item can be added to Deep Storage in two ways:
     *  2.  Something puts it there (Notify_ReceivedThing called)
     *  1.  The game loads.
     *  Both need to be addressed.
     */
    
    // Make non-mesh things invisible when loaded in Deep Storage
    // Making item on top display on top: loaded items on "top" need to go into the HashSet
    // Gui Overlay: loaded items' overlays should not display
    // Put DeepStorage at the end of the ThingsList for proper display post-save
    [HarmonyPatch(typeof(Building_Storage), "SpawnSetup")]
    public static class PatchDisplay_SpawnSetup {
        public static void Postfix(Building_Storage __instance, Map map) {
            CompDeepStorage cds;
            if ((cds = __instance.GetComp<CompDeepStorage>()) == null) return;
            
            foreach (IntVec3 cell in __instance.AllSlotCells()) {
                List<Thing> list = map.thingGrid.ThingsListAt(cell);
                bool alreadyFoundItemOnTop=false;
                for (int i=list.Count-1; i>=0; i--) {
                    Thing thing=list[i];
                    if (!thing.Spawned || !thing.def.EverStorable(false)) continue; // don't make people walking past be invisible...

                    if (cds.cdsProps.overlayType != GuiOverlayType.Normal || !cds.showContents) {
                        // Remove gui overlay - this includes number of stackabe item, quality, etc
                        map.listerThings.ThingsInGroup(ThingRequestGroup.HasGUIOverlay).Remove(thing);
                    }
                    if (thing.def.drawerType != DrawerType.MapMeshOnly) {
                        if (!alreadyFoundItemOnTop) {
                            Utils.TopThingInDeepStorage.Add(thing);
                        }
                        if (!cds.showContents) {
                            map.dynamicDrawManager.DeRegisterDrawable(thing);
                        }
                    }
                    alreadyFoundItemOnTop=true;  // it's true now, one way or another!

                    if (!cds.showContents) {
                        map.tooltipGiverList.Notify_ThingDespawned(thing); // should this go with guioverlays?
                    }
                    // Don't need to thing.DirtyMapMesh(map); because of course it's dirty on spawn setup ;p
                } // end cell
                // Now put the DSU at the top of the ThingsList here:
                list.Remove(__instance);
                list.Add(__instance);
            }
        }
    }
    
    // Make non-mesh things invisible: they have to be de-registered on being added to a DSU
    // Making item on top display on top: added items need to go into the HashSet
    // Gui Overlay: added items' overlays should not display
    // Put DeepStorage at the end of the ThingsList for consistant display:
    [HarmonyPatch(typeof(Building_Storage),"Notify_ReceivedThing")]
    public static class PatchDisplay_Notify_ReceivedThing {
        public static void Postfix(Building_Storage __instance,Thing newItem) {
            CompDeepStorage cds;
            if ((cds = __instance.TryGetComp<CompDeepStorage>()) == null) return;

            /****************** Put DSU at top of list *******************/
            /*  See note 2 at top of file re: display                    */
            List<Thing> list = newItem.Map.thingGrid.ThingsListAt(newItem.Position);
            list.Remove(__instance);
            list.Add(__instance);

            /****************** Set display for items correctly *******************/
            /*** Clean up old "what was on top" ***/
            foreach (Thing t in list) {
                Utils.TopThingInDeepStorage.Remove(t);
            }
            
            /*** Complex meshes have a few rules for DeepStorage ***/
            if (newItem.def.drawerType != DrawerType.MapMeshOnly) {
                //  If they are on top, they should be drawn on top:
                if (cds.showContents)
                    Utils.TopThingInDeepStorage.Add(newItem);
                else // If we are not showing contents, don't draw them:
                    __instance.Map.dynamicDrawManager.DeRegisterDrawable(newItem);
            }

            /*** Gui overlay - remove if the DSU draws it, or if the item is invisible ***/
            if (cds.cdsProps.overlayType != GuiOverlayType.Normal || !cds.showContents) {
                // Remove gui overlay - this includes number of stackabe item, quality, etc
                __instance.Map.listerThings.ThingsInGroup(ThingRequestGroup.HasGUIOverlay).Remove(newItem);
            }

            if (!cds.showContents) return; // anything after is for invisible items

            /*** tool tip, dirt mesh, etc ***/
            __instance.Map.tooltipGiverList.Notify_ThingDespawned(newItem); // should this go with guioverlays?
            newItem.DirtyMapMesh(newItem.Map); // for items with a map mesh; probably unnecessary?
            // Note: not removing linker here b/c I don't think it's applicable to DS?
        }
    }

    /*** Removing an item from DeepStorage necessitates re-calculating which item is "on top" ***/
    
    // Son. Of. A. Biscuit.  This does not work:
    //   Notify_LostThing is an empty declaration, and it seems to be optimized out of existance,
    //   so Harmony cannot attach to it.  The game crashes - with no warning - when the patched
    //   method gets called.
    #if false
    [HarmonyPatch(typeof(RimWorld.Building_Storage), "Notify_LostThing")]
    public static class PatchDisplay_Notify_LostThing {
        static void Postfix(){
            return; // crashes instantly when Notify_LostThing is called ;_;
        }

        static void Postfix_I_Would_Like_To_Use(Building_Storage __instance, Thing newItem) {
            Utils.TopThingInDeepStorage.Remove(newItem);
            if (__instance.TryGetComp<CompDeepStorage>() == null) return;
            List<Thing> list = newItem.Map.thingGrid.ThingsListAt(newItem.Position);
            for (int i=list.Count-1; i>0; i--) {
                if (!list[i].def.EverStorable(false)) continue;
                Utils.TopThingInDeepStorage.Add(list[i]);
                return;
            }
        }
    }
    #endif

    /* Item Removed from Deep Storage: reprise: */
    /* We have to make a general check in DeSpawn to see if it was in DeepStorage before it disappears 
     * If so, make sure display is correct */
    [HarmonyPatch(typeof(Verse.Thing), "DeSpawn")]
    static class Cleanup_For_DeepStorage_Thing_At_DeSpawn {
        static void Prefix(Thing __instance) {
            // I wish I could just do this:
            // Utils.TopThingInDeepStorage.Remove(__instance);
            // But, because I cannot patch Notify_LostThing, I have to do its work here:  >:/
            if (!Utils.TopThingInDeepStorage.Remove(__instance)) return;
            if (__instance.Position == IntVec3.Invalid) return; // ???
            // So it was at one point in Deep Storage.  Is it still?
            CompDeepStorage cds;
            if ((cds=((__instance.Position.GetSlotGroup(__instance.Map)?.parent) as ThingWithComps)?.
                 TryGetComp<CompDeepStorage>())==null) return;
            // Figure out what is on top now:
            if (!cds.showContents) return;
            List<Thing> list = __instance.Map.thingGrid.ThingsListAtFast(__instance.Position);
            for (int i=list.Count-1; i>=0; i--) {
                if (!list[i].def.EverStorable(false)) continue;
                if (list[i] == __instance) continue;
                Utils.TopThingInDeepStorage.Add(list[i]);
                return;
            }
        }
    }

    /*************** Deep Storage DeSpawns (destroyed, minified, etc) *****************/
    [HarmonyPatch(typeof(Verse.Building), "DeSpawn")]
    public static class Patch_Building_DeSpawn_For_Building_Storage {
        [HarmonyPriority(Priority.First)] // MUST execute, cannot be postfix,
                                          // as some elements already null (e.g., map)
        public static void Prefix(Building __instance) {
            CompDeepStorage cds;
            if ((cds = __instance.GetComp<CompDeepStorage>())==null) return;
            Map map=__instance.Map;
            if (map == null) {
                Log.Error("DeepStorage despawning: Map is null; some assets may not display properly: "
                          +__instance.ToString()); return;
            }
            ISlotGroupParent DSU = __instance as Building_Storage;
            foreach (IntVec3 cell in DSU.AllSlotCells()) {
                List<Thing> list = map.thingGrid.ThingsListAt(cell);
                Thing t;
                for (int i=0; i<list.Count;i++) {
                    t=list[i];
                    Utils.TopThingInDeepStorage.Remove(t); // just take them all, to be safe
                    if (t==null) { Log.Warning("DeepStorage despawning: tried to clean up null object"); continue;}
                    if (!t.Spawned || !t.def.EverStorable(false)) continue;
                    if (t.def.drawerType != DrawerType.MapMeshOnly)
                    {   // should be safe to register even if already registered
                        map.dynamicDrawManager.RegisterDrawable(t);
                    }
                    // from the ListerThings code:
                    if (ThingRequestGroup.HasGUIOverlay.Includes(t.def)) {
                        if (!map.listerThings.ThingsInGroup(ThingRequestGroup.HasGUIOverlay).Contains(t)) {
                            map.listerThings.ThingsInGroup(ThingRequestGroup.HasGUIOverlay).Add(t);
                        }
                    }
                    // Just to make sure it's not in the tooltip list twice:
                    //    Is this ineffecient?  Yes.
                    //    It also means that if anything changes whether cds.showContents, nothing breaks
                    //    Also, this only happens rarely, so ineffecient is okay.
                    map.tooltipGiverList.Notify_ThingDespawned(t);
                    map.tooltipGiverList.Notify_ThingSpawned(t);
                } // end list of things at                
            } // end foreach cell of despawning DSU
        }  //end postfix
    } // end patch for when DSU despawns

    /* The workhouse solving #2 (from top of file)
     * The magic to make what-is-on-top get displayed "above" everything else: */
    /* (thank you DuckDuckGo for providing this approach, and thak you to everyone
     *  who helped people who had similar which-mesh-is-on-top problems)
     */
    [HarmonyPatch(typeof(Verse.Thing),"get_DrawPos")]
    static class Ensure_Top_Item_In_DSU_Draws_Correctly {
        static void Postfix(Thing __instance, ref Vector3 __result) {
            if (Utils.TopThingInDeepStorage.Contains(__instance)) {
                __result.y+=0.05f; // The default "altitudes" are around .45 apart, so .05 should be about right.
                                   //             "altitudes" here are "terrain," "buildings," etc.
            }
        }
    }

    
    /**************** GUI Overlay *****************/
    [HarmonyPatch(typeof(Thing),"DrawGUIOverlay")]
    static class Add_DSU_GUI_Overlay {
        static bool Prefix(Thing __instance) {
            if (Find.CameraDriver.CurrentZoom != CameraZoomRange.Closest) return true; // maybe someone changes this? Who knows.
            Building_Storage DSU = __instance as Building_Storage;
            if (DSU == null) return true;
            CompDeepStorage cds = DSU.GetComp<CompDeepStorage>();
            if (cds == null) return true;
            if (cds.cdsProps.overlayType == LWM.DeepStorage.GuiOverlayType.Normal) return true;
            if (cds.cdsProps.overlayType == GuiOverlayType.None) return false;

            List<Thing> things;
            String s;
            if (cds.cdsProps.overlayType == GuiOverlayType.CountOfAllStacks) {
                // maybe Armor Racks, Clothing Racks, def Weapon Lockers etc...
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
                    var l2=GenMapUI.LabelDrawPosFor(c);
//                    l2.x+=cds.x;
//                    l2.y+=cds.y;
                    l2.y+=10f;
                    GenMapUI.DrawThingLabel(l2,s,GenMapUI.DefaultThingLabelColor);
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
            if (cds.cdsProps.overlayType == GuiOverlayType.SumOfItemsPerCell) {
                // Big Shelves
                bool anyItems=false;
                foreach (IntVec3 c in DSU.AllSlotCellsList()) {
                    bool itemsWithStackSizeOne=false;
                    things=__instance.Map.thingGrid.ThingsListAtFast(c).FindAll(t=>t.def.EverStorable(false));
                    if (things.Count > 0) {
                        anyItems=true;
                        int count=0;
                        for (int i=0; i<things.Count; i++) {
                            // Break logic if there is anything with a stackLimit of 1
                            //   show instead the count of stacks:
                            if (itemsWithStackSizeOne || things[i].def.stackLimit==1) {
                                itemsWithStackSizeOne=true;
                                if (things.Count ==1)
                                    s=1.ToStringCached(); // ..a language that doesn't use arabic numerals?
                                else if (AllSameType(things))
                                    s="x"+things.Count.ToStringCached();
                                else
                                    s="[ "+things.Count.ToStringCached()+" ]";
                                var l=GenMapUI.LabelDrawPosFor(c);
                                l.y+=10f;
                                GenMapUI.DrawThingLabel(l,s,GenMapUI.DefaultThingLabelColor);
                                goto WhyDoesCSharpNotHaveBreakTwo;
                            } else {
                                count+=things[i].stackCount;
                            }
                        } // end list of things.
                        if (AllSameType(things))
                            s=count.ToStringCached();
                        else
                            s="[ "+count.ToStringCached()+" ]";
                        var l2=GenMapUI.LabelDrawPosFor(c);
                        l2.y+=10f;
                        GenMapUI.DrawThingLabel(l2,s,GenMapUI.DefaultThingLabelColor);
                    } // if count > 0
                    WhyDoesCSharpNotHaveBreakTwo:;
                } // foreach cell
                if (!anyItems && !cds.cdsProps.showContents) { // there are no items, but no way to see that.
                    s="LWM_DS_Empty".Translate();
                    GenMapUI.DrawThingLabel(GenMapUI.LabelDrawPosFor(__instance,0f),s,GenMapUI.DefaultThingLabelColor);
                }
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
