using System.Collections.Generic;
using UnityEngine.UIElements;

namespace PuckReplayMod
{
    internal static class ReplayOverlaySettingsSection
    {
        public static void Create(ReplayModUiService ui, VisualElement parent)
        {
            parent.Add(ReplayUiTools.CreateSectionTitle("On-screen Display"));
            parent.Add(ReplayUiTools.CreateNote("Choose what Replay Mod shows on screen during recording and playback."));

            parent.Add(ReplayUiTools.CreateHeader("Status and Time"));

            parent.Add(ReplayUiTools.CreateToggleRow("Show status badge", ui.Settings.ShowStatusIndicator, delegate(bool value)
            {
                ui.Settings.ShowStatusIndicator = value;
                ui.SaveSettings();
                ui.RefreshStatusIndicator();
            }));

            parent.Add(ReplayUiTools.CreateDropdownRow("Status badge", FormatIndicatorVisibility(ui.Settings.StatusIndicatorVisibility), GetIndicatorVisibilityChoices(), delegate(string value)
            {
                ui.Settings.StatusIndicatorVisibility = ParseIndicatorVisibility(value);
                ui.SaveSettings();
                ui.RefreshStatusIndicator();
            }));

            parent.Add(ReplayUiTools.CreateDropdownRow("Status badge position", FormatOverlayPosition(ui.Settings.StatusIndicatorPosition), GetOverlayPositionChoices(), delegate(string value)
            {
                ReplayOverlayPosition parsed = ParseOverlayPosition(value);
                ui.Settings.StatusIndicatorPosition = parsed;
                ui.SaveSettings();
                ui.ApplyOverlayPosition(ui.StatusLabel, parsed, 76f);
            }));

            parent.Add(ReplayUiTools.CreateToggleRow("Show replay time", ui.Settings.ShowPlaybackTimeline, delegate(bool value)
            {
                ui.Settings.ShowPlaybackTimeline = value;
                ui.SaveSettings();
                ui.RefreshTimelineIndicator();
            }));

            parent.Add(ReplayUiTools.CreateDropdownRow("Replay time position", FormatOverlayPosition(ui.Settings.PlaybackTimelinePosition), GetOverlayPositionChoices(), delegate(string value)
            {
                ReplayOverlayPosition parsed = ParseOverlayPosition(value);
                ui.Settings.PlaybackTimelinePosition = parsed;
                ui.SaveSettings();
                ui.ApplyOverlayPosition(ui.TimelineLabel, parsed, 110f);
            }));

            parent.Add(ReplayUiTools.CreateSeparator());
            parent.Add(ReplayUiTools.CreateHeader("Chat"));

            parent.Add(ReplayUiTools.CreateToggleRow("Show recorded chat", ui.Settings.ShowReplayChat, delegate(bool value)
            {
                ui.Settings.ShowReplayChat = value;
                ui.SaveSettings();
            }));

            parent.Add(ReplayUiTools.CreateToggleRow("Clear chat before playback", ui.Settings.ClearChatOnPlaybackStart, delegate(bool value)
            {
                ui.Settings.ClearChatOnPlaybackStart = value;
                ui.SaveSettings();
            }));

            parent.Add(ReplayUiTools.CreateSeparator());
            parent.Add(ReplayUiTools.CreateHeader("Playback Smoothing"));

            parent.Add(ReplayUiTools.CreateToggleRow("Smooth slow motion playback", "Interpolates replay bodies, sticks, and pucks between recorded ticks while playback speed is below 1x. Paused, tick-by-tick, and seeked playback still uses exact recorded ticks.", ui.Settings.EnableSlowMotionInterpolation, delegate(bool value)
            {
                ui.Settings.EnableSlowMotionInterpolation = value;
                ui.SaveSettings();
            }));

            parent.Add(ReplayUiTools.CreateSeparator());
            parent.Add(ReplayUiTools.CreateHeader("Playback Camera"));

            parent.Add(ReplayUiTools.CreateFloatSliderRow("3rd person camera distance", "How far the replay camera sits behind the selected player in 3rd person POV.", ui.Settings.PlaybackThirdPersonCameraDistance, 1.5f, 12f, delegate(float value)
            {
                ui.Settings.PlaybackThirdPersonCameraDistance = UnityEngine.Mathf.Clamp(value, 1.5f, 12f);
                ui.SaveSettings();
            }));

            parent.Add(ReplayUiTools.CreateFloatSliderRow("1st person FOV", "Camera field of view used only while watching a replay in 1st person POV.", ui.Settings.PlaybackFirstPersonFov, 60f, 120f, delegate(float value)
            {
                ui.Settings.PlaybackFirstPersonFov = UnityEngine.Mathf.Clamp(value, 60f, 120f);
                ui.SaveSettings();
            }));

            parent.Add(ReplayUiTools.CreateToggleRow("Smooth 1st person POV", "Softens recorded look direction changes during active first-person replay playback. Paused and tick-by-tick playback still snaps exactly to the recorded tick.", ui.Settings.EnableFirstPersonCameraSmoothing, delegate(bool value)
            {
                ui.Settings.EnableFirstPersonCameraSmoothing = value;
                ui.SaveSettings();
            }));

            parent.Add(ReplayUiTools.CreateFloatSliderRow("1st person smoothing speed", "Higher values follow the recorded camera more tightly. Lower values feel smoother but add more visual lag.", ui.Settings.FirstPersonCameraSmoothingSpeed, 1f, 60f, delegate(float value)
            {
                ui.Settings.FirstPersonCameraSmoothingSpeed = UnityEngine.Mathf.Clamp(value, 1f, 60f);
                ui.SaveSettings();
            }));
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
