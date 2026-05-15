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

            parent.Add(ReplayUiTools.CreateIntegerRow("Storage limit (MB)", ui.Settings.StorageLimitMb, delegate(int value)
            {
                ui.Settings.StorageLimitMb = Mathf.Max(0, value);
                ui.SaveSettings();
                ui.RefreshStorageUsage();
            }));

            parent.Add(ReplayUiTools.CreateIntegerRow("Keep replays at least (sec)", ui.Settings.MinimumReplayLengthSeconds, delegate(int value)
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
            cleanupButton.style.marginRight = 8f;
            actions.Add(cleanupButton);

            Button refreshButton = ReplayUiTools.CreateButton("REFRESH", ui.RefreshStorageUsage);
            actions.Add(refreshButton);

            ui.RefreshStorageUsage();
        }

        public static void RefreshStorageUsage(ReplayModUiService ui)
        {
            if (ui.StorageUsageLabel == null)
            {
                return;
            }

            long totalBytes = 0L;
            int replayCount = 0;
            if (Directory.Exists(ui.Storage.ReplaysDirectory))
            {
                FileInfo[] files = new DirectoryInfo(ui.Storage.ReplaysDirectory).GetFiles("*" + ReplayModConstants.ReplayFileExtension);
                replayCount = files.Length;
                for (int i = 0; i < files.Length; i++)
                {
                    totalBytes += files[i].Length;
                }
            }

            string limit = ui.Settings.StorageLimitMb <= 0 ? "unlimited" : ui.Settings.StorageLimitMb + " MB";
            string minimumLength = ui.Settings.MinimumReplayLengthSeconds <= 0 ? "any length" : ui.Settings.MinimumReplayLengthSeconds + " seconds";
            ui.StorageUsageLabel.text =
                "Stored replays: " + replayCount +
                "\nDisk space used: " + ReplayUiTools.FormatBytes(totalBytes) +
                "\nStorage limit: " + limit +
                "\nReplay length kept: " + minimumLength +
                "\nReplay folder: " + ui.Storage.ReplaysDirectory;
        }
    }
}
