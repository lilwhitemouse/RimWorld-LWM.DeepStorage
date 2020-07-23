using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace LWM.DeepStorage.UnitTest
{
    public class TestRoot : DeepStorageTest
    {
        #region Overrides of DeepStorageTest

        public override void Setup() {
            Tests.Add(new FoodBasketTest());
            Tests.Add(new HamperTest());
        }

        public override void Cleanup() {
            StringBuilder stringBuilder = new StringBuilder();
            Report(this, ref stringBuilder);
            Log.Warning(stringBuilder.ToString());
        }

        #endregion
    }
}
