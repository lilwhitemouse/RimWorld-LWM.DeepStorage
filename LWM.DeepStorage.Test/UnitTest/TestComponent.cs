using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace LWM.DeepStorage.UnitTest
{
    public class TestComponent : GameComponent
    {
        private DeepStorageTest _root = new TestRoot();

        private bool _init;

        public TestComponent(Game game) {
        }

        #region Overrides of GameComponent

        public override void GameComponentTick() {
            if (!_init)
            {
                DefDatabase<ThingDef>.AllDefs
                    .ToList()
                    .ForEach(def =>
                    {
                        CompProperties compProperties =
                            def.comps.FirstOrDefault(comp => comp.compClass == typeof(CompDeepStorage));

                        if (compProperties != null)
                        {
                            compProperties.compClass = typeof(CompCachedDeepStorage);
                        }
                    });

                _root.Start();
                _init = true;
            }
        }

        #endregion
    }
}
