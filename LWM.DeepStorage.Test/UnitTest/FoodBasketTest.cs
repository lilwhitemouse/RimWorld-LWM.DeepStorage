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

            Thing foodBasket = ThingMaker.MakeThing(_defBasket, GenStuff.DefaultStuffFor(_defBasket));
            ((Building_Storage)foodBasket).settings.filter.SetAllowAll(null); 
            _comp = foodBasket.TryGetComp<CompCachedDeepStorage>();

            GenPlace.TryPlaceThing(foodBasket, pawn.Position, pawn.Map, ThingPlaceMode.Direct);
        }

        public override void Run(out bool result) {
            Thing herb = ThingMaker.MakeThing(_defHerb);

            // MedicineHerbal has a stackLimit of 25
            herb.stackCount = 25;
            float herbWeight = herb.GetStatValue(StatDefOf.Mass) * herb.stackCount;
            int herbCount = Mathf.CeilToInt((float)herb.stackCount / herb.def.stackLimit);

            GenPlace.TryPlaceThing(herb, _position, _map, ThingPlaceMode.Direct);
            result = _comp.CellStorages.TestCellStorageWeight(_position, herbWeight);
            result &= _comp.CellStorages.TestCellStorageCount(_position, herbCount);


            Thing herb2 = ThingMaker.MakeThing(_defHerb);
            herb2.stackCount = 25;
            herbWeight *= 2;
            herbCount++;

            GenPlace.TryPlaceThing(herb2, _position, _map, ThingPlaceMode.Direct);
            result = _comp.CellStorages.TestCellStorageWeight(_position, herbWeight);
            result &= _comp.CellStorages.TestCellStorageCount(_position, herbCount);

            for (int i = 0; i < 10; i++)
            {
                Thing temp = ThingMaker.MakeThing(_defHerb);
                temp.stackCount = _defHerb.stackLimit;
                GenPlace.TryPlaceThing(temp, _position, _map, ThingPlaceMode.Near);
            }
        }

        #endregion
    }
}
