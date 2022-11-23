using System;
using RimWorld;
using Verse;
using UnityEngine;

namespace LWM.DeepStorage
{
    // ripped shamelessly from Dialog_RenameZone
    public class Dialog_CompSettingsMaybe : Window
    {
        public Dialog_CompSettingsMaybe(CompDeepStorage cds)
        {
            this.forcePause = true;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            this.closeOnAccept = true;
            this.closeOnClickedOutside = true;
            this.cds = cds;
        }

/*        public override Vector2 InitialSize
        {
            get {
                var o = base.InitialSize;
                o.y += 50f;
                return o;
            }
        }*/

        public override void DoWindowContents(Rect inRect)
        {
            Rect frame = new Rect(10f, 10f, inRect.x - 10, inRect.y - 10);
            GUI.BeginGroup(frame);
            GUI.color = Color.white;
            Text.Font = GameFont.Medium;
            float curY = 0f;
            Widgets.Label(0f, ref curY, frame.width, "LWMDSBuildingSettings".Translate(), 
                          "LWMDSBuildingSettingsDesc".Translate());
            Text.Font = GameFont.Small;
            Widgets.DrawLineHorizontal(0f, curY + 6f, frame.width - 10f);
            curY += 12f;

/*
            if (Widgets.ButtonText(new Rect(15f, inRect.height - 35f - 15f - 50f, inRect.width - 15f - 15f, 35f), "ResetButton".Translate(),
                                   true, false, true))
            {
                this.SetName("");
                Find.WindowStack.TryRemove(this, true);
            }
            GUI.EndGroup();
            //TODO: this should get stored at top and set here.
            GUI.color = Color.white;
            //TODO: this should get stored at top and set here.
            // it should get set to whatever draw-row uses at top
            Text.Anchor = TextAnchor.UpperLeft;*/
        }
        // TODO:  Make a nice button, eh?
        // override InitialSize to make it bigger
        // override DoWindowContents to add a new button on top of "Okay" that says "reset"?

        private CompDeepStorage cds;
    }
}
