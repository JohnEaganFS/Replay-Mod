using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuckReplayMod
{
    internal static class ReplayRecordingSettingsSection
    {
        public static void Create(ReplayModUiService ui, VisualElement parent)
        {
            parent.Add(ReplayUiTools.CreateSectionTitle("Recording"));
            parent.Add(ReplayUiTools.CreateNote("Control when replay files are created. The default settings are a good fit for most players."));

            parent.Add(ReplayUiTools.CreateToggleRow("Record games automatically", ui.Settings.AutoRecord, delegate(bool value)
            {
                ui.Settings.AutoRecord = value;
                if (!value && ui.Recorder.IsRecording)
                {
                    ui.Recorder.StopRecording(true, "auto-record disabled");
                }

                ui.SaveSettings();
            }));

            parent.Add(ReplayUiTools.CreateDropdownRow("Replay smoothness", FormatCaptureRate(ui.Settings.CaptureTickRate), GetCaptureRateChoices(), delegate(string value)
            {
                int parsed = ParseCaptureRate(value);
                if (parsed <= 0)
                {
                    return;
                }

                if (ui.Recorder.IsRecording)
                {
                    ReplayModLog.Warning("Capture rate changes apply after the current recording stops.");
                    return;
                }

                ui.Settings.CaptureTickRate = Mathf.Clamp(parsed, 5, 120);
                ui.SaveSettings();
            }));

            parent.Add(ReplayUiTools.CreateDropdownRow("Marker shortcut", ui.Settings.MarkerKey.ToString(), GetFunctionKeyChoices(), delegate(string value)
            {
                KeyCode parsed;
                if (Enum.TryParse(value, true, out parsed))
                {
                    ui.Settings.MarkerKey = parsed;
                    ui.SaveSettings();
                }
            }));

            parent.Add(ReplayUiTools.CreateSeparator());

            parent.Add(ReplayUiTools.CreateNote("Markers let you flag a moment while recording so it is easier to find later."));

            Button markerButton = ReplayUiTools.CreateButton("ADD MARKER", delegate
            {
                ui.Recorder.AddMarker();
            });
            markerButton.SetEnabled(ui.Recorder.IsRecording);
            markerButton.style.width = 220f;
            parent.Add(markerButton);
        }

        private static List<string> GetCaptureRateChoices()
        {
            return new List<string>
            {
                "Low (smaller files)",
                "Standard",
                "High (smoother)"
            };
        }

        internal static string FormatCaptureRate(int tickRate)
        {
            if (tickRate <= 15)
            {
                return "Low (smaller files)";
            }

            if (tickRate >= 60)
            {
                return "High (smoother)";
            }

            return "Standard";
        }

        private static int ParseCaptureRate(string value)
        {
            switch (value)
            {
                case "Low (smaller files)":
                    return 15;
                case "High (smoother)":
                    return 60;
                default:
                    return 30;
            }
        }

        private static List<string> GetFunctionKeyChoices()
        {
            return new List<string>
            {
                "F1",
                "F2",
                "F3",
                "F4",
                "F5",
                "F6",
                "F7",
                "F8",
                "F9",
                "F10",
                "F11",
                "F12"
            };
        }
    }
}
