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
    // TODO: Why bother with dialog_rename? I have it all here; just remove other dialog
    // TODO: show new message for maxNumStacks changing
    // NOTE: Any changes that happen can be directed to the Comp - it'll handle any weird storage group things
	public class Dialog_CompSettings : Dialog_Rename
	{
        public Dialog_CompSettings(ThingWithComps parent)
        //public Dialog_CompSettings(CompDeepStorage cds, Thing parent = null)
        {
            this.cds = DSStorageGroupUtility.GetOrTryMakeCompFrom(parent);
            this.parent = parent;
            if (cds == null) return;
            this.curName = cds.buildingLabel;
            if (curName == "") curName = DSStorageGroupUtility.GetDefaultLabelFor(parent); // same as below
            origName = curName;
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
            if (cds == null) return;
            this.curMaxNumStacks = cds.MaxNumberStacks;
            this.curName = cds.buildingLabel;
            if (curName == "") curName = DSStorageGroupUtility.GetDefaultLabelFor(parent); // same as below
            origName = curName;
        }

        public override void DoWindowContents(Rect inRect) {
            if (cds == null)
            {
                Log.Error("CompDeepStorage is null - this should never happen");
                return; // TODO: make this say some error message? Maybe?
            }
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
            Widgets.Label(0f, ref curY, frame.width, "LWMDS_PerBuildingSettings"
                          .Translate(DSStorageGroupUtility.GetDefaultLabelFor(parent)),
                          "LWMDS_PerBuildingSettingsDesc".Translate());
            Text.Font = GameFont.Small;
            //////////////////////////// Building settings ///////////////////////
            StorageGroup group = (parent as IStorageGroupMember)?.Group;
            // Earlier, I thought about having settings that could apply to only one group member....
            //   But it's better to tie all group settings together.
            if (group != null)
            {
                Rect r = new Rect(0, curY, frame.width, 46f);
                Widgets.Label(r, "LWMDS_ApplyChangesToGroup".Translate(group.MemberCount));
                if (Mouse.IsOver(r))
                {
                    TooltipHandler.TipRegion(r, "LWMDS_ApplyChangesToGroupDesc".Translate(group.MemberCount));
                }
                curY += 50f;
            }
            Widgets.DrawLineHorizontal(0f, curY + 6f, frame.width - 10f);
            curY += 15f;
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
                this.SetName("");
            }
            string newName = Widgets.TextEntryLabeled(new Rect(0f, curY, frame.width-100f, 23f),
                            "CommandRenameZoneLabel".Translate(), curName);
            if (newName.Length < this.MaxNameLength)
            {
                this.curName = newName;
            }
            curY += 28;
            ///////// max number stacks //////// //TODO: Should this lists total number for the group? YES
            Widgets.Label(0f, ref curY, frame.width, "LWMDS_CurrentMaxNumStacksTotals"
                                    .Translate(curMaxNumStacks, curMaxNumStacks * parent.OccupiedRect().Cells.EnumerableCount()));

            Widgets.Label(0f, ref curY, frame.width, "LWMDS_DefaultMaxNumStacksTotals"
                                    .Translate(cds.CdsProps.maxNumberStacks, cds.CdsProps.maxNumberStacks *
                                                                parent.OccupiedRect().Cells.EnumerableCount()));
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
                // TODO: make this a separate method with a message?
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
            if (Widgets.ButtonText(new Rect(15f, inRect.height - 35f - 15f, inRect.width - 15f - 15f, 35f), "OK", true, true, true, null) 
                || pressedEnterForOkay)
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
                    cds.MaxNumberStacks = curMaxNumStacks;
                    this.SetName(this.curName);
                    Find.WindowStack.TryRemove(this, true);
                }
            } // and Reset:
            else if (Widgets.ButtonText(new Rect(15f, inRect.height -35f -15f -50f, inRect.width-15f-15f, 35f), "ResetButton".Translate(),
                                   true,false,true)) {
                this.SetName("");
                foreach (var oc in CompsToApplyChangeTo())
                {
                    oc.ResetSettings();
                }
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
            if (name != origName)
            {
                if (name == "")
                {
                    Messages.Message("LWM_DSU_DefaultName".Translate(DSStorageGroupUtility.GetDefaultLabelFor(parent)),
                                     MessageTypeDefOf.TaskCompletion, false);
                }
                else
                {
                    Messages.Message("LWM_DSU_GainsName".Translate(DSStorageGroupUtility.GetDefaultLabelFor(parent), name),
                                     MessageTypeDefOf.TaskCompletion, false);
                }

                origName = name;
            }

            // SetLabel sets the label for the entire storage group:
            this.cds.SetLabel(name);
        }

        IEnumerable<CompDeepStorage> CompsToApplyChangeTo(bool includeThisOne = true)
        {
            if ((parent as IStorageGroupMember)?.Group == null) {
                Utils.Mess(Utils.DBF.StorageGroup, "Dialog_CompSettings applying changes to single comp: " + includeThisOne);
                if (includeThisOne) yield return cds;
                yield break;
            }
            Utils.Warn(Utils.DBF.StorageGroup, "Dialog_CompSettings applying changes to entire group: " +
                           string.Join(",", (parent as IStorageGroupMember).Group.members));
            foreach (var c in DSStorageGroupUtility.GetDSCompsFromGroup((parent as IStorageGroupMember).Group))
            {
                if (c != this.cds || includeThisOne)
                {
                    Utils.Mess(Utils.DBF.StorageGroup, "Dialog_CompSettings applying changes to comp in group: " + cds.parent);
                    yield return c;
                }
            }
        }

        // TODO:  Make a nice button, eh?
        // override InitialSize to make it bigger
        // override DoWindowContents to add a new button on top of "Okay" that says "reset"?

        private CompDeepStorage cds;
        private ThingWithComps parent;

        // Dialog_Rename has curName
        private string origName = "";
        protected int curMaxNumStacks;
	}
}
