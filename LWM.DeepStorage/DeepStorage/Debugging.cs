using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;
using Harmony;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler




namespace LWM.DeepStorage
{
    // This doesn't work, BTW:
//    [HarmonyPatch(typeof(PawnComponentsUtility), "CreateInitialComponents")]
    class Debug_Start_Debugger_Jobs {
        static void Postfix(Pawn pawn) {
            pawn.jobs.debugLog = true;
        }
    }

    // TODO: transpile?  faster, surely?
    // TODO: make an option
    //   TODO: Option could include NonHumanlikeOrWildMan OR AnimalOrWildMan
    //   maybe p.RaceProps.Animal or p.RaceProps.HumanLike
    [HarmonyPatch(typeof(StoreUtility), "IsGoodStoreCell")]
    class Patch_IsGoodStoreCell {
        static void Postfix(ref bool __result, IntVec3 c, Map map, Pawn carrier) {
            if (__result == false) return;
            if (carrier == null) return;
            if (carrier.RaceProps.Humanlike) return; // humans can carry wherever
            // non-human here!
            if (LWM.DeepStorage.Utils.CanStoreMoreThanOneThingAt(map, c))
            {
                __result = false;
                return;
            }
            return;
        }
    }


    //  [StaticConstructorOnStartup]
    static class HarXmonyPatchesXXX
    {
        static void HarXmonyPatchesTest()
        {
            return;
            HarmonyInstance harmony;

            harmony = HarmonyInstance.Create("net.littlewhitemouse.rimworld.deep_storageX");


            var all = BindingFlags.Public
                                           | BindingFlags.NonPublic
                                           | BindingFlags.Instance
                                           | BindingFlags.Static
                                           | BindingFlags.GetField
                                           | BindingFlags.SetField
                                           | BindingFlags.GetProperty
                                           | BindingFlags.SetProperty;
            var p = new ParameterModifier[] { };

            Type[] tf = new Type[] {typeof(UnityEngine.Rect),typeof(float).MakeByRefType(),
                typeof(string).MakeByRefType(),typeof(float), typeof(float)};
            //          var tfm = typeof(Verse.Widgets).GetMethod("TextFieldNumeric", all);//, all, null, tf, p);
            var tfm = AccessTools.Method(typeof(Verse.Widgets), "TextFieldNumeric").MakeGenericMethod(typeof(float));
            //              typeof(Verse.Widgets).GetMethod("TextFieldNumeric", all);//, all, null, tf, p);
            if (tfm == null)
            {
                throw (new Exception("TextFieldNumeric capture failure"));
            }

            //HarmonyMethod prefixMethod1 = new HarmonyMethod(typeof(LWM.DeepStorage.HarmonyPatches).GetMethod("TextFieldNumeric"));

            //          harmony.Patch(tfm, prefixMethod1, null); //UGH, TODO


#if false
            Type[] good = new Type[] { typeof(Thing), typeof(ThingOwner), typeof(int), typeof(bool) };
            Type[] bad = new Type[] { typeof(Thing), typeof(ThingOwner), typeof(int), typeof(Thing).MakeByRefType(), typeof(bool) };
            var r1 = typeof(Verse.ThingOwner).GetMethod("TryTransferToContainer", all, null, good, p);
// null//   var r2 = typeof(Verse.ThingOwner).GetMethod("TryTransferToContainer", all, null, bad, p);
            var r2 = typeof(Verse.ThingOwner).GetMethod("TryTransferToContainer", all, null, new Type[] {typeof(Thing), typeof(ThingOwner), typeof(int), typeof(Thing).MakeByRefType(), typeof(bool)}, p);
            if (r1 != null) { Log.Error("r1 is not null");}
            else { Log.Error("r1 is null..."); }
            if (r2 != null) { Log.Error("r2 is not null"); }
            else { Log.Error("r2 is null..."); }
#endif


            //          Type[] x = { typeof(Thing), typeof(ThingOwner), typeof(int), typeof(Thing), typeof(bool) };
            MethodInfo targetMethod = AccessTools.Method(typeof(Verse.ThingOwner),
                                                         "TryTransferToContainer",
                                                         new Type[] { typeof(Thing), typeof(ThingOwner), typeof(int), typeof(Thing).MakeByRefType(), typeof(bool) });
            //      new Type[] { typeof(Thing), typeof(ThingOwner), typeof(int), typeof(bool) });

            //          if (targetMethod == null) { Log.Error("Well, F that"); return; }

            HarmonyMethod prefixMethod = new HarmonyMethod(typeof(LWM.DeepStorage.HarXmonyPatchesXXX).GetMethod("TryTransferToContainer_Prefix"));

            harmony.Patch(targetMethod, prefixMethod, null);
            //          Log.Error("Patched?");

            //          targetMethod = AccessTools.Method(typeof(GenPlace),
            //                                           "TryPlaceDirect")


            return;
        }
        //------------------------------------------------------------//


        static bool TryTransferToContainer_Prefix(Thing item, ThingOwner otherContainer) //???
        {
            Log.Error("Trying to Transfer " + item.ToString() + " to container " + otherContainer.ToString());
            return true;
        }
    }





}