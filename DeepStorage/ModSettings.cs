using System;
using Verse;
using UnityEngine;

namespace LWM.DeepStorage
{
    public class Settings : ModSettings {
        public static bool storingTakesTime=true;
        

        public static void DoSettingsWindowContents(Rect inRect) {
            Listing_Standard listing_Standard = new Listing_Standard();
            listing_Standard.Begin(inRect);
            
            listing_Standard.CheckboxLabeled("Does storing take time?", ref storingTakesTime, "Deselect this if you want all storing to happen instantly.  Not LWM approved, but if you want to, go for it.");

            listing_Standard.End();
        }

        public override void ExposeData() {
            base.ExposeData();

            Scribe_Values.Look(ref storingTakesTime, "storing_takes_time", true);

        }
    }


}
