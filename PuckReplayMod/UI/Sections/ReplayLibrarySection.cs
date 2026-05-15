using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuckReplayMod
{
    internal static class ReplayLibrarySection
    {
        public static void Create(ReplayModUiService ui, VisualElement parent)
        {
            parent.Add(ReplayUiTools.CreateSectionTitle("Replay Library"));
            parent.Add(ReplayUiTools.CreateNote("Watch a recent recording or check that new replays are being saved correctly."));

            ui.StorageLabel = ReplayUiTools.CreateConfigurationLabel(string.Empty);
            ui.StorageLabel.style.marginBottom = 6f;
            parent.Add(ui.StorageLabel);

            ui.PlaybackLabel = ReplayUiTools.CreateConfigurationLabel(string.Empty);
            ui.PlaybackLabel.style.marginBottom = 8f;
            parent.Add(ui.PlaybackLabel);

            VisualElement actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.marginTop = 6f;
            actions.style.marginBottom = 10f;
            parent.Add(actions);

            Button refreshButton = ReplayUiTools.CreateButton("REFRESH", delegate
            {
                ui.RefreshLibraryText();
                ui.RefreshReplayList();
                ui.RefreshPlaybackStatus();
            });
            refreshButton.style.marginRight = 8f;
            actions.Add(refreshButton);

            Button markerButton = ReplayUiTools.CreateButton("ADD MARKER", delegate
            {
                ui.Recorder.AddMarker();
            });
            markerButton.SetEnabled(ui.Recorder.IsRecording);
            actions.Add(markerButton);

            ui.ReplayList = new VisualElement
            {
                name = "PuckReplayModReplayList"
            };
            ui.ReplayList.style.marginTop = 2f;
            parent.Add(ui.ReplayList);

            ui.RefreshLibraryText();
            ui.RefreshPlaybackStatus();
            ui.RefreshReplayList();
        }

        public static void RefreshReplayList(ReplayModUiService ui)
        {
            if (ui.ReplayList == null)
            {
                return;
            }

            ui.ReplayList.Clear();
            List<ReplayFileSummary> replays = ui.Reader.GetRecentReplays(ui.Storage.ReplaysDirectory, ui.Storage.SummariesDirectory, 12);
            if (replays.Count == 0)
            {
                Label emptyLabel = ReplayUiTools.CreateConfigurationLabel("No saved replays yet. Join a server with recording enabled, then leave to save your first replay.");
                emptyLabel.style.color = ReplayUiTools.MutedTextColor;
                ui.ReplayList.Add(emptyLabel);
                return;
            }

            foreach (ReplayFileSummary replay in replays)
            {
                ui.ReplayList.Add(CreateReplayRow(ui, replay));
            }
        }

        private static VisualElement CreateReplayRow(ReplayModUiService ui, ReplayFileSummary replay)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.minHeight = 54f;
            row.style.marginTop = 4f;
            row.style.paddingLeft = 10f;
            row.style.paddingRight = 10f;
            row.style.paddingTop = 6f;
            row.style.paddingBottom = 6f;
            row.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 0.95f);

            VisualElement details = new VisualElement();
            details.style.flexDirection = FlexDirection.Column;
            details.style.flexGrow = 1f;
            details.style.marginRight = 10f;

            Label title = ReplayUiTools.CreateConfigurationLabel(string.IsNullOrEmpty(replay.ServerName) ? "Unknown Server" : replay.ServerName);
            title.style.color = Color.white;
            title.style.fontSize = 15f;
            details.Add(title);

            string date = replay.LastWriteUtc.ToLocalTime().ToString("MM/dd/yyyy HH:mm");
            string duration = replay.IsMetadataComplete ? FormatDuration(replay.DurationSeconds) : "Indexing...";
            Label meta = ReplayUiTools.CreateConfigurationLabel(date + "    " + duration + "    " + ReplayUiTools.FormatBytes(replay.SizeBytes));
            meta.style.fontSize = 12f;
            meta.style.color = ReplayUiTools.MutedTextColor;
            details.Add(meta);
            row.Add(details);

            ReplayFileSummary selectedReplay = replay;
            Button playButton = ReplayUiTools.CreateButton("PLAY", delegate
            {
                ui.PlayReplay(selectedReplay.FilePath);
            });
            playButton.style.width = 92f;
            playButton.style.minWidth = 92f;
            row.Add(playButton);

            return row;
        }

        private static string FormatDuration(float seconds)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(Math.Max(0f, seconds));
            if (timeSpan.TotalHours >= 1.0)
            {
                return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)timeSpan.TotalHours, timeSpan.Minutes, timeSpan.Seconds);
            }

            return string.Format("{0:D2}:{1:D2}", timeSpan.Minutes, timeSpan.Seconds);
        }
    }
}
