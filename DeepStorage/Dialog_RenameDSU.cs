using System;
using RimWorld;
using Verse;
using UnityEngine;

namespace LWM.DeepStorage
{
    // ripped shamelessly from Dialog_RenameZone
    public class Dialog_RenameDSU : Dialog_RenameBuildingStorage
    {
        public Dialog_RenameDSU(IRenameable cds): base(cds)
        {
            forcePause = true;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
        }

        // ... Actually, whatever, name it whatever you want.
        // But use "" to reset to default.
        protected override AcceptanceReport NameIsValid(string name)
        {
            if (name.Length == 0) return true;
            AcceptanceReport result = base.NameIsValid(name);
            if (!result.Accepted)
            {
                return result;
            }
            return true;
        }

        public override Vector2 InitialSize
        {
            get {
                var o = base.InitialSize;
                o.y += 50f;
                return o;
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            base.DoWindowContents(inRect);
            if (Widgets.ButtonText(new Rect(15f, inRect.height - 35f - 15f - 50f, inRect.width - 15f - 15f, 35f), "ResetButton".Translate(),
                                   true, false, true))
            {
                this.curName = null;
                this.OnRenamed(null);
                Find.WindowStack.TryRemove(this, true);
            }

        }
        
        protected override void OnRenamed (string name)
        {
            if (string.IsNullOrEmpty(name) && this.renaming != null)
            {
                this.renaming.RenamableLabel = null;
            }
        }
        // TODO:  Make a nice button, eh?
        // override InitialSize to make it bigger
        // override DoWindowContents to add a new button on top of "Okay" that says "reset"?
    }
}