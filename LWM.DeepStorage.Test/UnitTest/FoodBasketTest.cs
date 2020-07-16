using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using UnityEngine;
using Verse;

namespace LWM.DeepStorage.UnitTest
{
    public class FoodBasketTest : DeepStorageTest
    {
        private const string _defName = "LWM_Food_Basket";

        private ThingDef _defBasket = ThingDef.Named(_defName);

        private ThingDef _defHerb = ThingDefOf.MedicineHerbal;

        private Thing _foodBasket;

        private IntVec3 _position;

        private Map _map;

        private CompCachedDeepStorage _comp;

        public FoodBasketTest() {

        }

        #region Overrides of DeepStorageTest

        public override void Setup() {
            Pawn pawn = PawnsFinder.HomeMaps_FreeColonistsSpawned.First();
            _position = pawn.Position;
            _map = pawn.Map;

            _foodBasket = ThingMaker.MakeThing(_defBasket, GenStuff.DefaultStuffFor(_defBasket));
            ((Building_Storage)_foodBasket).settings.filter.SetAllowAll(null); 
            _comp = _foodBasket.TryGetComp<CompCachedDeepStorage>();

            GenPlace.TryPlaceThing(_foodBasket, pawn.Position, pawn.Map, ThingPlaceMode.Direct);
        }

        public override void Run(out bool result) {

            // Store Herbal Medicine x 25 to the allowed maximum number of stacks for food basket in a cell.
            result = StoreXUntilStackFull(_comp, _defHerb, 1, _map, _position, out List<Thing> storedThings1);

            // Store Herbal Medicine one by one until the basket is full.
            result &= StoreXUntilStackFull(
                _comp, _defHerb, _defHerb.stackLimit, _map, _position + new IntVec3(1, 0, 0), out List<Thing> storedThings2);

            // Take out one full stack of Herbal Medicine at a time.
            result &= RemoveXUntilEmpty(_comp, storedThings1, _defHerb.stackLimit, _map, _position);

            // Take out one Herbal Medicine at a time.
            result &= RemoveXUntilEmpty(_comp, storedThings2, 1, _map, _position + new IntVec3(1, 0, 0));

            result &= StoreXUntilStackFull(_comp, _defHerb, 23, _map, _position, out storedThings1);
            result &= RemoveXUntilEmpty(_comp, storedThings1, 23, _map, _position);

            _comp.CellStorages.Storages.ForEach(c => c.PrintStates());
        }

        #endregion

        private static bool StoreToFullStacksWithStackLimit(CompCachedDeepStorage comp, ThingDef def, Map map,
                                                            IntVec3 cell, out List<Thing> storedThings) {
            bool result = false;
            storedThings = new List<Thing>();
            for (int i = 0; i < comp.maxNumberStacks; i++) {
                Thing temp = ThingMaker.MakeThing(def);
                temp.stackCount = def.stackLimit;

                // Used for query NonFullThings. Using temp, which is destroyed because TryAbsorbStack(), will return value not found.
                Thing temp2 = ThingMaker.MakeThing(def);

                float unitWeight = temp.GetStatValue(StatDefOf.Mass);
                storedThings.Add(temp);

                GenPlace.TryPlaceThing(temp, cell, map, ThingPlaceMode.Direct);

                result = comp.CellStorages.TestCellStorageWeight(cell, unitWeight * (i + 1) * def.stackLimit);
                result &= comp.CellStorages.TestCellStorageStack(cell, i + 1);
                result &= comp.CellStorages.TestSpareSpaceOnNonFull(temp2, cell, 0);
                result &= comp.TestCellStorageCapacity(temp, map, cell, (comp.maxNumberStacks - i - 1) * def.stackLimit);
            }

            return result;
        }

        private static bool StoreToFullStacksWithOneCount(CompCachedDeepStorage comp, ThingDef def, Map map,
                                                            IntVec3 cell, out List<Thing> storedThings) {
            bool result = false;
            storedThings = new List<Thing>();

            for (int i = 0; i < comp.maxNumberStacks * def.stackLimit; i++) {
                Thing temp = ThingMaker.MakeThing(def);
                temp.stackCount = 1; 

                // Used for query NonFullThings. Using temp, which is destroyed because TryAbsorbStack(), will return value not found.
                Thing temp2 = ThingMaker.MakeThing(def);

                float unitWeight = temp.GetStatValue(StatDefOf.Mass);
                storedThings.Add(temp);

                GenPlace.TryPlaceThing(temp, cell, map, ThingPlaceMode.Direct);

                result = comp.CellStorages.TestCellStorageWeight(cell, unitWeight * (i + 1));
                result &= comp.CellStorages.TestCellStorageStack(cell, Mathf.CeilToInt((i + 1) / (float)def.stackLimit));
                result &= comp.CellStorages.TestSpareSpaceOnNonFull(temp2, cell, (def.stackLimit - (i + 1) % def.stackLimit) % def.stackLimit);
                result &= comp.TestCellStorageCapacity(temp2, map, cell, comp.maxNumberStacks * def.stackLimit - i - 1);
            }

            return result;
        }

