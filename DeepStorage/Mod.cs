using System;
using Verse;
using UnityEngine;

namespace LWM.DeepStorage
{
    public class DeepStorageMod : Mod {
        Settings settings;

        public DeepStorageMod(ModContentPack content) : base(content) {
            this.settings=GetSettings<Settings>();
            
        }

        public override string SettingsCategory() => "LWM's Deep Storage"; // todo: translate?

        public override void DoSettingsWindowContents(Rect inRect) {
            Settings.DoSettingsWindowContents(inRect);
        }
        
    }

}
