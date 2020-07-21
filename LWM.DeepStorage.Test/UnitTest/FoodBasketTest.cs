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

            // Store Herbal Medicine one by one until the basket is full.
            result = Test.StoreXUntilStackFull(_comp, _defHerb, 1, _map, _position, out List<Thing> storedThings1);

            // Store Herbal Medicine x 25 to the allowed maximum number of stacks for food basket in a cell.
            result &= Test.StoreXUntilStackFull(
                _comp, _defHerb, _defHerb.stackLimit, _map, _position + new IntVec3(1, 0, 0), out List<Thing> storedThings2);

            // Take out one full stack of Herbal Medicine at a time.
            result &= Test.RemoveXUntilEmpty(_comp, storedThings1, _defHerb.stackLimit, _map, _position);

            // Take out one Herbal Medicine at a time.
            result &= Test.RemoveXUntilEmpty(_comp, storedThings2, 1, _map, _position + new IntVec3(1, 0, 0));

            result &= Test.StoreXUntilStackFull(_comp, _defHerb, 23, _map, _position, out storedThings1);
            result &= Test.RemoveXUntilEmpty(_comp, storedThings1, 23, _map, _position);

            result &= Test.TestSelfCorrection(_comp, _position, _map, _defHerb, 1, 4);
            result &= Test.TestSelfCorrection(_comp, _position, _map, _defHerb, 20, 4);
            result &= Test.TestSelfCorrection(_comp, _position, _map, _defHerb, _defHerb.stackLimit, 4);

            _comp.CellStorages.Storages.ForEach(c => c.PrintStates());
        }

        #endregion
     }
}
