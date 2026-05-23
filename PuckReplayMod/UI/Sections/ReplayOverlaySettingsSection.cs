using System.Collections.Generic;
using UnityEngine.UIElements;

namespace PuckReplayMod
{
    internal static class ReplayOverlaySettingsSection
    {
        public static void Create(ReplayModUiService ui, VisualElement parent)
        {
            parent.Add(ReplayUiTools.CreateSectionTitle("Display / Interface"));
            parent.Add(ReplayUiTools.CreateNote("Choose what Replay Mod shows on screen and adjust the Replay Manager window."));

            parent.Add(ReplayUiTools.CreateHeader("Status and Time"));

            parent.Add(ReplayUiTools.CreateToggleRow("Show status badge", "Shows Replay Mod's small recording/playback status badge when the visibility rule below allows it.", ui.Settings.ShowStatusIndicator, delegate(bool value)
            {
                ui.Settings.ShowStatusIndicator = value;
                ui.SaveSettings();
                ui.RefreshStatusIndicator();
            }));

            parent.Add(ReplayUiTools.CreateDropdownRow("Status badge", "Controls when the status badge is allowed to appear. The hotkey can still temporarily hide or restore it.", FormatIndicatorVisibility(ui.Settings.StatusIndicatorVisibility), GetIndicatorVisibilityChoices(), delegate(string value)
            {
                ui.Settings.StatusIndicatorVisibility = ParseIndicatorVisibility(value);
                ui.SaveSettings();
                ui.RefreshStatusIndicator();
            }));

            parent.Add(ReplayUiTools.CreateDropdownRow("Status badge position", "Moves the status badge to a preset screen corner so it can avoid vanilla UI or other mods.", FormatOverlayPosition(ui.Settings.StatusIndicatorPosition), GetOverlayPositionChoices(), delegate(string value)
            {
                ReplayOverlayPosition parsed = ParseOverlayPosition(value);
                ui.Settings.StatusIndicatorPosition = parsed;
                ui.SaveSettings();
                ui.ApplyOverlayPosition(ui.StatusLabel, parsed, 76f);
            }));

            parent.Add(ReplayUiTools.CreateToggleRow("Show replay time", "Shows the small replay time readout while watching a replay, if Capture Mode has not hidden it.", ui.Settings.ShowPlaybackTimeline, delegate(bool value)
            {
                ui.Settings.ShowPlaybackTimeline = value;
                ui.SaveSettings();
                ui.RefreshTimelineIndicator();
            }));

            parent.Add(ReplayUiTools.CreateDropdownRow("Replay time position", "Moves the replay time readout to a preset screen corner.", FormatOverlayPosition(ui.Settings.PlaybackTimelinePosition), GetOverlayPositionChoices(), delegate(string value)
            {
                ReplayOverlayPosition parsed = ParseOverlayPosition(value);
                ui.Settings.PlaybackTimelinePosition = parsed;
                ui.SaveSettings();
                ui.ApplyOverlayPosition(ui.TimelineLabel, parsed, 110f);
            }));

            parent.Add(ReplayUiTools.CreateSeparator());
            parent.Add(ReplayUiTools.CreateHeader("Replay Manager Window"));
            ReplayInterfaceSettingsSection.AddInterfaceSettings(ui, parent);
        }

        private static List<string> GetIndicatorVisibilityChoices()
        {
            return new List<string>
            {
                "Always",
                "Recording and playback",
                "Recording only",
                "With scoreboard",
                "Hidden"
            };
        }

        private static List<string> GetOverlayPositionChoices()
        {
            return new List<string>
            {
                "Top right",
                "Top left",
                "Bottom right",
                "Bottom left"
            };
        }

        private static string FormatIndicatorVisibility(ReplayIndicatorVisibility visibility)
        {
            switch (visibility)
            {
                case ReplayIndicatorVisibility.RecordingAndPlayback:
                    return "Recording and playback";
                case ReplayIndicatorVisibility.RecordingOnly:
                    return "Recording only";
                case ReplayIndicatorVisibility.ScoreboardOnly:
                    return "With scoreboard";
                case ReplayIndicatorVisibility.Hidden:
                    return "Hidden";
                default:
                    return "Always";
            }
        }

        private static ReplayIndicatorVisibility ParseIndicatorVisibility(string value)
        {
            switch (value)
            {
                case "Recording and playback":
                    return ReplayIndicatorVisibility.RecordingAndPlayback;
                case "Recording only":
                    return ReplayIndicatorVisibility.RecordingOnly;
                case "With scoreboard":
                    return ReplayIndicatorVisibility.ScoreboardOnly;
                case "Hidden":
                    return ReplayIndicatorVisibility.Hidden;
                default:
                    return ReplayIndicatorVisibility.Always;
            }
        }

        private static string FormatOverlayPosition(ReplayOverlayPosition position)
        {
            switch (position)
            {
                case ReplayOverlayPosition.TopLeft:
                    return "Top left";
                case ReplayOverlayPosition.BottomRight:
                    return "Bottom right";
                case ReplayOverlayPosition.BottomLeft:
                    return "Bottom left";
                default:
                    return "Top right";
            }
        }

        private static ReplayOverlayPosition ParseOverlayPosition(string value)
        {
            switch (value)
            {
                case "Top left":
                    return ReplayOverlayPosition.TopLeft;
                case "Bottom right":
                    return ReplayOverlayPosition.BottomRight;
                case "Bottom left":
                    return ReplayOverlayPosition.BottomLeft;
                default:
                    return ReplayOverlayPosition.TopRight;
            }
        }
    }
}
