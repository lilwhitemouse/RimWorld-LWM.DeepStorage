using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LWM.DeepStorage
{
    class Deep_Storage_Building : Building_Storage, IRenameable
    {
        public Deep_Storage_Building() : base()
        {
        }

        public string buildingLabel;

        public string RenamableLabel
        {
            get => this.buildingLabel ?? this.BaseLabel;
            set
            {
                this.buildingLabel = value;
                SetLabelMultiplayer(buildingLabel);
            }
        }

        public string BaseLabel
        {
            get => this.Label;
        }

        public string InspectLabel
        {
            get => this.RenamableLabel;
        }
        
        [Multiplayer.API.SyncMethod] // I am informed that doing it this way is overkill
        private void SetLabelMultiplayer(string newLabel)
        {
            buildingLabel = newLabel;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            if (Scribe.mode != LoadSaveMode.LoadingVars || this.buildingLabel == null)
            {
                Scribe_Values.Look(ref this.buildingLabel, "LWM_DS_DSU_label");
            }
        }
    }
}