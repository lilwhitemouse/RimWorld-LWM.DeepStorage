using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using RimWorld;
using Verse;
using UnityEngine;

namespace LWM.DeepStorage
{
    // TODO: show new message for maxNumStacks changing
    // NOTE: Any changes that happen can be directed to the Comp - it'll handle any weird storage group things
	public class Dialog_CompSettings : Dialog_RenameBuildingStorage_CreateNew
	{
        public Dialog_CompSettings(ThingWithComps parent) : base(parent as IStorageGroupMember)
        //public Dialog_CompSettings(CompDeepStorage cds, Thing parent = null)
        {
            this.cds = DSStorageGroupUtility.GetOrTryMakeCompFrom(parent);
            this.parent = parent;
            if (cds == null) return;
            if (!(parent is IStorageGroupMember))
            {
                Log.Warning("LWM.DeepStorage: Dialog_CompSettings called but " + parent + " is not an IStorageGroupMember");
                return;
            }
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
            this.curMaxNumStacks = cds.MaxNumberStacks; // TODO: maybe this should be different, in case we want to allow quality to affect MaxNumStacks?
            origName = curName;
            origMaxNumStacks = curMaxNumStacks;
        }

        public override void DoWindowContents(Rect inRect) {
            if (cds == null)
            {
                Log.Error("CompDeepStorage is null - this should never happen");
                return;
            }
            Rect tmpR;
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
            if (group != null && group.MemberCount > 1)
            {
                tmpR = new Rect(0, curY, frame.width, 25f);
                Widgets.Label(tmpR, "LWMDS_ApplyChangesToGroup".Translate(group.MemberCount));
                if (Mouse.IsOver(tmpR))
                {
                    TooltipHandler.TipRegion(tmpR, "LWMDS_ApplyChangesToGroupDesc".Translate(group.MemberCount));
                }
                curY += 30f;
            }
            Widgets.DrawLineHorizontal(0f, curY + 6f, frame.width - 10f);
            curY += 15f;
            ////////// Rename //////////
            // NOte: this has to go first to keep focus when other buttons appeaer and disappear
            string newName = Widgets.TextEntryLabeled(new Rect(0f, curY, frame.width - 100f, 23f),
                "Rename".Translate(), curName);
            if (newName.Length < this.MaxNameLength)
            {
                this.curName = newName;
            }
            // default/remove button:
            if (group != null)
            {
                tmpR = new Rect(frame.width - 95f, curY, 45f, 23f);
                if (Mouse.IsOver(tmpR))
                {
                    // TODO: tooltips for different numbers of groups?
                    TooltipHandler.TipRegion(tmpR, "LWMDS_DefaultRemoveNameDesc".Translate());
                }
                if (Widgets.ButtonText(tmpR, group.MemberCount > 1 ? "Default".Translate() : "LWMDS_Remove".Translate(),
                           true, false, true))
                {
                    var t = parent as IStorageGroupMember;
                    if (t!= null)
                    {
                        var oName = t.Group?.RenamableLabel;
                        if (oName != null)
                        {
                            t.Group.RenamableLabel = null;
                            curName = StorageGroupManager.NewStorageName(t.Group.BaseLabel);
                            t.Group.RenamableLabel = oName;
                        }
                    }
//                    RemoveName(); // TODO: Make "default" option not do anything immediately - make it change curName
                }
            }
            // If names aren't the same and they aren't both empty:
            if (origName != curName && !(origName.NullOrEmpty() && curName.NullOrEmpty()))
            {
                // reset name button:
                tmpR = new Rect(frame.width - 45f, curY, 45f, 23f);
                if (Widgets.ButtonText(tmpR, "ResetButton".Translate(),
                           true, false, true))
                {
                    curName = origName;
                }
            }
            curY += 28;
            ///////// max number stacks //////// //TODO: Should this lists total number for the group? YES
            Widgets.Label(0f, ref curY, frame.width, "LWMDS_CurrentMaxNumStacksTotals"
                                    .Translate(curMaxNumStacks, curMaxNumStacks * parent.OccupiedRect().Cells.EnumerableCount()));

            Widgets.Label(0f, ref curY, frame.width, "LWMDS_DefaultMaxNumStacksTotals"
                                    .Translate(cds.CdsProps.maxNumberStacks, cds.CdsProps.maxNumberStacks *
                                                                parent.OccupiedRect().Cells.EnumerableCount()));
            //// text box to change:
            string tmpString = curMaxNumStacks.ToString();
            // Set min of 0 and max of 1024, because why not?
            Widgets.TextFieldNumericLabeled<int>(new Rect(0, curY, frame.width - 100f, 46f), "LWM_DS_maxNumStacks".Translate(),
                                   ref curMaxNumStacks, ref tmpString, 0, 1024);
            //// default button:
            if (curMaxNumStacks != cds.CdsProps.maxNumberStacks && Widgets.ButtonText(new Rect(frame.width - 95f, curY, 45f, 23f), "Default".Translate(),
                       true, false, true))
            {
                this.curMaxNumStacks = cds.CdsProps.maxNumberStacks;
            }
            //// reset button:
            if ((curMaxNumStacks != origMaxNumStacks) &&  Widgets.ButtonText(new Rect(frame.width - 45f, curY, 45f, 23f), "ResetButton".Translate(),
                       true, false, true))
            {
                curMaxNumStacks = origMaxNumStacks;
            }
            curY += 50f;

            /////////////////////////// DEFAULT & OK buttons ////////////////////////////
            // OK:
            if (Widgets.ButtonText(new Rect(15f, inRect.height - 35f - 15f, inRect.width - 15f - 15f, 35f), "OK", true, true, true, null) 
                || pressedEnterForOkay)
            {
                AcceptanceReport acceptanceReport = this.NameIsValid(this.curName);
                // If the name is something and it's bad and the name has changed, then we complain:
                if (!curName.NullOrEmpty() && !acceptanceReport.Accepted && !(curName == origName))
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
                    Log.Warning("Setting max num stacks to " + curMaxNumStacks);
                    cds.MaxNumberStacks = curMaxNumStacks;
                    OnRenamed(curName);
                    Find.WindowStack.TryRemove(this, true);
                }
            } // and DEFAULT:
            else if (Widgets.ButtonText(new Rect(15f, inRect.height -35f -15f -50f, inRect.width-15f-15f, 35f), "Default".Translate(),
                                   true,false,true)) {
                RemoveName();
                cds.ResetSettings();
                Find.WindowStack.TryRemove(this, true);
            }
            GUI.EndGroup(); // very important for this to be called

        }


