using System;
using Verse;
using Harmony;

namespace LWM.DeepStorage
{
    // Initialize Harmony https://todo
	internal class DeepStorageMod : Mod
    {
        public DeepStorageMod(ModContentPack content) : base(content)
		{
            try
            {
                HarmonyInstance.Create("net.littlewhitemouse.rimworld.deepstorage").PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
                Log.Message("LWM's Deep Storage successfully loaded Harmony patches");
            }
            catch (Exception e)
            {
				Log.Error("LWM's Deep Storage: Oh no! Harmony failure:  caught exception: " + e);
                Log.Warning("  (This may be due to LWM:Deep Storage or earlier Harmony-based load errors.)");
            }
			return;
        }
    }
}
