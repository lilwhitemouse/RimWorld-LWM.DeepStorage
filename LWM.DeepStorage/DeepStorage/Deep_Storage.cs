using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;
using Harmony;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
							  // XXX:
using UnityEngine;

using static LWM.DeepStorage.Utils.DBF;

namespace LWM.DeepStorage
{
    public class Utils
    {
        //public const bool debug = false;
        static bool[] showDebug ={
            false, // No Storage Blockers In
            false, // Haul To Cell Storage Job
            false, // Try Place Direct
            false, // Spawn
            false, // Tidy Stacks Of
        };

        public enum DBF
        { // DeBugFlag
            NoStorageBlockerseIn, HaulToCellStorageJob, TryPlaceDirect, Spawn, TidyStacksOf,
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Warn(DBF l, string s) {
            if (showDebug[(int)l])
                Log.Warning("LWM." + l.ToString() + ": " + s);
        }
        [System.Diagnostics.Conditional("DEBUG")]
        public static void Err(DBF l, string s) {
            if (showDebug[(int)l])
                Log.Error("LWM." + l.ToString() + ": " + s);
        }


        public static bool CanStoreMoreThanOneThingAt(Map map, IntVec3 loc)
        {
            SlotGroup slotGroup = loc.GetSlotGroup(map);
            if (slotGroup == null || !(slotGroup?.parent is ThingWithComps) ||
                (slotGroup.parent as ThingWithComps).TryGetComp<CompDeepStorage>() == null)
            {
                return false;
                Log.Warning("CanStoreMoreThanOneThingAt: " + loc.ToString() + "? false");
                return false;
                if (slotGroup == null) Log.Warning("  null slotGroup");
                else if (slotGroup.parent == null) Log.Warning("  null slotGroup.parent");
                else if (!(slotGroup.parent is ThingWithComps)) Log.Warning("  slotGroup.parent is not ThingWithComps");
                else Log.Warning("  no CompDeepStorage");
                Log.Warning("Just for the record, " + (Scribe.mode == LoadSaveMode.LoadingVars) +
                            (Scribe.mode == LoadSaveMode.PostLoadInit) +
                            Scribe.mode);
                List<Thing> l = map.thingGrid.ThingsListAt(loc);
                foreach (Thing t in l)
                {
                    Log.Error("Did find a " + t.ToString() + " here at " + loc.ToString());
                }
                //

                return false;
            }
            //            Log.Warning("CanStoreMoreThanOneThingAt: " + loc.ToString() + "? true");
            return true;
            Log.Warning("CanStoreMoreThanOneThingAt: " + loc.ToString() + "? true!");
            List<Thing> lx = map.thingGrid.ThingsListAt(loc);
            foreach (Thing t in lx)
            {
                Log.Error("Did find a " + t.ToString() + " here at " + loc.ToString());
            }

            return true;
        }

        public static void TidyStacksOf(Thing thing)
        {
            if (thing == null || !thing.Spawned || thing.Destroyed || thing.Map == null
                || thing.Position == IntVec3.Invalid) {// just in case
                #if DEBUG
                if (Utils.showDebug[(int)DBF.TidyStacksOf]) {
                    if (!thing.Spawned) Log.Warning("Cannot tidy stack of unspawned " + thing.ToString());
                    else if (thing.Destroyed) Log.Warning("Cannot tidy stack of destroyed " + thing.ToString());
                    else if (thing.Map == null) Log.Warning("Cannot tidy stack on null map for " + thing.ToString());
                    else if (thing.Position == IntVec3.Invalid) Log.Warning("Cannot tidy invalid position for " + thing.ToString());
                }
                #endif
                return;
            } 
            // If stack limit is 1, it cannot stack
            var stackLimit = thing.def.stackLimit;
            if (stackLimit <= 1) { return; }
            // Get everything and start tidying!
            var thingStacks = thing.Map.thingGrid.ThingsAt(thing.Position)
                                   .Where(t => t.def == thing.def).ToList();
            int extra = 0; // "extra" Sheep we pick up from wherever
            for (int i = 0; i < thingStacks.Count; i++)
            {
                if (thingStacks[i] == null || thingStacks[i].Destroyed) { break; } // maybe we emptied it already
                if (thingStacks[i].stackCount == stackLimit) { continue; }
                if (thingStacks[i].stackCount > stackLimit)
                {
                    // should never happen?
                    Log.Warning("LWM.DeepStorage found " + thingStacks[i].stackCount + " of " +
                                thingStacks[i].ToString() + " when the stackLimit is " +
                                stackLimit + ". This should never happen?");
                    extra = thingStacks[i].stackCount - stackLimit;
                    thingStacks[i].stackCount = stackLimit;
                    continue;
                }
                // If we get to here, we have something like
                //   74 sheep, and the stacklimit is 75
                if (extra > 0)
                { // maybe there were some extra lying around?
                    int x = Math.Min(stackLimit - thingStacks[i].stackCount, extra);
                    thingStacks[i].stackCount += x;
                    extra -= x;
                    if (thingStacks[i].stackCount == stackLimit) { continue; }
                }
                // Now try to pick up any extra sheep from remaining stacks of sheep
                // (All stacks ab)ve here already have full stacks)
                // Start looking at the end:
                for (int j = thingStacks.Count - 1; j > i; j--)
                {
                    if (thingStacks[j] == null) { continue; } //maybe already empty
                    if (thingStacks[j].stackCount <= (stackLimit - thingStacks[i].stackCount))
                    {
                        // can absorb all
                        thingStacks[i].TryAbsorbStack(thingStacks[j], true);
                        if (thingStacks[i].stackCount >= stackLimit) { break; }
                    }
                    else
                    {
                        // more than enough
                        int x = stackLimit - thingStacks[i].stackCount;
                        thingStacks[i].stackCount = stackLimit;
                        thingStacks[j].stackCount -= x;
                        break;
                    }
                } // continue on big loop if we filled it...
                if (thingStacks[i].stackCount < stackLimit)
                {
                    break; // we can't clean up any more...continue looking for unfull stacks (probably this one)
                }
            } // end loop thru ThingsAt the location
            while (extra > 0)
            {
                //weird?  Shouldn't happen?
                int x = Math.Min(stackLimit, extra);
                extra -= x;
                Thing t = GenSpawn.Spawn(thing.def, thing.Position, thing.Map);
                t.stackCount = x;
            }
        } // end TidyStacksOf(thing)
    } // End Utils class

