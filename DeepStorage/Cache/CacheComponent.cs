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
        public CacheComponent(Game game) {
        }

        #region Overrides of GameComponent

        public override void ExposeData() {
            if (Scribe.mode == LoadSaveMode.LoadingVars) {
                ReplaceWithOriginalComp();
            }
            else if (Scribe.mode == LoadSaveMode.ResolvingCrossRefs) {
                ReplaceWithCachedComp();
            }
        }

        public override void StartedNewGame() {
            base.StartedNewGame();
            ReplaceWithCachedComp();
        }

        #endregion

        private static void ReplaceWithOriginalComp() {
            DefDatabase<ThingDef>.AllDefs
                .ToList()
                .ForEach(def => {
                    CompProperties compProperties =
                        def.comps.FirstOrDefault(comp => comp.compClass == typeof(CompCachedDeepStorage));

                    if (compProperties != null) {
                        compProperties.compClass = typeof(CompDeepStorage);
                    }
                });
        }

        public static void ReplaceWithCachedComp() {
            DefDatabase<ThingDef>.AllDefs
                .ToList()
                .ForEach(def => {
                    CompProperties compProperties =
                        def.comps.FirstOrDefault(comp => comp.compClass == typeof(CompDeepStorage));

                    if (compProperties != null) {
                        compProperties.compClass = typeof(CompCachedDeepStorage);
                        def.tickerType = TickerType.Rare;
                    }
                });
        }
    }
}