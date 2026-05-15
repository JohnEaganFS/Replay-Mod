using UnityEngine;

namespace PuckReplayMod
{
    public static class ReplayModLog
    {
        public static void Info(string message)
        {
            Debug.Log("[" + ReplayModConstants.ModName + "] " + message);
        }

        public static void Warning(string message)
        {
            Debug.LogWarning("[" + ReplayModConstants.ModName + "] " + message);
        }

        public static void Error(string message)
        {
            Debug.LogError("[" + ReplayModConstants.ModName + "] " + message);
        }
    }
}
