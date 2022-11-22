using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Linq;
using UnityEngine;
using static LWM.DeepStorage.Utils.DBF; // trace utils

namespace LWM.DeepStorage
{
    public static class ITab_Inventory_HeaderUtil
    {
        static List<Thing> listOfStoredItems = new List<Thing>();
        static System.Text.StringBuilder headerStringB = new System.Text.StringBuilder();
        public static List<Thing> GetContentsHeader(this CompDeepStorage cds, out string header, out string tooltip)
        {
            listOfStoredItems.Clear();
            headerStringB.Length = 0;
            tooltip = null; // TODO: add more information via tooltip for DSUs with minNumStacks above 2

            bool flagUseStackInsteadOfItem = false; // "3/4 Items" vs "3/4 Stacks"
            int numCells = 0;
            float itemsTotalMass = 0; // or Bulk for CE ;p
            int cellsBelowMin = 0;
            int cellsAtAboveMin = 0;
            foreach (IntVec3 storageCell in (cds.parent as Building_Storage).AllSlotCells())
            {
                int countInThisCell = 0;
                numCells++;
                foreach (Thing t in cds.parent.Map.thingGrid.ThingsListAt(storageCell))
                {
                    if (t.Spawned && t.def.EverStorable(false))
                    {
                        listOfStoredItems.Add(t);
                        itemsTotalMass += t.GetStatValue(cds.stat, true) * (float)t.stackCount;
                        if (t.def.stackLimit > 1) flagUseStackInsteadOfItem = true;
                        countInThisCell++;
                    }
                }
                if (countInThisCell >= cds.MinNumberStacks) cellsAtAboveMin++;
                else cellsBelowMin++;
            }
            // We want to give user inforation about mass limits and how close we are, if they exist
            // TODO: Maybe use prop's kg() to translate everywhere, for better readability if using
            //       bulk.  Or maybe just leave it as is; CE will live.
            if (cds.limitingTotalFactorForCell > 0f)
            {
                // If minNumberStacks > 2, this really really complicates things.
                // For example, if one cell has 1 SUPER HEAVY item in it, and the other cell has 7 light items...
                // What do we say?  It's over the total mass limit....but each cell can get more things!
                if (cds.MinNumberStacks > 2)
                {
                    // Easy case: if each cell has at least minimum number of stacks:
                    // TODO: if min is 5 and there are 4 with below mass limit, also go here:
                    if (cellsAtAboveMin == numCells)
                    { //////////////// NO cells below minimum
                        // Simple header that includes mass:  12/20 stacks with total mass of 2.3/5 - as below
                        headerStringB.Append("LWM.ContentsHeaderMaxMass".Translate(listOfStoredItems.Count,
                                // 3 stacks or 3 items:
                                (flagUseStackInsteadOfItem ? "LWM.XStacks" : "LWM.XItems").Translate(cds.MaxNumberStacks * numCells),
                                cds.stat.ToString().ToLower(), itemsTotalMass.ToString("0.##"),
                                (cds.limitingTotalFactorForCell * numCells).ToString("0.##")));
                    }
                    else if (cellsBelowMin == numCells)
                    { ///////////// ALL cells below minimum
                        // 3/10 items, max 20, with total mass 0.45
                        headerStringB.Append("LWM.ContentsHeaderMinMax".Translate(listOfStoredItems.Count,
                                (flagUseStackInsteadOfItem ? "LWM.XStacks" : "LWM.XItems").Translate(cds.MinNumberStacks * numCells),
                                cds.MaxNumberStacks * numCells, cds.stat.ToString().ToLower(), itemsTotalMass.ToString("0.##")));
                    }
                    else
                    { ////////////////////////////////////////// SOME cells are below the minimum
                        if (flagUseStackInsteadOfItem) // 11 stacks, max 20, limited with total mass 8.2
                            headerStringB.Append("LWM.ContentsHeaderStacksMax".Translate(listOfStoredItems.Count,
                                cds.MaxNumberStacks * numCells, cds.stat.ToString().ToLower(), itemsTotalMass.ToString("0.##")));
                        else
                            headerStringB.Append("LWM.ContentsHeaderItemsMax".Translate(listOfStoredItems.Count,
                                cds.MaxNumberStacks * numCells, cds.stat.ToString().ToLower(), itemsTotalMass.ToString("0.##")));
                    }
                }
                else
                { // Simple header that includes mass:  4/8 stacks with total mass of 12/20
                    headerStringB.Append("LWM.ContentsHeaderMaxMass".Translate(listOfStoredItems.Count,
                            (flagUseStackInsteadOfItem ? "LWM.XStacks" : "LWM.XItems").Translate(cds.MaxNumberStacks * numCells),
                            cds.stat.ToString().ToLower(), itemsTotalMass.ToString("0.##"),
                            (cds.limitingTotalFactorForCell * numCells).ToString("0.##")));
                }
            }
            else
            { // No limiting mass factor per cell
                // 4/8 stacks with total mass of 12kg
                headerStringB.Append("LWM.ContentsHeaderMax".Translate(listOfStoredItems.Count,
                             // 3 stacks or 3 items:
                             (flagUseStackInsteadOfItem ? "LWM.XStacks" : "LWM.XItems").Translate(cds.MaxNumberStacks * numCells),
                             cds.stat.ToString().ToLower(), itemsTotalMass.ToString("0.##")));
            }
            ///////////////////////////// Max mass per item?
            if (cds.limitingFactorForItem > 0f)
            { // (Cannot store items above mass of X kg)
                headerStringB.Append('\n').Append("LWM.ContentsHeaderMaxSize".Translate(
                                      cds.stat.ToString().ToLower(),
                                      cds.limitingFactorForItem.ToString("0.##")));
            }
            AddPawnReservationsHeader((Building_Storage)cds.parent); // Probably best to not add this comp to anything else.
            header = headerStringB.ToString();
            return listOfStoredItems;
        }
        public static List<Thing> GenericContentsHeader(Building_Storage storage, out string header, out string tooltip)
        {
            headerStringB.Length = 0;
            listOfStoredItems.Clear();
            tooltip = null; // TODO: use this, eh?
            bool flagUseStackInsteadOfItem = false; // "3/4 Items" vs "3/4 Stacks"
            float itemsTotalMass = 0; // not Bulk here
            int numCells = 0;
            foreach (IntVec3 storageCell in storage.AllSlotCells())
            {
                foreach (Thing t in storage.Map.thingGrid.ThingsListAt(storageCell))
                {
                    if (t.Spawned && t.def.EverStorable(false))
                    {
                        listOfStoredItems.Add(t);
                        itemsTotalMass += t.GetStatValue(StatDefOf.Mass, true) * (float)t.stackCount;
                        if (t.def.stackLimit > 1) flagUseStackInsteadOfItem = true;
                    }
                }
                numCells++;
            }
            // 4/8 stacks with total mass of 12kg (as above)
            headerStringB.Append("LWM.ContentsHeaderMax"
                     .Translate(listOfStoredItems.Count,
                                // 3 stacks or 3 items:
                                (flagUseStackInsteadOfItem ? "LWM.XStacks" : "LWM.XItems").Translate(numCells),
                                StatDefOf.Mass, itemsTotalMass.ToString("0.##")));
            AddPawnReservationsHeader(storage); // affects headerStringB directly. Eh.
            header = headerStringB.ToString();
            return listOfStoredItems;
        }
        ///////////////////////////// Pawn reservations
        //             Displaying who is using the storage building has cut
        //             down on questions in the Steam thread. Can I get a wahoo?
        // (adds directly to headerStringB)
        static List<string> listOfReservingPawns = new List<string>();
        static void AddPawnReservationsHeader(Building_Storage storage)
        {
            List<Pawn> pwns = storage.Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
            if (pwns.Count > 0)
            {
                listOfReservingPawns.Clear();
                foreach (IntVec3 c in storage.AllSlotCells())
                {
                    Pawn p = storage.Map.reservationManager.FirstRespectedReserver(c, pwns[0]);
                    if (p != null)
                    {
                        // (p can possibly be animals)
                        listOfReservingPawns.Add(p.LabelShort);
                    }
                }
                if (listOfReservingPawns.Count > 0)
                {
                    headerStringB.Append('\n');
                    if (listOfReservingPawns.Count == 1)
                    {
                        headerStringB.Append("LWM.ContentsHeaderPawnUsing".Translate(listOfReservingPawns[0]));
                    }
                    else
                    {
                        headerStringB.Append("LWM.ContentsHeaderPawnsUsing".Translate(
                                          String.Join(", ", listOfReservingPawns.ToArray())));
                    }
                }
            }
        } // end checking pawn reservations

    }
}
