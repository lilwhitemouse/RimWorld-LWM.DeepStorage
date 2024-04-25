using System;
using HarmonyLib;
namespace LWM.DeepStorage
{
    /// <summary>
    /// Deep Storage problems are usually infrequent and most easily debuged with COPIOUS
    /// logs beforehand.  This completely overwhelmes the logging system, which shuts down,
    /// meaning that when a rare bug does happen, there's no information available. Ugh.
    /// This patch removes the log limit (200 messages are always held in the message 
    /// queue anyway, even if the log limit is 1000, so ....fine by me?)
    /// </summary>
#if DEBUG
    [HarmonyPatch(typeof(Verse.Log), "Notify_MessageReceivedThreadedInternal")]
    public static class Patch_Log
    {
        public static void Prefix(ref int ___messageCount)
        {
            if (___messageCount > 199) ___messageCount = 199;
        }
    }
#endif
}
