using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace LWM.DeepStorage.UnitTest
{
    public class CacheComponent : GameComponent
    {
        private bool _init;

        public CacheComponent(Game game) {
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

                _init = true;
            }
        }

        #endregion
    }
}
