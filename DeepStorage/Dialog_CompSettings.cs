using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
        public override void PreOpen()
        {
            base.PreOpen();
            this.tmpMaxNumStacks = cds.MaxNumberStacks;
            this.tmpLabel = cds.buildingLabel;
        }

        public override void DoWindowContents(Rect inRect) {
            // Take "Enter" press and close window with it:
            bool pressedEnterForOkay = false;
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                pressedEnterForOkay = true;
                Event.current.Use();
            }
            Rect frame = inRect.ContractedBy(10f);
            GUI.BeginGroup(frame);
            GUI.color = Color.white;
            Text.Font = GameFont.Medium;
            float curY = 0f;
            Widgets.Label(0f, ref curY, frame.width, "LWMDS_BuildingSettings".Translate());
                          //"LWMDSBuildingSettingsDesc".Translate());
            Text.Font = GameFont.Small;
            Widgets.DrawLineHorizontal(0f, curY + 6f, frame.width - 10f);
            curY += 15f;
            Text.Font = GameFont.Medium;
            Widgets.Label(0f, ref curY, frame.width, "LWMDS_PerBuildingSettings".Translate(cds.parent.def.label),
                          "LWMDS_PerBuildingSettingsDesc".Translate());
            Text.Font = GameFont.Small;
            Widgets.DrawLineHorizontal(0f, curY + 6f, frame.width - 10f);
            curY += 15f;
            //////////////////////////// Building settings ///////////////////////
            StorageGroup group = (cds.parent as IStorageGroupMember)?.Group;
            if (group != null)
            {
                Rect r = new Rect(0, curY, frame.width, 46f);
                Widgets.CheckboxLabeled(r, "LWMDS_ApplyChangesToGroup".Translate(),
                         ref applyChangesToGroup);
                if (Mouse.IsOver(r))
                {
                    TooltipHandler.TipRegion(r, "LWMDS_ApplyChangesToGroupDesc".Translate(group.MemberCount));
                }
                curY += 50f;
            }
            ////////// Rename //////////
            string newName = Widgets.TextEntryLabeled(new Rect(0f, curY, frame.width-50f, 23f),
                            "CommandRenameZoneLabel".Translate(), tmpLabel);
            if (newName.Length < this.MaxNameLength)
            {
                this.curName = newName;
            }
            // reset name button:
            if (Widgets.ButtonText(new Rect(frame.width -45f, curY, 45f, 23f), "ResetButton".Translate(),
                       true, false, true))
            {
                this.SetName(""); // any internal logic there
                foreach (var oc in CompsToApplyChangeTo(false))
                    oc.SetLabel(""); // but also get everything in group if need to
            }
            curY += 28;
            ///////// max number stacks ////////
            string tmpString = cds.MaxNumberStacks.ToString();
            int tmpInt = cds.MaxNumberStacks;
            Widgets.Label(0f, ref curY, frame.width, "LWMDS_CurrentMaxNumStacksTotals"
                                    .Translate(tmpInt, tmpInt * cds.parent.OccupiedRect().Cells.EnumerableCount()));
            Widgets.Label(0f, ref curY, frame.width, "LWMDS_DefaultMaxStacksTotals"
                                    .Translate(cds.CdsProps.maxNumberStacks, cds.CdsProps.maxNumberStacks *
                                                                cds.parent.OccupiedRect().Cells.EnumerableCount()));
            Widgets.TextFieldNumericLabeled<int>(new Rect(0, curY, frame.width, 46f), "LWM_DS_maxNumStacks".Translate(),
                                   ref tmpInt, ref tmpString, 0, 1024);
            curY += 50f;

            /////////////////////////// RESET & OK buttons ////////////////////////////
            if (Widgets.ButtonText(new Rect(15f, inRect.height - 35f - 15f, inRect.width - 15f - 15f, 35f), "OK", true, true, true, null) || pressedEnterForOkay)
            {
                if (tmpInt != cds.MaxNumberStacks)
                {
                    //(Important check for Multiplayer - don't spam sync requests)
                    cds.MaxNumberStacks = tmpInt;
                }

                AcceptanceReport acceptanceReport = this.NameIsValid(this.curName);
                if (!acceptanceReport.Accepted)
                {
                    if (acceptanceReport.Reason.NullOrEmpty())
                    {
                        Messages.Message("NameIsInvalid".Translate(), MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                    Messages.Message(acceptanceReport.Reason, MessageTypeDefOf.RejectInput, false);
                }
                else
                {
                    this.SetName(this.curName);
                    Find.WindowStack.TryRemove(this, true);
                }
            }
            else if (Widgets.ButtonText(new Rect(15f, inRect.height -35f -15f -50f, inRect.width-15f-15f, 35f), "ResetButton".Translate(),
                                   true,false,true)) {
                this.SetName("");
                cds.ResetSettings();
                Find.WindowStack.TryRemove(this, true);
            }
            GUI.EndGroup(); // very important for this to be called

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
            this.cds.SetLabel(name);
            Messages.Message("LWM_DSU_GainsName".Translate(this.cds.parent.def.label, cds.parent.Label),
                             MessageTypeDefOf.TaskCompletion, false);
        }

        IEnumerable<CompDeepStorage> CompsToApplyChangeTo(bool includeThisOne = true)
        {
            if (includeThisOne) yield return cds;
            if (applyChangesToGroup)
            {
                foreach (var otherCds in (cds.parent as IStorageGroupMember)?.Group?.members?.OfType<ThingWithComps>()
                                       .Select(x => x.GetComp<CompDeepStorage>()).Where(x => x != cds) 
                      ??  Enumerable.Empty<CompDeepStorage>())
                {
                    yield return otherCds;
                }
            }
        }

        // TODO:  Make a nice button, eh?
        // override InitialSize to make it bigger
        // override DoWindowContents to add a new button on top of "Okay" that says "reset"?

        private CompDeepStorage cds;
        private bool applyChangesToGroup = true;

        private string tmpLabel;
        private int tmpMaxNumStacks;
	}
}