        protected override void OnRenamed(string name)
        {
            if (origName == curName) return;
            if (origName.NullOrEmpty() && curName.NullOrEmpty()) return;
            if (curName.NullOrEmpty())
            {
                RemoveName();
                return;
            }
            // Why Ludeon made two separate Dialogs instead of one that handles the _CreateNew option
            //   inside OnRenamed is beyond me. But whatever.
            if ((parent as IStorageGroupMember).Group == null)
            {
                base.OnRenamed(name);
            }
            else
            {
                (parent as IStorageGroupMember).Group.RenamableLabel = name;
            }
        }
        void RemoveName()
        {
            var t = parent as IStorageGroupMember;
            if (t.Group == null) return;
            if (t.Group.MemberCount == 1)
            {
                curName = "";
                origName = curName;
                t.SetStorageGroup(null);
                return;
            }
            t.Group.RenamableLabel = null; // Remove the current name before trying to get next avaiable name
            //                                (otherwise, Group 4, Group 5, Group 4, Group 5, &c
            t.Group.RenamableLabel = StorageGroupManager.NewStorageName(t.Group.BaseLabel);
            curName = t.Group.RenamableLabel;
            origName = curName;
        }
        /*
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
        */

        // TODO:  Make a nice button, eh?
        // override InitialSize to make it bigger
        // override DoWindowContents to add a new button on top of "Okay" that says "reset"?

        private CompDeepStorage cds;
        private ThingWithComps parent;

        // Dialog_Rename has curName
        private string origName = "";
        private int curMaxNumStacks;
        private int origMaxNumStacks;
	}
}
