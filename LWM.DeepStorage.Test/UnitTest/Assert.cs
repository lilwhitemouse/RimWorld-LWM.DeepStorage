using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace LWM.DeepStorage.UnitTest
{
    public class Assert
    {
        public static bool AreEqaul<T>(T actual, T expect, string nameA, string nameB) {
            if (typeof(T) == typeof(float)) {
                if (Mathf.Abs((float) Convert.ChangeType(actual, typeof(float)) - (float) Convert.ChangeType(expect, typeof(float))) > 0.01f) {
                    Log.Error($"{nameA} is not equal to {nameB}\n{nameA}: {actual} -- {nameB}: {expect}");
                    return false;
                }

                return true;
            }

            if (!actual.Equals(expect)) {
                Log.Error($"{nameA} is not equal to {nameB}\n{nameA}: {actual} -- {nameB}: {expect}");
                return false;
            }

            return true;
        }
    }
}
