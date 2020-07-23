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
        private bool _run = false;

        public TestComponent(Game game) {
        }

        #region Overrides of GameComponent

        public override void GameComponentTick() {
            if (!_run) {
                TestRoot root = new TestRoot();
                root.Start();
                _run = true;
            }
        }

        #endregion
    }
}
