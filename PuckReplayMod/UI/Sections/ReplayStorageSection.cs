using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuckReplayMod
{
    internal static class ReplayStorageSection
    {
        public static void Create(ReplayModUiService ui, VisualElement parent)
        {
            parent.Add(ReplayUiTools.CreateSectionTitle("Storage"));
            parent.Add(ReplayUiTools.CreateNote("Set how much disk space replays can use and skip recordings that are too short to keep."));

            ui.StorageUsageLabel = ReplayUiTools.CreateConfigurationLabel(string.Empty);
            ui.StorageUsageLabel.style.marginBottom = 8f;
            parent.Add(ui.StorageUsageLabel);

            parent.Add(ReplayUiTools.CreateIntegerRow("Storage limit (MB)", "Maximum disk space Replay Mod should use for saved replay files. Set to 0 for no automatic storage cap.", ui.Settings.StorageLimitMb, delegate(int value)
            {
                ui.Settings.StorageLimitMb = Mathf.Max(0, value);
                ui.SaveSettings();
                ui.RefreshStorageUsage();
            }));

            parent.Add(ReplayUiTools.CreateIntegerRow("Keep replays at least (sec)", "Replays shorter than this are deleted after saving. Set to 0 to keep every replay, including very short tests.", ui.Settings.MinimumReplayLengthSeconds, delegate(int value)
            {
                ui.Settings.MinimumReplayLengthSeconds = Mathf.Max(0, value);
                ui.SaveSettings();
                ui.RefreshStorageUsage();
            }));

            parent.Add(ReplayUiTools.CreateNote("Set to 0 to keep replays of any length."));

            VisualElement actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.marginTop = 12f;
            parent.Add(actions);

            Button cleanupButton = ReplayUiTools.CreateButton("CLEAN UP OLD REPLAYS", delegate
            {
                ui.Storage.CleanupShortReplays(ui.Settings.MinimumReplayLengthSeconds);
                ui.Storage.CleanupOldReplays(ui.Settings.StorageLimitMb);
                ui.RefreshStorageUsage();
                ui.RefreshLibraryText();
                ui.RefreshReplayList();
            });
            cleanupButton.tooltip = "Applies the current minimum length and storage limit immediately.";
            cleanupButton.style.marginRight = 8f;
            actions.Add(cleanupButton);

            Button refreshButton = ReplayUiTools.CreateButton("REFRESH", ui.RefreshStorageUsage);
            refreshButton.tooltip = "Recalculate replay count and disk usage.";
            actions.Add(refreshButton);

            ui.RefreshStorageUsage();
        }

        public static void RefreshStorageUsage(ReplayModUiService ui)
        {
            if (ui.StorageUsageLabel == null)
            {
                return;
            }

            long totalBytes = ui.GetReplayStorageBytes();
            int replayCount = 0;
            if (Directory.Exists(ui.Storage.ReplaysDirectory))
            {
                FileInfo[] files = new DirectoryInfo(ui.Storage.ReplaysDirectory).GetFiles("*" + ReplayModConstants.ReplayFileExtension);
                replayCount = files.Length;
            }

            string limit = ui.Settings.StorageLimitMb <= 0 ? "unlimited" : ui.Settings.StorageLimitMb + " MB";
            string minimumLength = ui.Settings.MinimumReplayLengthSeconds <= 0 ? "any length" : ui.Settings.MinimumReplayLengthSeconds + " seconds";
            string warning = GetStorageWarning(ui, totalBytes);
            ui.StorageUsageLabel.text =
                "Stored replays: " + replayCount +
                "\nDisk space used: " + ReplayUiTools.FormatBytes(totalBytes) +
                "\nStorage limit: " + limit +
                "\nReplay length kept: " + minimumLength +
                warning +
                "\nReplay folder: " + ui.Storage.ReplaysDirectory;
        }

        private static string GetStorageWarning(ReplayModUiService ui, long totalBytes)
        {
            if (ui.Settings.StorageLimitMb <= 0)
            {
                return string.Empty;
            }

            long limitBytes = (long)ui.Settings.StorageLimitMb * 1024L * 1024L;
            if (limitBytes <= 0L)
            {
                return string.Empty;
            }

            float ratio = totalBytes / (float)limitBytes;
            if (ratio < 0.8f)
            {
                return string.Empty;
            }

            int percent = Mathf.RoundToInt(ratio * 100f);
            return "\nStorage warning: " + percent + "% used. Oldest replays will be cleaned up automatically after saves.";
        }
    }
}
