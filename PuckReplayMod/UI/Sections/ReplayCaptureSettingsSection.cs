using UnityEngine.UIElements;

namespace PuckReplayMod
{
    internal static class ReplayCaptureSettingsSection
    {
        public static void Create(ReplayModUiService ui, VisualElement parent)
        {
            parent.Add(ReplayUiTools.CreateSectionTitle("Capture Mode"));
            parent.Add(ReplayUiTools.CreateNote("Capture Mode gives you a clean replay view for highlights and montage recording. It only hides UI while replay playback is active."));

            parent.Add(ReplayUiTools.CreateHeader("Quick Toggle"));

            parent.Add(ReplayUiTools.CreateToggleRow(
                "Capture Mode active",
                "Turns the clean-screen mode on or off for the current replay session.",
                ui.CaptureModeActive,
                delegate(bool value)
            {
                ui.SetCaptureModeActive(value);
            }));

            parent.Add(ReplayUiTools.CreateSeparator());
            parent.Add(ReplayUiTools.CreateHeader("Hidden While Active"));

            parent.Add(ReplayUiTools.CreateToggleRow(
                "Playback controls",
                "Hides the replay timeline, pause, seek, speed, and POV controls.",
                ui.Settings.CaptureModeHidePlaybackControls,
                delegate(bool value)
            {
                ui.Settings.CaptureModeHidePlaybackControls = value;
                ui.SaveSettings();
                ui.RefreshCaptureModeVisibility();
            }));

            parent.Add(ReplayUiTools.CreateToggleRow(
                "Replay status and time",
                "Hides Replay Mod's status badge and replay time overlay.",
                ui.Settings.CaptureModeHideReplayOverlays,
                delegate(bool value)
            {
                ui.Settings.CaptureModeHideReplayOverlays = value;
                ui.SaveSettings();
                ui.RefreshCaptureModeVisibility();
            }));

            parent.Add(ReplayUiTools.CreateToggleRow(
                "Scoreboard and speed HUD",
                "Hides the vanilla score/time display, speed readout, and scoreboard panel.",
                ui.Settings.CaptureModeHideGameHud,
                delegate(bool value)
            {
                ui.Settings.CaptureModeHideGameHud = value;
                ui.SaveSettings();
                ui.RefreshCaptureModeVisibility();
            }));

            parent.Add(ReplayUiTools.CreateToggleRow(
                "Chat",
                "Hides recorded chat while Capture Mode is active.",
                ui.Settings.CaptureModeHideChat,
                delegate(bool value)
            {
                ui.Settings.CaptureModeHideChat = value;
                ui.SaveSettings();
                ui.RefreshCaptureModeVisibility();
            }));

            parent.Add(ReplayUiTools.CreateToggleRow(
                "Minimap",
                "Hides the vanilla minimap while Capture Mode is active.",
                ui.Settings.CaptureModeHideMinimap,
                delegate(bool value)
            {
                ui.Settings.CaptureModeHideMinimap = value;
                ui.SaveSettings();
                ui.RefreshCaptureModeVisibility();
            }));

            parent.Add(ReplayUiTools.CreateToggleRow(
                "Player names and announcements",
                "Hides floating player names and large vanilla announcements such as goals.",
                ui.Settings.CaptureModeHidePlayerNames,
                delegate(bool value)
            {
                ui.Settings.CaptureModeHidePlayerNames = value;
                ui.SaveSettings();
                ui.RefreshCaptureModeVisibility();
            }));
        }
    }
}