    public class Properties : CompProperties
    {
        public Properties() {
            this.compClass = typeof(LWM.DeepStorage.CompDeepStorage);
        }
        public int maxNumberStacks = 1;
        public int timeStoringTakes = 1000; // measured in ticks
    }

    public class CompDeepStorage : ThingComp
	{
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra()) {
                yield return g;
            }
            yield return new Command_Action
            {
                defaultLabel = "Minus One",
                action=delegate ()
                {
                    foreach (var cl in parent.GetSlotGroup().CellsList)
                    {
                        foreach (Thing t in parent.Map.thingGrid.ThingsAt(cl))
                        {
                            if (t.def.category == ThingCategory.Item)
                            {
                                if (t.stackCount > 1)
                                {
                                    Log.Warning("Lowering " + t.ToString());
                                    t.stackCount--;
                                }
                            }// item
                        }// each thing
                    }// each cell
                },// end action

            };

        }
        public int maxNumberStacks
		{
			get
			{
				return ((Properties)this.props).maxNumberStacks;
			}
		}

        public int timeStoringTakes
        {
            get
            {
                return ((Properties)this.props).timeStoringTakes;
            }
        }

        public override void Initialize (CompProperties props) {
            base.Initialize(props);
            // Remove duplicate entries and ensure the last entry is the only one left
            //   This allows a default abstract def with the comp
            //   and child def to change the comp value:
            CompDeepStorage[] list = this.parent.GetComps<CompDeepStorage>().ToArray();
            // Remove everything but the last entry:
            for (var i = 0; i < list.Length-1; i++)
            {
                this.parent.AllComps.Remove(list[i]);
            }
        }
    }







    ///////////////////////////////////////////////////////////////////////
    /// Patching:
    /// 
    /// 5??? places need to be patched to allow more than one object in Deep Storage units:
    /// 1. RimWorld.StoreUtility's NoStorageBlockersIn - no longer block if the
    ///    storage unit isn't full yet
    /// 2. Verse.AI.HaulAIUtility's HaulToCellStorageJob - let pawns know how many
    ///    of stackable things (e.g., wood) they can carry to deep storage
    /// 3. Verse.Ai.Toil_Haul's StartCarryThing - the delegate() function, so
    ///    Pawns picking up 50 of something from Deep Storage pick up more than
    ///    just the 7 in the top stack. (otherwise, they have reserved the spot
    ///    so can't get anything else lying there)
    /// 4. Verse.GenPlace's TryPlaceDirect - allow putting down (stackable) stuff
    ///    in deep storage, even if there's already stuff there.
    /// 5. Verse.GenSpawn's Spawn - allow putting 2 or more items in the same place
    /// 
    /// And then there is the loading, which requires 3 more patches to make sure
    /// items can spawn on top of each other...and not pretty patches.  Why?  B/c
    /// buildings spawn after items do...so checking if an item is in DeepStorage
    /// isn't possible during game load.
    ///////////////////////////////////////////////////////////////////////

    /**********************************************************************/

    /**********************************************************************
    * RimWorld/StoreUtility.cs:  NoStorageBlockersIn(...)
    * This allows the hauling AI to see a partly-filled deep storage area as open/empty
    * 
    * We patch via prefix by first checking if the slotGroup in question is part of a
    * Deep Storage unit.  If it is, then we take over (and duplicate a little bit of code)
    * 
    * Possible ways to make this better:
    *   Use Transpiler to inject a variable maxStacks=1 into the original
    *   Update maxStacks if it's a DeepStorage object
    *   Check maxStacks in a loop.
    * This was easier.
    **************************************/
    [HarmonyPatch(typeof(RimWorld.StoreUtility), "NoStorageBlockersIn")]
	class PatchNoStorageBlockersIn
	{
		protected static bool Prefix(IntVec3 c, Map map, Thing thing, ref bool __result)
		{
            SlotGroup slotGroup = c.GetSlotGroup(map);
			if (slotGroup == null || !(slotGroup?.parent is ThingWithComps) ||
			    (slotGroup.parent as ThingWithComps).TryGetComp<CompDeepStorage>() ==null)
			{
				return true; // normal spot, NoStorageBlockersIn() will handle it
			}

			var maxStacks = ((ThingWithComps)slotGroup.parent).GetComp<CompDeepStorage>().maxNumberStacks;
           
			var objInStack = 0;
			__result = false; // NoStorageBlockersIn returns false if there's a blocker
							  //  We return false from Patch to skip original method
			List<Thing> list = map.thingGrid.ThingsListAt(c);
			for (int i = 0; i < list.Count; i++)
			{
				Thing thing2 = list[i];
				if (thing2.def.EverStorable(false))
				{
					if (!thing2.CanStackWith(thing))
					{
						objInStack++;
					}
					else if (thing2.stackCount >= thing.def.stackLimit)
					{
						objInStack++;
					}
					else // it can stack and there's room in the stack for more...
					{ // go ahead and get out of here with the good news!
						__result = true;
                        Utils.Warn(NoStorageBlockerseIn, thing.ToString() + " at " + c.ToString() + ": " + __result);
                        return false;
					}
					if (objInStack >= maxStacks) { return false; }
					continue;
				}
				if (thing2.def.entityDefToBuild != null && thing2.def.entityDefToBuild.passability != Traversability.Standable)
				{
                    Utils.Warn(NoStorageBlockerseIn, thing.ToString() + " at " + c.ToString() + ": " + __result);
                    return false;
				}
				if (thing2.def.surfaceType == SurfaceType.None && thing2.def.passability != Traversability.Standable)
				{
                    Utils.Warn(NoStorageBlockerseIn, thing.ToString() + " at " + c.ToString() + ": " + __result);
                    return false;
				}
			}
			//You know what I can't get running in Linux?  Monodevelop's debugger.
			//Log.Warning("No storage blockers for "+thing.ToString()+" in "+slotGroup.ToString());
			__result = true; // no blockers after all!
            Utils.Warn(NoStorageBlockerseIn, thing.ToString() + " at " + c.ToString() + ": " + __result);
			return false;
		}
	}


	/**********************************************************************
     * Verse/AI/HaulAIUtility.cs:  HaulToCellStorageJob(...)
     * The important part here is that HaulToCellStorageJob counts how many
     *   of a stackable thing to carry to a slotGroup (storage)
     * 
     * We patch via prefix by first checking if the slotGroup in question is part of a
     * Deep Storage unit.  If it is, then we take over (and duplicate a little bit of code)
     * and do the more complicated calculation of how many to carry.
     * 
     * We run through the same idea as the original function with a bunch of loops thrown in
     **************************************/
	[HarmonyPatch(typeof(Verse.AI.HaulAIUtility), "HaulToCellStorageJob")]
	class Patch_HaulToCellStorageJob
	{
		public static bool Prefix(out Job __result, Pawn p, Thing t, IntVec3 storeCell, bool fitInStoreCell)
		{
            Utils.Err(HaulToCellStorageJob, "Job request for " + t.stackCount + t.ToString() + " by pawn " + p.ToString() +
                      " to " + storeCell.ToString());
            //            Log.Error("Job request for " + t.stackCount + t.ToString() + " by pawn " + p.ToString()+
            //                      " to "+storeCell.ToString());
            SlotGroup slotGroup = p.Map.haulDestinationManager.SlotGroupAt(storeCell);
			if (slotGroup == null || !(slotGroup?.parent is ThingWithComps) ||
			    (slotGroup.parent as ThingWithComps).TryGetComp<CompDeepStorage>() == null)
			{
				__result = null;
				return true; // normal spot, HaulToCellStorageJob will handle it
			}
			// Create our own job for hauling to Deep_Storage units...
			Job job = new Job(JobDefOf.HaulToCell, t, storeCell);
			job.haulOpportunisticDuplicates = true;
			job.haulMode = HaulMode.ToCellStorage;

			__result = job;

			if (t.def.stackLimit <= 1)
			{
				job.count = 1;
                Utils.Err(HaulToCellStorageJob, "haulMaxNumToCellJob: " + t.ToString() + ": stackLimit<=1 so job.count=1");
				return false;
			}
			// we have to count :p
			//TODO: really?  statValue is the carrying capacity of the Pawn; original code is odd
			float statValue = p.GetStatValue(StatDefOf.CarryingCapacity, true);
			job.count = 0;

			var maxStacks = ((ThingWithComps)slotGroup.parent).GetComp<CompDeepStorage>().maxNumberStacks;
            Utils.Err(HaulToCellStorageJob, p.ToString() + " taking " + t.ToString() + ", count: " + t.stackCount);
			var howManyStacks = 0;
			//fill job.count with space in the direct storeCell:
			List<Thing> stuffHere = p.Map.thingGrid.ThingsListAt(storeCell);
			for (int i = 0; i < stuffHere.Count; i++) // thing list at storeCell
			{
				Thing thing2 = stuffHere[i];
				// We look thru for stacks of storeable things; if they match our thing t, 
				//   we see how many we can carry there!
				if (thing2.def.EverStorable(false))
				{
                    Utils.Warn(HaulToCellStorageJob, "... already have a stack here of " + thing2.stackCount + " of " + thing2.ToString());
					howManyStacks++;
					if (thing2.def == t.def)
					{
						job.count += thing2.def.stackLimit - thing2.stackCount;
                        Utils.Warn(HaulToCellStorageJob, "... count is now " + job.count);
						if ((float)job.count >= statValue || job.count >= t.def.stackLimit)
						{
							job.count = Math.Min(t.def.stackLimit, job.count);
                            Utils.Warn(HaulToCellStorageJob, "Final count: " + job.count + " (can carry: " + statValue + ")");
							return false;
						}
					}
				} // if storeable
			} // thing list at storeCell

			if (howManyStacks < maxStacks || job.count >= t.def.stackLimit)
			{
				// There's room for a whole stack right here!
				job.count = t.def.stackLimit;
                Utils.Warn(HaulToCellStorageJob, "Room for whole stack! " + howManyStacks + "/" + maxStacks);
				return false;
			}
			if (fitInStoreCell)
			{ // don't look if pawn can put stuff anywhere else
				return false;
			}
            //  If !fitInStoreCell, we look in the other cells of the storagearea to see if we can put some there:
            //     (personally, I'd be okay if my pawns also tried NEARBY storage areas,
            //      but that's getting really complicted.)
            Utils.Warn(HaulToCellStorageJob, "Continuing to search for space in nearby cells...");
			List<IntVec3> cellsList = slotGroup.CellsList;
			for (int i = 0; i < cellsList.Count; i++)
			{
				IntVec3 c = cellsList[i];
				if (c == storeCell)
				{
					continue;
				}
				if (!StoreUtility.IsGoodStoreCell(c, p.Map, t, p, p.Faction))
				{
					continue;
				}
				//repeat above counting of space in the current store cell:
				stuffHere = p.Map.thingGrid.ThingsListAt(c);
				howManyStacks = 0;
				for (int j = 0; j < stuffHere.Count; j++)
				{
					Thing thing2 = stuffHere[j];
					// We look thru for stacks of storeable things; if they match our thing t, 
					//   we see how many we can carry there!
					if (thing2.def.EverStorable(false))
					{
                        Utils.Warn(HaulToCellStorageJob, "... already have a stack here of " + thing2.stackCount + " of " + thing2.ToString());
                        howManyStacks++;
                        if (thing2.def == t.def)
						{
                            job.count += thing2.def.stackLimit - thing2.stackCount;
                            Utils.Warn(HaulToCellStorageJob, "... count is now " + job.count);
                            if ((float)job.count >= statValue || job.count >= t.def.stackLimit)
							{
								job.count = Math.Min(t.def.stackLimit, job.count);
                                Utils.Warn(HaulToCellStorageJob, "Final count: " + job.count + " (can carry: " + statValue + ")");
                                return false;
							}
						}
					} // if storeable
				} // looking at all stuff in c
				  // How many stacks were there? in c?
				if (howManyStacks < maxStacks)
				{
                    // There's another stack's worth of room!
                    Utils.Warn(HaulToCellStorageJob, "Room for whole stack! " + howManyStacks + "/" + maxStacks);
                    job.count = t.def.stackLimit;
					return false;
				}
			} // looking at all cells in storeage area
              // Nowhere else to look, no other way to store anything we pick up.
              // Count stays at job.count
            Utils.Warn(HaulToCellStorageJob, "Final count: " + job.count);
			return false;
		}



	}



	/**********************************************************************
     * Verse/AI/Toils_Haul.cs:  StartCarryThing(...)
     * The Haul job has a bunch of sub-jobs ("toils") and this one picks
     * up a thing from the ground.
     * 
     * The code returns a Toil object that has a delegate() initAction
     * 
     * We insert code into the delegate() function created for the Haul toil
     * to get around a problem that pawns have picking up from Deep Storage:
     * If they are picking up a stack of, say, 7 wood, and they need 25, 
     * they don't look for any more wood in the Deep Storage (probably because
     * it has been reserved....by our pawn :p )  So we patch to check if the Thing 
     * being picked up is in Deep Storage, and if it is, see if its stackCount
     * can be adjusted to match the job from other stacks in the Deep Storage unit.
     * #DeepMagic #YouHaveBeenWarned
     **************************************/
	[HarmonyPatch]
	public class Patch_StartCarryThing_Delegate
	{
		public static Type predicateClass;
		static MethodBase TargetMethod()//The target method is found using the custom logic defined here
		{
            //c__AnonStorey0 is the hidden IL class that is created for the delegate() function
            // created by StartCarryThing.  We need this class later on.

            predicateClass = typeof(Verse.AI.Toils_Haul).GetNestedTypes(Harmony.AccessTools.all)
			   .FirstOrDefault(t => t.FullName.Contains("c__AnonStorey0"));
            if (predicateClass == null) {
                Log.Error("LWM.Deep_Storage: Could not find Verse.AI.Toils_Haul:c__AnonStorey0");
                return null;
            }
            // This fails, by the way:  predicateClass.GetMethod(alreadyFoundMethod.Name).
            //   No idea why.
            // Is this matching <>m__0?  Or is it returning the first one, which is what
            //   we want anyway?  Who knows!  But this works.  #DeepMagic
            var m=predicateClass.GetMethods(AccessTools.all)
								 .FirstOrDefault(t => t.Name.Contains("m__"));
            if (m==null) {
                Log.Error("LWM.Deep_Storage: Cound not find Verse.AI.Toils_Haul:c__AnonStorey0<>m__0");
            }
            return m;
		}
		// StartCarryThing's delegate starts with several (very reasonable) tests.
		//   We want to insert our function call right after the tests, but
		//   before the first bit that checks the stack size of the Thing being carried:
		//   if (failIfStackCountLessThanJobCount && thing.stackCount<curJob.count)
		//        {
		//            actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
		//            return;
		//        }
		//   If the test before this is passed, the code jumps to right where we want
		//     to insert.
		//   On failure, there is code with the unique string
		//      "StartCarryThing got availableStackSpace "
		//   So we look for that string, and then once we've found it, the next
		//     throw OpCode marks the end of the test.  That's where we insert our code:
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var code = new List<CodeInstruction>(instructions);
			// have we passed "StartCarryThing got availableStackSpace "?
			bool foundCorrectException = false;
			// We keep track of where every BrTrue jump goes:
			//    One of them will be where we insert our code:
			System.Reflection.Emit.Label branchLabel;
			int i = 0; // we do 2 for loops on the same i
			for (; i < code.Count-1; i++)
			{
				if (code[i].opcode == OpCodes.Brtrue)
				{ // keep track of where Branch on True are going
                    branchLabel = (Label)code[i].operand;
                } 
				else if (!foundCorrectException && code[i].opcode == OpCodes.Ldstr &&
					(string)code[i].operand == "StartCarryThing got availableStackSpace ")
				{
					// We have passed the correct Branch on True command and 
                    // are in the correct error code - we will insert after the exception is thrown!
					foundCorrectException = true;
				}
				else if (foundCorrectException && code[i].opcode==OpCodes.Throw) {
					yield return code[i]; // the "throw"
					i++; //advance past throw
					// We steal the label from the next code and use it as the anchor the
					//   Brtrue jump goes to:
					if (!code[i].labels.Remove(branchLabel)) {
						throw new SystemException("LWM.DeepStorage: Patching StartCarryThing failed: label failure");
					}
					// Ldarg_0 - this delegate() function on stack
					var tmp = new CodeInstruction(OpCodes.Ldarg_0);
					// It needs the jump label:
					tmp.labels.Add(branchLabel);
					yield return tmp;
					// pop the delegate() function off the stack to load its "toil" field:
					yield return new CodeInstruction(OpCodes.Ldfld,
					      Harmony.AccessTools.Field(predicateClass,"toil"));
					yield return new CodeInstruction(OpCodes.Ldloc_2); // This SHOULD be Thing thing.
					yield return new CodeInstruction(OpCodes.Ldloc_S, 4); // This SHOULD be num.
					// Now call FillThisStackIfAble(toil, thing, num):
					yield return new CodeInstruction(OpCodes.Call, Harmony.AccessTools
					       .Method("LWM.DeepStorage.Patch_StartCarryThing_Delegate:FillThisStackIfAble"));
					break; // exit this complicated for loop
				}
                // nothing special, return the instruction
                yield return code[i];
            }
			// Just return almost all the rest:
			// I know there's probably a snappy one-line C# way to do it, but this is fine:
			for (; i < code.Count-1; i++)
			{
				yield return code[i];
			}
			var cleanUp= new CodeInstruction(OpCodes.Call, Harmony.AccessTools
			      .Method("LWM.DeepStorage.Patch_StartCarryThing_Delegate:CleanUpStacks"));
			// save the last instruction to swipe any labels/etc from the Return call
			cleanUp.labels = code[i].labels;
			yield return cleanUp;

			var retCode = new CodeInstruction(OpCodes.Ret);
			retCode.operand = code[i].operand;
			retCode.blocks = code[i].blocks;
			yield return retCode;
			yield break;
        }
        // helper variables for cleanup after picking thigns up:
        public static Thing tmpThing;
        // Helper function to try to give "thing" a stack size of "job.count"
        //   So if a pawn wants to pick up sheep, and thing is a stack of 7 sheep,
        //   but the job size says to pick up 50 sheep...we look for other stacks
        //   of sheep in the same square to shift some of them to "thing"
		public static void FillThisStackIfAble(Toil toil, Thing thing, int carryCapacity)
		{
			//Log.Error("FillThisStack with " + thing.ToString() + "("+thing.stackCount+") able to carry " 
            //          + carryCapacity + " of job " + toil.actor.jobs.curJob.count);
			tmpThing = null;
			// We'd like thing to have at least num in our stack to pick up at once:
			int num = Math.Min(carryCapacity, toil.actor.jobs.curJob.count);
			if (thing.stackCount >= num) { return; }
			var slotGroup = thing.Map.haulDestinationManager.SlotGroupAt(thing.Position);
			if (slotGroup == null || !(slotGroup?.parent is ThingWithComps) ||
			    ((ThingWithComps)slotGroup.parent).TryGetComp<CompDeepStorage>() == null) {
				//Giant piles on the ground can be messy, so I am okay with
                //  inefficiently picking stuff up from here.
				//  TODO: an option, perhaps, to avoid this return?
				return;
			}
            tmpThing = thing;
			var tmpMap = thing.Map;
			var tmpLoc = thing.Position;
			var thingsHere = tmpMap.thingGrid.ThingsListAt(tmpLoc);
			foreach (Thing otherThing in thingsHere) {
				if (otherThing == thing) { continue; }
				if (!otherThing.CanStackWith(thing)) { continue; }
				if (otherThing.stackCount <= num-thing.stackCount) {
					thing.TryAbsorbStack(otherThing, false); // false is respectStackLimit:
					        // If the pawn can carry it, I don't care.
					        // Maybe this is foolish?
					if (thing.stackCount >= num) { return; }
					continue;
				}
				otherThing.stackCount -= num - thing.stackCount;
				thing.stackCount = num;
				return;
			}
		}
		// Make sure stacks of type tmpDef (that is, what the pawn just picked up) are tidy
        // For the record, I don't think this is necessary, but I would rather be safe.
        // CleanUpStacks
        //   Tidy stacks of 74 sheep, 74 sheep, and 74 sheep into stacks of
        //   75 sheep, 75 sheep, and 72 sheep.
		public static void CleanUpStacks() {
            if (tmpThing == null) {
				return; // only do this if the pawn in messing around in Deep Storage
			}
            Utils.TidyStacksOf(tmpThing);
		} //end CleanUpStacks
	}
	//As an aside, pawns will sometimes still refuse to carry stacks.  This behavior
    //  happens with the default shelves, too.  This is not the mod to fix/change that.
	//[HarmonyPatch(typeof(Verse.AI.Toils_Haul),"CheckForGetOpportunityDuplicate")]

	/**********************************************************************
     * Verse/GenPlace.cs:  TryPlaceDirect(...)
     * This handles putting something down on the ground or in a slotGroup (storage)
     * 
     * We patch the original by waiting until it's dropped/placed whatever it can 
     * and then seeing if it was putting stuff in a Deep Storage unit.  If it was,
     * then it might not have put down everything it can!  So we check and if 
     * there is anything else to place, we check for room, etc.
     * 
     * We also check to make sure the stacks are tidy - otherwise it may be impossible
     * for pawns to clean up stacks ("merge" jobs), where the pawn picks up from the 
     * small top stack to merge with a lower stack...but then puts it right back down
     * on top - oops.
     * 
     * After the unpatched version runs, it returns false iff:
     *   1)  a flag is thrown - because something is trying to place more than a stacklimit?
     *       (so, for example, trying to place 100 wood, when the stacklimit is 75)
     *       This probably only happens if a pawn somehow ends up carrying a large number
     *       of things, like if they butcher a big animal and have a lot of meat.
     *   2)  Only some of the stack (perhaps 0) could actually be placed...
     * In case 2, if we were placing something in Deep Storage, there might still
     * be room, so we try again.
     * 
     * We patch via prefix to catch case 1) above.
     * We patch via postfix to catch case 2).
     * We also patch via postfix to make sure all of the stacks are tidy
     **************************************/
	[HarmonyPatch(typeof(Verse.GenPlace),"TryPlaceDirect")]
	class Patch_TryPlaceDirect
	{
        static void Prefix(ref Thing __state, Thing thing, IntVec3 loc, Map map) // TODO: XXX last 2
        {
            // TryPlaceDirect changes what the variable "thing" points to
            //   We do actually need the original value of "thing":
            __state = thing;
            Utils.Err(TryPlaceDirect, "LWM:TryPlaceDirect: going to place " + thing.stackCount + thing.ToString() + " at " + loc.ToString());
        }
        static void Postfix(ref bool __result, Thing __state, IntVec3 loc, Map map, 
		                    ref Thing resultingThing, Action<Thing, int> placedAction) {
            Thing thing = __state;
            // First check if we're dropping off in Deep Storage:
            SlotGroup slotGroup = loc.GetSlotGroup(map);

            if (slotGroup == null || !(slotGroup?.parent is ThingWithComps) ||
                ((ThingWithComps)slotGroup.parent).TryGetComp<CompDeepStorage>() == null)
            {
                Utils.Warn(TryPlaceDirect, "  (placed NOT in Deep Storage: with result " + __result + ")");
                return;
            }

            if (resultingThing != null) {
                // Two cases here if resultingThing exists:
                //   1. There's a new object on the map
                //        (so resultingThing is the original thing, we don't have anyting to put down)
                //   2. Everything we put down was able to stack with something on the map
                //        (so nothing else to put down!)
                //   (case three - something weird going on possibly involving carrying?)
                //        (all bets are off anyway >_< )
                // Probably, the pawn put down what they were carrying, and all is good.
                Utils.Warn(TryPlaceDirect, "  successfully placed " + resultingThing.stackCount + resultingThing.ToString());
                Utils.TidyStacksOf(resultingThing);
				return;
            }
            // Ok, so we still have something we want to place, and it goes in Deep Storage
            //   (We will still return __result=false if we can't place everything in the desired space.)

            Utils.Err(TryPlaceDirect, "LWM:TryPlaceDirect tried to place " + thing.ToString() + " in " + slotGroup.ToString());

            // Let's see if there's still room in the Deep Storage area the pawn is using:
			List<Thing> list = map.thingGrid.ThingsListAt(loc);
			//     so many castings...  slotGroup.parent has to be a DeepStorageDef to have a def
			//        have to cast the def as the mod's def to access my field...
			int maxNumberStacks = ((ThingWithComps)slotGroup.parent).GetComp<CompDeepStorage>().maxNumberStacks;
			int thingsHere = 0;
            // We know there was at least one thing here, and it either doesn't stack with our thing
            //   or its stack is full.
            // So, we go thru the items that are there, but starting at [1]:
            //  (1 should be safe because it would already have been accessed by default function)
            for (int i = 1; i < list.Count; i++)
            {
                Thing thing2 = list[i];
                if (!thing2.def.EverStorable(false))
                {
                    //not an object we count
                    continue;
                }
                thingsHere++;
                //unfortunately, we have to duplicate some of the original code:
                if (!thing2.CanStackWith(thing))
                {
                    Utils.Warn(TryPlaceDirect, "...ignoring \"other\" stack " + thing2.ToString());
                    //                    if (Utils.__debug_TryPlaceDirect) { Log.Warning( }
                    continue;  // am carrying wood, but this is sheep.  Or rock, whatever.
                }
                // Okay, can stack.
                if (thing2.stackCount >= thing2.def.stackLimit)
                {
                    Utils.Warn(TryPlaceDirect, "...ignoring full stack " + thing2.ToString());
                    //                    if (Utils.__debug_TryPlaceDirect) { Log.Warning( }
                    continue; // stack is full.
                }
                // Put some down in the non-full stack!
                var origStackCount = thing.stackCount;
                if (thing2.TryAbsorbStack(thing, true))
                {
                    // the "thing2" stack could hold everything we wanted to put down!
                    Utils.Warn(TryPlaceDirect, "... Object " + thing2.ToString() + " absorbed ALL of " + thing.ToString());
                    //                    if (Utils.__debug_TryPlaceDirect) { Log.Warning( }
                    resultingThing = thing2;
                    if (placedAction != null)
                    {
                        placedAction(thing2, origStackCount);
                    }
                    Utils.TidyStacksOf(thing2);
                    __result = true; // Could put down everything!
                    return;
                }
                Utils.Warn(TryPlaceDirect, "... Object " + thing2.ToString() + " absorbed SOME of " + thing.ToString());
                //if (Utils.__debug_TryPlaceDirect) { Log.Warning(; }
                // Since we tried to put some down in that stack, do we do placedAction?
                if (placedAction != null && origStackCount != thing.stackCount) {
					placedAction(thing2, origStackCount - thing.stackCount);
				}
				// ...but there's still more to place, so keep going:
			} // end loop of objects in this location
            if (thingsHere >= maxNumberStacks)
            { // Ran out of room in the storage object but still want to put stuff down ;_;
                Utils.Warn(TryPlaceDirect, "...But ran out of stack space here: all " + maxNumberStacks + " filled");
                __result = false;
                return; // no need to TidyStacks here - they have to be all full
            }
            // if we reach here, we found an empty space,
            //   so put something down!
            Utils.Warn(TryPlaceDirect, "...Found empty stack space at " + loc.ToString() +
                       ". Trying to create from " + thing.ToString());
            // In some circumstances (e.g., butchering a large animal), a pawn can be putting
            //   down way more than stackLimit of something.  We deal with that scenario here
            //////////////////////// NOTE:  POSSIBLE PROBLEM: ////////////////////
            //   resultingThing is set to only the last thing here - it's possible we miss something
            //   important by placing as much as we can at once:
            while (thing.stackCount > thing.def.stackLimit) {// put down part of a carried load?
                Thing thing2 = thing.SplitOff(thing.def.stackLimit);
                resultingThing = GenSpawn.Spawn(thing2, loc, map);
                Utils.Warn(TryPlaceDirect, "...put down one full stack ("
                           + resultingThing.ToString() + "); " + thing.stackCount + " left");
                if (placedAction != null)
                {
                    placedAction(thing2, thing2.stackCount);
                }
                thingsHere++;
                if (thingsHere >=maxNumberStacks) { // Oh dear.  There was still at least SOME left...
                    Utils.Warn(TryPlaceDirect, "...But ran out of stack space here: all "
                               + maxNumberStacks + " filled but still have " + thing.stackCount + " left");
                    __result = false; // couldn't put things down
                    return; // no need to TidyStacks - they all have to be full
                }
            }
            // Either Spawn the thing (or Spawn the final part of the thing):
            resultingThing = GenSpawn.Spawn(thing, loc, map);
            __result = true; // nothing unplaced!

            Utils.Warn(TryPlaceDirect, "...created " + resultingThing.ToString());
			if (placedAction != null)
			{
                placedAction(thing, thing.stackCount);
			}
            Utils.TidyStacksOf(resultingThing);
			return; // with __result=!flag (probably "true");
		} // end TryPlaceDirect's Postfix
	} // end patching TryPlaceDirect



	/**********************************************************************
	 * Verse/GenSpawn.cs:  Spawn(...)
	 * This is THE function that puts things on the map.
	 * 
	 * Prior to B0.19, Spawn would happily place one item on top of another
	 * item.  That changed in B.019, and obviously, this is a problem for 
	 * us.  Currently, there is a check during spawning, and if there is
	 * another "item"-category object there, that item is bumped aside
	 * (placed in "near" mode).
	 * 
	 * We patch Spawn(...) to change the test from
	 *     if (newThing.def.category == ThingCategory.Item)
	 * to
	 *     if (newThing.def.category == ThingCategory.Item && 
	 *         !LWM.DeepStorage.Utils.CanStoreMoreThanOneThingAt(Map,loc))
	 * (I could probably manage without the Utils function, but this is
	 *  much easier in terms of C# -> IL magic)
     **************************************/
	[HarmonyPatch(typeof(Verse.GenSpawn),"Spawn",new Type[] {typeof(Thing),typeof(IntVec3),typeof(Map),typeof(Rot4),typeof(WipeMode),typeof(bool)})]
	class Patch_GenSpawn_Spawn {
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			// replace if (newThing.def.category == ThingCategory.Item)
            // with
			//         if (newThing.def.category == ThingCategory.Item && 
			//             !LWM.DeepStorage.Utils.CanStoreMoreThanOneThingAt(Map,loc))
			// The original branch in the code is at:
			//          /* 0x002B85A2 02           */ IL_015E: ldarg.0
            //          /* 0x002B85A3 7B93360004   */ IL_015F: ldfld     class Verse.ThingDef Verse.Thing::def
            //          /* 0x002B85A8 7BCB2C0004   */ IL_0164: ldfld valuetype Verse.ThingCategory Verse.ThingDef::category
			//          /* 0x002B85AD 18           */ IL_0169: ldc.i4.2
            //          /* 0x002B85AE 40BA000000   */ IL_016A: bne.un IL_0229
			//  We find that spot (it's the first time .def.category comes up)
			//  and then add a second check for CanStoreMoreTHanOneThingAt, also jumping to IL0229 
			//  if we fail(pass?).
			var code = new List<CodeInstruction>(instructions);
			var i = 0; // using two for loops
			for (; i < code.Count; i++) {
				yield return code[i];
				if (code[i].opcode==OpCodes.Ldarg_0) { // thing
					if (code[i+1]?.opcode==OpCodes.Ldfld && // thing.def
					    code[i+1]?.operand == typeof(Verse.Thing).GetField("def")) {
						i++;
						yield return code[i];
						if (code[i + 1]?.opcode == OpCodes.Ldfld && //thing.def.category!
						    code[i + 1]?.operand == typeof(Verse.ThingDef).GetField("category")) {
							i++;
							yield return code[i++]; // the category
							yield return code[i++]; // ldc.i4.2  ("item" category)
							// i now points to the branch operation; we need the label
							System.Reflection.Emit.Label branchLabel;
							branchLabel = (Label)code[i].operand;
							yield return code[i++];
							CodeInstruction c;
							c = new CodeInstruction(OpCodes.Ldarg_2); // map
							yield return c;
							c = new CodeInstruction(OpCodes.Ldarg_1); // loc
							yield return c;
							c = new CodeInstruction(OpCodes.Call,Harmony.AccessTools.Method(
								"LWM.DeepStorage.Utils:CanStoreMoreThanOneThingAt"));
							yield return c; // Utils.CanStoreMoreThanOneThingAt(map, loc);
							c = new CodeInstruction(OpCodes.Brtrue, branchLabel);
							yield return c; // if CanStoreMoreThanOneThing, skip this section
							break; // Done with this for loop                            
						}
					}
				}
			}
			// finish the rest of the function:
			for (; i < code.Count; i++) {
				yield return code[i];
			}
			yield break;
		}
	} // done with patching GenSpawn's Spawn(...)



} // close LWM.DeepStorage namespace.  Thank you for reading!  =^.^=
