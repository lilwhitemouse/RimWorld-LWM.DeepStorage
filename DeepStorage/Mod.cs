using System;
using Verse;
using UnityEngine;
using Multiplayer.API;

namespace LWM.DeepStorage
{
    public class DeepStorageMod : Mod {
        Settings settings;

        public DeepStorageMod(ModContentPack content) : base(content) {
            this.settings=GetSettings<Settings>();
            
            if (MP.enabled) {
                MP.RegisterAll();
            }
        }

        public override string SettingsCategory() => "LWM's Deep Storage"; // todo: translate?

        public override void DoSettingsWindowContents(Rect inRect) {
            Settings.DoSettingsWindowContents(inRect);
        }
        
    }

}
