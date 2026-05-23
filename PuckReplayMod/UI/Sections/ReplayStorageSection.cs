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

            parent.Add(ReplayUiTools.CreateHeader("Recovery"));
            parent.Add(ReplayUiTools.CreateNote("If Puck closes unexpectedly while recording, Replay Mod may be able to rebuild a replay from completed temp chunks."));

            ui.StorageRecoveryLabel = ReplayUiTools.CreateConfigurationLabel(string.Empty);
            ui.StorageRecoveryLabel.style.marginBottom = 8f;
            parent.Add(ui.StorageRecoveryLabel);

            VisualElement recoveryActions = new VisualElement();
            recoveryActions.style.flexDirection = FlexDirection.Row;
            recoveryActions.style.marginTop = 4f;
            recoveryActions.style.marginBottom = 12f;
            parent.Add(recoveryActions);

            Button recoverButton = ReplayUiTools.CreateButton("RECOVER UNFINISHED", delegate
            {
                ReplayRecoveryResult result = ui.Storage.RecoverUnfinishedRecordings();
                ReplayModLog.Info("Replay recovery finished: " + result.RecoveredCount + " recovered, " + result.FailedCount + " failed.");
                ui.RefreshStorageUsage();
                ui.RefreshLibraryText();
                ui.RefreshReplayList();
            });
            recoverButton.tooltip = "Attempts to turn completed temp chunks into normal saved replay files.";
            recoverButton.style.marginRight = 8f;
            recoveryActions.Add(recoverButton);

            Button discardButton = ReplayUiTools.CreateButton("DISCARD UNFINISHED", delegate
            {
                ReplayRecoveryResult result = ui.Storage.DiscardUnfinishedRecordings();
                ReplayModLog.Info("Discarded unfinished replay chunks: " + result.DiscardedCount + " removed, " + result.FailedCount + " failed.");
                ui.RefreshStorageUsage();
            });
            discardButton.tooltip = "Deletes unfinished temp recordings that were left after a crash or force close.";
            recoveryActions.Add(discardButton);

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

            RefreshRecoveryLabel(ui);
        }

        private static void RefreshRecoveryLabel(ReplayModUiService ui)
        {
            if (ui.StorageRecoveryLabel == null)
            {
                return;
            }

            System.Collections.Generic.List<ReplayRecoveryCandidate> candidates = ui.Storage.GetRecoverableRecordings();
            if (candidates.Count == 0)
            {
                ui.StorageRecoveryLabel.text = "Recoverable unfinished recordings: none";
                ui.StorageRecoveryLabel.style.color = ReplayUiTools.MutedTextColor;
                return;
            }

            int totalEvents = 0;
            float longestSeconds = 0f;
            long totalBytes = 0L;
            for (int i = 0; i < candidates.Count; i++)
            {
                totalEvents += candidates[i].EventCount;
                longestSeconds = Mathf.Max(longestSeconds, candidates[i].DurationSeconds);
                totalBytes += candidates[i].SizeBytes;
            }

            ui.StorageRecoveryLabel.text =
                "Recoverable unfinished recordings: " + candidates.Count +
                "\nCompleted chunks found: " + totalEvents + " events" +
                "\nLongest recoverable duration: " + FormatDuration(longestSeconds) +
                "\nTemp space used: " + ReplayUiTools.FormatBytes(totalBytes);
            ui.StorageRecoveryLabel.style.color = Color.white;
        }

        private static string FormatDuration(float seconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.RoundToInt(seconds));
            int minutes = totalSeconds / 60;
            int remainder = totalSeconds % 60;
            return minutes + ":" + remainder.ToString("00");
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
