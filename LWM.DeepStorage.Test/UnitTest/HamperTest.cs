using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace LWM.DeepStorage.UnitTest
{
    public class HamperTest : DeepStorageTest
    {
        private const string _defName = "LWM_Clothing_Rack";

        private ThingDef _defRack = ThingDef.Named(_defName);

        private ThingDef _defParka = ThingDef.Named("Apparel_Parka");

        private Thing _rack;

        private IntVec3 _position;

        private Map _map;

        private CompCachedDeepStorage _comp;

        #region Overrides of DeepStorageTest

        public override void Setup() {
            Pawn pawn = PawnsFinder.HomeMaps_FreeColonistsSpawned.First();
            _position = pawn.Position + new IntVec3(2, 0, 0);
            _map = pawn.Map;

            _rack = ThingMaker.MakeThing(_defRack, GenStuff.DefaultStuffFor(_defRack));
            ((Building_Storage)_rack).settings.filter.SetAllowAll(null); 
            _comp = _rack.TryGetComp<CompCachedDeepStorage>();

            GenPlace.TryPlaceThing(_rack, _position, pawn.Map, ThingPlaceMode.Direct);
        }

        public override void Run(out bool result) {

            result = Test.StoreXUntilStackFull(_comp, _defParka, _defParka.stackLimit, _map, _position, out List<Thing> storedThings);
            result &= Test.RemoveXUntilEmpty(_comp, storedThings, _defParka.stackLimit, _map, _position);

            _comp.CellStorages.Storages.ForEach(c => c.PrintStates());

            result = true;
        }

        #endregion
    }
}
