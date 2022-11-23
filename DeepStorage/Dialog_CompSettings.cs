using System;
using RimWorld;
using Verse;
using UnityEngine;

namespace LWM.DeepStorage
{
	// ripped shamelessly from Dialog_RenameZone
	public class Dialog_CompSettings : Dialog_Rename
	{
		public Dialog_CompSettings(CompDeepStorage cds)
		{
			this.cds = cds;
			this.curName = cds.parent.Label;
		}

		public override Vector2 InitialSize
		{
			get
			{
                return new Vector2(500f, 500f);
                /*
                var o=base.InitialSize;
                o.y+=50f;
                return o;
                */               
			}
		}

        public override void DoWindowContents(Rect inRect) {
            bool pressedEnterForOkay = false;
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                pressedEnterForOkay = true;
                Event.current.Use();
            }

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
            string newName = Widgets.TextEntryLabeled(new Rect(0f, curY + 15f, frame.width-50f, 35f),
                            "CommandRenameZoneLabel".Translate(), cds.buildingLabel);
            if (newName.Length < this.MaxNameLength)
            {
                this.curName = newName;
            }
            // reset name button:
            if (Widgets.ButtonText(new Rect(frame.width -45f, curY, 45f, 35f), "ResetButton".Translate(),
                       true, false, true))
            {
                this.SetName("");
            }
            curY += 37;
            Text.Font = GameFont.Medium;
            Widgets.Label(0f, ref curY, frame.width, "LWMDS_PerBuildingSettings".Translate(),
                          "LWMDS_PerBuildingSettingsDesc".Translate());

            Text.Font = GameFont.Small;
            string tmpString = cds.MaxNumberStacks.ToString();
            int tmpInt = cds.MaxNumberStacks;
            Widgets.TextFieldNumericLabeled<int>(new Rect(0, curY, frame.width, 15f), "LWM_DS_maxNumStacks".Translate(),
                                   ref tmpInt, ref tmpString, 0, 1024);
            if (tmpInt != cds.MaxNumberStacks)
            {
                //TODO Multiplayer:
                cds.MaxNumberStacks = tmpInt;
            }



            if (Widgets.ButtonText(new Rect(15f, inRect.height - 35f - 15f, inRect.width - 15f - 15f, 35f), "OK", true, true, true, null) || pressedEnterForOkay)
            {
                AcceptanceReport acceptanceReport = this.NameIsValid(this.curName);
                if (!acceptanceReport.Accepted)
                {
                    if (acceptanceReport.Reason.NullOrEmpty())
                    {
                        Messages.Message("NameIsInvalid".Translate(), MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                    Messages.Message(acceptanceReport.Reason, MessageTypeDefOf.RejectInput, false);
                    return;
                }
                else
                {
                    this.SetName(this.curName);
                    Find.WindowStack.TryRemove(this, true);
                }
            }
            if (Widgets.ButtonText(new Rect(15f, inRect.height -35f -15f -50f, inRect.width-15f-15f, 35f), "ResetButton".Translate(),
                                   true,false,true)) {
                this.SetName("");
                cds.ResetSettings();
                Find.WindowStack.TryRemove(this, true);
            }

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

        protected override void SetName(string name)
        {
            this.cds.buildingLabel = name;
            Messages.Message("LWM_DSU_GainsName".Translate(this.cds.parent.def.label, cds.parent.Label),
                             MessageTypeDefOf.TaskCompletion, false);
        }

        // TODO:  Make a nice button, eh?
        // override InitialSize to make it bigger
        // override DoWindowContents to add a new button on top of "Okay" that says "reset"?

        private CompDeepStorage cds;
	}
}
