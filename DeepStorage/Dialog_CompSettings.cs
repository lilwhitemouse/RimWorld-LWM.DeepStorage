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
    // NOTE: Why bother with dialog_rename? I have it all here
    // TODO: change this
    // TODO: only show name message if name changed
    // TODO: show new message for maxNumStacks changing
	public class Dialog_CompSettings : Dialog_Rename<CompDeepStorage>
	{
		public Dialog_CompSettings(CompDeepStorage cds): base(cds)
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
            this.curMaxNumStacks = cds.MaxNumberStacks;
            this.curName = cds.buildingLabel;
        }

        public override void DoWindowContents(Rect inRect) {
            // Take "Enter" press and close window with it (as if pressed OK):
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
            // default button:
            if (Widgets.ButtonText(new Rect(frame.width - 95f, curY, 45f, 23f), "Default".Translate(),
                       true, false, true))
            {
                this.curName = "";
            }
            // reset name button:
            if (Widgets.ButtonText(new Rect(frame.width - 45f, curY, 45f, 23f), "ResetButton".Translate(),
                       true, false, true))
            {
                this.SetName(""); // any internal logic there
                foreach (var oc in CompsToApplyChangeTo(false))
                    oc.SetLabel(""); // but also get everything in group if need to
            }
            string newName = Widgets.TextEntryLabeled(new Rect(0f, curY, frame.width-100f, 23f),
                            "CommandRenameZoneLabel".Translate(), curName);
            if (newName.Length < this.MaxNameLength)
            {
                this.curName = newName;
            }
            curY += 28;
            ///////// max number stacks ////////
            Widgets.Label(0f, ref curY, frame.width, "LWMDS_CurrentMaxNumStacksTotals"
                                    .Translate(curMaxNumStacks, curMaxNumStacks * cds.parent.OccupiedRect().Cells.EnumerableCount()));
            Widgets.Label(0f, ref curY, frame.width, "LWMDS_DefaultMaxNumStacksTotals"
                                    .Translate(cds.CdsProps.maxNumberStacks, cds.CdsProps.maxNumberStacks *
                                                                cds.parent.OccupiedRect().Cells.EnumerableCount()));
            //// default button:
            if (Widgets.ButtonText(new Rect(frame.width - 95f, curY, 45f, 23f), "Default".Translate(),
                       true, false, true))
            {
                this.curMaxNumStacks = cds.CdsProps.maxNumberStacks;
            }
            //// reset button:
            if (Widgets.ButtonText(new Rect(frame.width - 45f, curY, 45f, 23f), "ResetButton".Translate(),
                       true, false, true))
            {
                curMaxNumStacks = cds.CdsProps.maxNumberStacks;
                foreach (var oc in CompsToApplyChangeTo(true))
                    oc.MaxNumberStacks = curMaxNumStacks;
            }
            //// text box to change:
            string tmpString = curMaxNumStacks.ToString();
            // Set min of 0 and max of 1024, because why not?
            Widgets.TextFieldNumericLabeled<int>(new Rect(0, curY, frame.width-100f, 46f), "LWM_DS_maxNumStacks".Translate(),
                                   ref curMaxNumStacks, ref tmpString, 0, 1024);
            curY += 50f;

            /////////////////////////// RESET & OK buttons ////////////////////////////
            // OK:
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
                }
                else
                {
                    foreach (var oc in CompsToApplyChangeTo())
                        oc.MaxNumberStacks = curMaxNumStacks;
                    this.SetName(this.curName);
                    Find.WindowStack.TryRemove(this, true);
                }
            } // and Reset:
            else if (Widgets.ButtonText(new Rect(15f, inRect.height -35f -15f -50f, inRect.width-15f-15f, 35f), "ResetButton".Translate(),
                                   true,false,true)) {
                this.SetName("");
                foreach (var oc in CompsToApplyChangeTo())
                    oc.ResetSettings();
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

        protected void SetName(string name)
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

        // Dialog_Rename has curName
        protected int curMaxNumStacks;
	}
}