        private static bool StoreXUntilStackFull(CompCachedDeepStorage comp, ThingDef def, int stackCount, Map map,
                                                  IntVec3 cell, out List<Thing> storedThings) {
            bool result = false;
            storedThings = new List<Thing>();

            float unitWeight = def.GetStatValueAbstract(StatDefOf.Mass);
            float maxWeight = comp.maxNumberStacks * def.stackLimit * unitWeight;

            // Used for query NonFullThings. Using temp, which is destroyed because TryAbsorbStack(), will return value not found.
            Thing temp2 = ThingMaker.MakeThing(def);

            int iteration = Mathf.CeilToInt((float) comp.maxNumberStacks * def.stackLimit / stackCount);
            for (int i = 0; i < iteration; i++) {
                Thing temp = ThingMaker.MakeThing(def);
                temp.stackCount = stackCount; 


                if (GenPlace.TryPlaceThing(temp, cell, map, ThingPlaceMode.Direct))
                    storedThings.Add(temp);

                float rawWeight = unitWeight * (i + 1) * stackCount;
                float finalWeight = rawWeight > maxWeight ? maxWeight : rawWeight;

                float rawStack = (float)stackCount * (i + 1) / def.stackLimit;
                int finalStack = rawStack > comp.maxNumberStacks ? comp.maxNumberStacks : Mathf.CeilToInt(rawStack);

                int rawCapacity = comp.maxNumberStacks * def.stackLimit - stackCount * (i + 1);
                int finalCapacity = rawCapacity < 0 ? 0 : rawCapacity;

                result = comp.CellStorages.TestCellStorageWeight(cell, finalWeight);
                result &= comp.CellStorages.TestCellStorageStack(cell, finalStack);
                result &= comp.CellStorages.TestSpareSpaceOnNonFull(
                    temp2, cell, (def.stackLimit - stackCount * ((i + 1) % iteration) % def.stackLimit) % def.stackLimit);
                result &= comp.TestCellStorageCapacity(temp2, map, cell, finalCapacity);
            }

            return result;
        }

        private static bool RemoveXUntilEmpty(CompCachedDeepStorage comp, List<Thing> storedThings, int stackCount, Map map, IntVec3 cell) {
            bool result = true;
            comp.CellStorages.TryGetCellStorage(cell, out Deep_Storage_Cell_Storage_Model model);
            ThingDef def = storedThings.First().def;
            Thing blackHole = ThingMaker.MakeThing(storedThings.First().def, GenStuff.DefaultStuffFor(def));

            float unitWeight = def.GetStatValueAbstract(StatDefOf.Mass);
            int totalStacks = model.Count;
            int totalStackCount = storedThings.Sum(t => t.stackCount);
            float totalWeight =  unitWeight * totalStackCount;
            int spareSpaceOnNonFull = model.SpareSpaceOnNonFull(blackHole);

            float rollingWeight = totalWeight;
            int rollingStackCount = 0;

            int remainAmount = stackCount;
            foreach (Thing thing in storedThings) {

                while (thing.stackCount > 0) {

                    if (remainAmount == thing.stackCount) {
                        blackHole.TryAbsorbStack(thing, false);

                        bool hasNonFull = model.NonFullThings.ContainsKey(thing);
                        result &= RunTests(
                            rollingWeight -= remainAmount * unitWeight
                            , totalStacks -= 1
                            , spareSpaceOnNonFull = hasNonFull ? spareSpaceOnNonFull : 0
                            , rollingStackCount += remainAmount);

                        remainAmount = 0;
                    }
                    else if (remainAmount < thing.stackCount) {
                        thing.stackCount -= remainAmount;
                        thing.Map.listerMergeables.Notify_ThingStackChanged(thing);

                        bool sameAsNonFull = false;
                        if (model.NonFullThings.TryGetValue(blackHole, out Thing nonFullThing)) {
                            sameAsNonFull = nonFullThing == thing;
                        }

                        bool absorbed = thing.stackCount <= spareSpaceOnNonFull;
                        result &= RunTests(
                            rollingWeight -= remainAmount * unitWeight
                            , totalStacks = thing.stackCount == 0 ? --totalStacks : totalStacks
                            , spareSpaceOnNonFull = sameAsNonFull
                                ? def.stackLimit - thing.stackCount
                                : absorbed
                                    ? spareSpaceOnNonFull - thing.stackCount
                                    : def.stackLimit - (thing.stackCount - spareSpaceOnNonFull)
                            , rollingStackCount += remainAmount);

                        remainAmount = 0;
                    }
                    else {
                        remainAmount -= thing.stackCount;
                        int temp = thing.stackCount;

                        blackHole.TryAbsorbStack(thing, false);

                        bool hasNonFull = model.NonFullThings.ContainsKey(thing);
                        result &= RunTests(
                            rollingWeight -= temp * unitWeight
                            , totalStacks -= 1
                            , spareSpaceOnNonFull = hasNonFull ? spareSpaceOnNonFull : 0
                            , rollingStackCount += temp);
                    }

                    if (remainAmount <= 0)
                        remainAmount = stackCount;
                }
            }

            bool RunTests(float weight, int stacks, int spareOnNonFull, int capacity) {
                bool innerResult = comp.CellStorages.TestCellStorageWeight(cell, weight);
                innerResult &= comp.CellStorages.TestCellStorageStack(cell, stacks);
                innerResult &= comp.CellStorages.TestSpareSpaceOnNonFull(blackHole, cell, spareOnNonFull);
                innerResult &= comp.TestCellStorageCapacity(blackHole, map, cell, capacity);
                return innerResult;
            }

            return result;
        }
    }
}
