using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LWM.DeepStorage;
using RimWorld;
using UnityEngine;
using Verse;

using CellStorage = LWM.DeepStorage.Deep_Storage_Cell_Storage_Model;
using static LWM.DeepStorage.Utils.DBF;
using Math = System.Math; // trace utils

namespace LWM.DeepStorage
{
    public class CompCachedDeepStorage : CompDeepStorage
    {
        private StorageSettings _storageSetting;

        private const int _tickRateQuotient = GenTicks.TickLongInterval / GenTicks.TickRareInterval;

        public Building_Storage StorageBuilding { get; private set; }

        public StorageSettings StorageSettings
        {
            get
            {
                if (_storageSetting == null)
                    _storageSetting = this.StorageBuilding.settings;

                return _storageSetting;
            }
        }

        public Cell_Storage_Collection CellStorages { get; private set; }

        public CompCachedDeepStorage() {
            this.cached = true;
        }

        #region Overrides of CompDeepStorage

        /// <summary>
        /// Check if <paramref name="thing"/> can be placed at <paramref name="cell"/>
        /// </summary>
        /// <param name="thing"> Thing to check. </param>
        /// <param name="cell"> Target position. </param>
        /// <param name="map"> Map that holds <paramref name="thing"/>. </param>
        /// <returns> Returns <see langword="true"/> if there is room for <paramref name="thing"/> </returns>
        public override bool StackableAt(Thing thing, IntVec3 cell, Map map)
        {
            if (!this.CellStorages.TryGetCellStorage(cell, out CellStorage cellStorage))
                return false;

            return StackableAt(thing, map, cellStorage, thing.GetStatValue(StatDefOf.Mass));
        }

        public override bool CapacityAt(Thing thing, IntVec3 cell, Map map, out int capacity)
        {
            capacity = CapacityToStoreThingAt(thing, map, cell);
            return capacity > 0;
        }

        public override int CapacityToStoreThingAt(Thing thing, Map map, IntVec3 cell) {
            if (!this.CellStorages.TryGetCellStorage(cell, out CellStorage cellStorage))
                return 0;

            float unitWeight = thing.GetStatValue(StatDefOf.Mass);
            if (!CanStore(thing, map))
                return 0;

            int emptyStack = this.maxNumberStacks - cellStorage.Count;
            int spareSpaceOnNonFull = cellStorage.SpareSpaceOnNonFull(thing);
            int spareStacks = emptyStack * thing.def.stackLimit + spareSpaceOnNonFull;

            if (this.limitingTotalFactorForCell > 0)
            {
                int spareStacksByWeight = Mathf.FloorToInt((this.limitingTotalFactorForCell - cellStorage.TotalWeight) / unitWeight);
                int spareStacksByMinimum = thing.def.stackLimit * (this.minNumberStacks - cellStorage.Count);

                return Mathf.Max(Mathf.Min(spareStacks, spareStacksByWeight), spareStacksByMinimum, 0);
            }

            return spareStacks;
        }

        public override void PostExposeData() {
            if (Scribe.mode != LoadSaveMode.LoadingVars)
                base.PostExposeData();

            if (this.CellStorages == null)
                this.CellStorages = new Cell_Storage_Collection(this.parent as Building_Storage, this);

            this.CellStorages.ExposeData();
        }

        public override void Initialize(CompProperties props) {
            base.Initialize(props);

            this.StorageBuilding = (Building_Storage)this.parent;
        }

        #endregion

        private bool StackableAt(Thing thing, Map map, CellStorage cellStorage, float unitWeight) {

            if (!CanStore(thing, map))
                return false;

            return cellStorage.CanAccept(thing, unitWeight);
        }

        private bool CanStore(Thing thing, Map map) {
            if (map != this.StorageBuilding.Map)
                return false;

            if (!thing.def.EverStorable(false) || !this.StorageSettings.AllowedToAccept(thing))
                return false;

            // Jewelry box can't store a rocket launcher.
            if (this.limitingFactorForItem > 0f)
            {
                if (thing.GetStatValue(this.stat) > this.limitingFactorForItem)
                {
                    Utils.Warn(CheckCapacity, "  Cannot store because " + stat + " of "
                               + thing.GetStatValue(stat) + " > limit of " + limitingFactorForItem);
                    return false;
                }
            }

            return true;
        }

        #region Overrides of ThingComp

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                Log.Message($"Initialize cached DS unit");
                this.CellStorages = new Cell_Storage_Collection(this.parent as Building_Storage, this);
            }

            Utils.Mess(Utils.DBF.Cache, $"TickerType: {this.parent.def.tickerType}");
        }

        public override void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
            this.CellStorages.Clear();
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            this.CellStorages.Clear();
        }

        /// <summary>
        /// CompTickRare is only called when Remainder, which is equal to Find.TickManager.TicksGame % GenTicks.TicksRareInterval,
        /// equals to the index of list to which this comp is added. Given TicksRareInterval = 250 and TicksLongInterval = 2000,
        /// _tickRateQuotient = 2000 / 250 = 8. Whenever this method is called, TicksGame = 250 * Multiplier + Remainder(position in the tick list).
        /// Therefore, SelfCorrection will only be invoked when Multiplier is a multiple of _tickRateQuotient.
        /// </summary>
        public override void CompTickRare() {
            Utils.Mess(Utils.DBF.Cache, 
                $"Quotient: {Find.TickManager.TicksGame / GenTicks.TickRareInterval}, _tickQuotient: {_tickRateQuotient}");

            if (Find.TickManager.TicksGame / GenTicks.TickRareInterval % _tickRateQuotient != 0)
                return;

            Utils.Mess(Utils.DBF.Cache, $"Tick for {this.parent} at tick {Find.TickManager.TicksGame}");
            foreach (CellStorage cellStorage in this.CellStorages.Storages)
                cellStorage.SelfCorrection();
        }

        #endregion
    }
}
