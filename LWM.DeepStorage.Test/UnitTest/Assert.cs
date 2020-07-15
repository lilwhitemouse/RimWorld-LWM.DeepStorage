using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace LWM.DeepStorage.UnitTest
{
    public class Assert
    {
        public static bool AreEqaul<T>(T a, T b, string nameA, string nameB) {
            if (!a.Equals(b))
            {
                Log.Error($"{nameA} is not equal to {nameB}\n{nameA}: {a}\n{nameB}: {b}");
                return false;
            }

            return true;
        }
    }
}
