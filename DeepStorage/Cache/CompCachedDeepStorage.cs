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
        private bool _isCachedComp = false;

        private StorageSettings _storageSetting;

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

        public CompCachedDeepStorage()
        {
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
            base.PostExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
                _isCachedComp = true;

            Scribe_Values.Look(ref _isCachedComp, nameof(_isCachedComp));

            if (Scribe.mode == LoadSaveMode.LoadingVars && !_isCachedComp)
            {
                int index = this.parent.AllComps.IndexOf(this);
                this.parent.AllComps[index] = new CompDeepStorage(this);
            }
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
                this.CellStorages = new Cell_Storage_Collection(this.parent as Building_Storage, this);
            }
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

        #endregion
    }
}
