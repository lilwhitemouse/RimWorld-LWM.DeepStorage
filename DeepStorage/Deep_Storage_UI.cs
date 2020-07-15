using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

// RT_Shelves:
using System.Linq;
using Verse.AI;


namespace LWM.DeepStorage
{
    /***************************************************
     * Select Deep Storage Unit
     *
     * It's a pain to click thru 10 items to get to the
     * Deep Storage Unit.
     * We patch Selector.cs's HandleMapClicks to allow
     * selecting the Deep Storage Unit by right clicking
     * after selecting an item in it.
     *
     */

    [HarmonyPatch(typeof(Selector), "HandleMapClicks")]
    class Patch_HandleMapClicks {
        static bool Prefix(Selector __instance, List<object> ___selected) {
            if (Event.current.type == EventType.MouseDown) {
                // Right mouse clicked and some item selected:
                if (Event.current.button == 1 && ___selected.Count == 1 && !(___selected[0] is Pawn)) {
                    Thing t = ___selected[0] as Thing;
                    if (t==null) {
                        return true; // Don't know what it was...
                    }
                    if (t.Map == null) {
                        return true; // Don't know where it is...
                    }
                    if (t.Position == IntVec3.Invalid) {
                        return true; // Don't know how it got selected, either :p
                    }
                    if (t.Map != Find.CurrentMap) {
                        return true; // Don't know where the player is looking
                    }
                    // TODO: make this cleaner:
                    if (Utils.GetDeepStorageOnCell(t.Position, t.Map, out CompDeepStorage comp))
                    {
                        __instance.ClearSelection();
                        // Select the Deep Storage Unit:
                        __instance.Select(comp.parent);
                        Event.current.Use();
                        return false;
                    }
                }
            }
            return true; // not us
        }
    } // end HandleMapClick's patch


    /************************* Let user click on DSU instead of giant pile of stacks! ******************/
    /* We would like it so when a player clicks on a DSU that has stuff in it,
     * the DSU gets selected instead of the first item, then the 2nd item, etc.
     *
     * The reason the items get selected first is that ThingsUnderMouse sorts
     * usng CompareThingsByDrawAltitude - and buildings are below items.
     * So, we add a call to SortForDeepStorage.
     *
     * However, we only want to use the SortForDeepStorage if we are selecting
     * a single object!  If we are selecting all Wheat on the screen, (double click)
     * we almost certainly want the default behavior.
     *
     * So we control whether we sort by adding a flag to SelectUnderMouse.
     *
     * Basically, we make ThingsUnderMouse() into ThingsUnderMouse(sortType),
     * and make SelectUnderMouse() call it with SortForSingleClick (or whatever I named it).
     */
    // Add new sort inside ThingsUnderMouse
    // After the list is sorted via CompareThingsByDrawAltitude, we insert code to sort the list
    //   in our new function SortForDeepStorage.
    //   SortForDeepStorage will use a flag that was set before ThingsUnderMouse was called.
    [HarmonyPatch(typeof(Verse.GenUI),"ThingsUnderMouse")]
    public static class Patch_GenUI_ThingsUnderMouse {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            // First marker we are looking for is
            //   ldftn int32 Verse.GenUI::CompareThingsByDrawAltitude(class Verse.Thing, class Verse.Thing)
            var wrongComparison = HarmonyLib.AccessTools.Method("Verse.GenUI:CompareThingsByDrawAltitude");
            if (wrongComparison == null) {
                Log.Error("LWM: Deep Storage: harmony transpiler fail: no CompareThingsByDrawAltitude");
            }
            // Second marker we are looking for is
            //   callvirt instance void class
            //     [mscorlib]System.Collections.Generic.List`1<class Verse.Thing>::Sort(class [mscorlib]System.Comparison`1<!0>)
            var sortFunction = typeof(System.Collections.Generic.List<Thing>)
                               .GetMethod("Sort", new Type[] {typeof(System.Comparison<Thing>)});

            var code = new List<CodeInstruction>(instructions);
            var i = 0; // using multiple 'for' loops
            bool foundMarkerOne=false;
            for (; i < code.Count; i++) {
                yield return code[i];
                if (code[i].opcode == OpCodes.Ldftn && (MethodInfo)code[i].operand == wrongComparison) {
                    foundMarkerOne=true;
                }
                if (foundMarkerOne && code[i].opcode == OpCodes.Callvirt && (MethodInfo)code[i].operand == sortFunction) {
                    // We insert our own sorting function here, to put DSUs on top of click order:
                    //yield return new CodeInstruction(OpCodes.Ldloc_S,6); // the temporary list
                    yield return new CodeInstruction(OpCodes.Ldloc_2); // the temporary list for 1.1
                    yield return new CodeInstruction(OpCodes.Call, HarmonyLib.AccessTools.
                                                     Method("LWM.DeepStorage.Patch_GenUI_ThingsUnderMouse:SortForDeepStorage"));
                    i++; // VERY VERY important -.^
                    break; // our work is done here
                }
            }
            for (; i < code.Count; i++) { // finish up
                yield return code[i];
            }
        }

        public enum DSSort : byte {
            Vanilla,
            SingleSelect,
            MultiSelect,
        }

