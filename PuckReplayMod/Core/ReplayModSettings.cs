using System;
using UnityEngine;

namespace PuckReplayMod
{
    public class ReplayModSettings
    {
        private const string Prefix = "PuckReplayMod.";

        public bool AutoRecord = true;
        public int CaptureTickRate = 30;
        public int StorageLimitMb = 2048;
        public KeyCode MarkerKey = KeyCode.F4;
        public bool EnableLegacyImport = true;
        public bool EnableDebugProfiling = false;

        public static ReplayModSettings Load()
        {
            ReplayModSettings settings = new ReplayModSettings
            {
                AutoRecord = PlayerPrefs.GetInt(Prefix + "AutoRecord", 1) == 1,
                CaptureTickRate = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "CaptureTickRate", 30), 5, 120),
                StorageLimitMb = Mathf.Max(0, PlayerPrefs.GetInt(Prefix + "StorageLimitMb", 2048)),
                EnableLegacyImport = PlayerPrefs.GetInt(Prefix + "EnableLegacyImport", 1) == 1,
                EnableDebugProfiling = PlayerPrefs.GetInt(Prefix + "EnableDebugProfiling", 0) == 1
            };

            string markerKey = PlayerPrefs.GetString(Prefix + "MarkerKey", KeyCode.F4.ToString());
            KeyCode parsed;
            if (Enum.TryParse(markerKey, true, out parsed))
            {
                settings.MarkerKey = parsed;
            }

            return settings;
        }

        public void Save()
        {
            PlayerPrefs.SetInt(Prefix + "AutoRecord", this.AutoRecord ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "CaptureTickRate", Mathf.Clamp(this.CaptureTickRate, 5, 120));
            PlayerPrefs.SetInt(Prefix + "StorageLimitMb", Mathf.Max(0, this.StorageLimitMb));
            PlayerPrefs.SetString(Prefix + "MarkerKey", this.MarkerKey.ToString());
            PlayerPrefs.SetInt(Prefix + "EnableLegacyImport", this.EnableLegacyImport ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "EnableDebugProfiling", this.EnableDebugProfiling ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
