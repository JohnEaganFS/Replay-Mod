using System;
using HarmonyLib;
using UnityEngine;

namespace PuckReplayMod
{
    public class PuckReplayModInit : IPuckPlugin
    {
        private static readonly Harmony Harmony = new Harmony(ReplayModConstants.ModGuid);

        public bool OnEnable()
        {
            try
            {
                ReplayModLog.Info("Enabling...");
                ReplayModController.Create();
                Harmony.PatchAll();
                ReplayModLog.Info("Enabled.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        public bool OnDisable()
        {
            try
            {
                ReplayModLog.Info("Disabling...");
                Harmony.UnpatchSelf();
                ReplayModController.DestroyInstance();
                ReplayModLog.Info("Disabled.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }
    }
}