        /* A flag to get passed to GenUI.ThingsUnderMouse() - make sure to set it *and unset it back to Vanilla* manually */
        /*   (because it's not a real parameter - that's more trouble than I want) */
        static public DSSort sortForDeepStorage=DSSort.Vanilla;

        // Put DeepStorage at the top of the list:
        static public void SortForDeepStorage(List<Thing> list) {
            if (sortForDeepStorage==DSSort.Vanilla) return;
            if (sortForDeepStorage==DSSort.SingleSelect) {
                /* Single Select: for RimWorld.Selector's SelectUnderMouse() -
                 *   which selects a single item.
                 * We want any DSU to be on the top of the list so it gets
                 *   selected first!
                 */
                if (list.Count <2) return; // too few to care
                for (int i=list.Count-1; i>0; i--) { // don't need to check i=0; if it's a DSU, we're already good
                    if (list[i].TryGetComp<CompDeepStorage>()!=null) {
                        Thing t=list[i];
                        list.RemoveAt(i);
                        list.Insert(0,t);
                        return; // That's all we needed!
                    }
                }
                return;
            }
            if (sortForDeepStorage==DSSort.MultiSelect) {
                /* Multi Select: for RimWorld.Selector's SelectAllMatchingObjectUnderMouseOnScreen() -
                 *   which happens when a user double clicks and selects all matching items on the
                 *   screen.
                 * The behavior we want: whatever is on "top" - last added, whatever is displayed
                 *   on screen - should be what gets multi-selected.
                 * Problem: ThingsUnderMouse sorts by Altitude - and that does not preserve
                 *   the sort order the ThingList uses.
                 * So we will pull whatever item is last in the ThingList and put it at the top
                 *   of the selectable list.
                 * Kashmar.
                 */
                if (list.Count <2) return; // too few to care
                CompDeepStorage cds;
                for (int i=list.Count-1; i >=0; i--) {
                    // might as well count down, DSUs should be at the end?
                    if ((cds=list[i].TryGetComp<CompDeepStorage>())!=null) {
                        // Okay, now we have to make the sorting happen.
                        // Find the location cell we are using:
                        IntVec3 cell = IntVec3.Invalid;
                        // use the location of an item that is in storage:
                        for (int j=0; j<list.Count; j++) {
                            if (list[j].def.EverStorable(false)) {
                                cell=list[j].Position;
                                break;
                            }
                        }
                        if (cell == IntVec3.Invalid) {
                            // There are no storable objects here, so
                            //   go with default behavior
                            return;
                        }
                        List<Thing> thingsList=Find.CurrentMap.thingGrid.ThingsListAt(cell);
                        for (int k=thingsList.Count-1; k>=0; k--) {
                            if (thingsList[k].def.EverStorable(false)) {
                                if (list.Remove(thingsList[k])) { // found item from ThingsList in OUR list!
                                    list.Insert(0,thingsList[k]);
                                    return; // Ha - sorted!
                                }
                                // That item wasn't in the list for some reason, continue...
                            }
                        }
                        return; // Found DSU, but no objects to make double-clickable?
                    }
                }
                return; // not in Deep Storage
            }
        } // end SortForDeepStorage
    } // done with Patch_GenUI_ThingsUnderMouse

    // Single click should select the Deep Storage unit
    [HarmonyPatch(typeof(RimWorld.Selector), "SelectUnderMouse")]
    static class Make_Select_Under_Mouse_Use_SortForDeepStorage {
        static void Prefix() {
            Patch_GenUI_ThingsUnderMouse.sortForDeepStorage = Patch_GenUI_ThingsUnderMouse.DSSort.SingleSelect;
        }
        static void Postfix() {
            Patch_GenUI_ThingsUnderMouse.sortForDeepStorage = Patch_GenUI_ThingsUnderMouse.DSSort.Vanilla;
        }
    }
    // Double click should multi-select all of whatever item is on top (similar to how items on shelves behave)
    [HarmonyPatch(typeof(RimWorld.Selector),"SelectAllMatchingObjectUnderMouseOnScreen")]
    static class Make_DoubleClick_Work {
        static void Prefix(Selector __instance) {
            // If the DSU is still selected from the first click of SelectUnderMouse(),
            //   it will get included in the SelectAll...  So we clear the selection - this should be fine in general?
            //   It may affect some weird use cases, but if that ever turns into a problem, I can fix this.
            __instance.ClearSelection();
            Patch_GenUI_ThingsUnderMouse.sortForDeepStorage = Patch_GenUI_ThingsUnderMouse.DSSort.MultiSelect;
        }
        static void Postfix() {
            Patch_GenUI_ThingsUnderMouse.sortForDeepStorage = Patch_GenUI_ThingsUnderMouse.DSSort.Vanilla;
        }
    }

    // If there are 10 artifacts in a weapons locker, it's nice to be able to tell which one you are about to activate:
    // Add "  (Label for Artifact)" to the right-click label.
    [HarmonyPatch(typeof(RimWorld.CompUsable), "FloatMenuOptionLabel")]
    static public class MakeArtifactsActivateLabelNameArtifact {
        static void Postfix(ref string __result, CompUsable __instance) {
            __result=__result+" ("+__instance.parent.LabelCap+")";
        }

    }
}
