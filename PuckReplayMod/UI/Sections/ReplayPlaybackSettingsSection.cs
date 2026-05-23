using UnityEngine.UIElements;

namespace PuckReplayMod
{
    internal static class ReplayPlaybackSettingsSection
    {
        public static void Create(ReplayModUiService ui, VisualElement parent)
        {
            parent.Add(ReplayUiTools.CreateSectionTitle("Playback"));
            parent.Add(ReplayUiTools.CreateNote("Tune how saved replays behave while you are watching them."));

            parent.Add(ReplayUiTools.CreateHeader("Chat"));

            parent.Add(ReplayUiTools.CreateToggleRow("Show recorded chat", "Replays chat messages captured in the replay file into the normal in-game chat UI during playback.", ui.Settings.ShowReplayChat, delegate(bool value)
            {
                ui.Settings.ShowReplayChat = value;
                ui.SaveSettings();
            }));

            parent.Add(ReplayUiTools.CreateToggleRow("Clear chat before playback", "Clears existing chat when playback starts so old live-server messages do not mix with recorded replay chat.", ui.Settings.ClearChatOnPlaybackStart, delegate(bool value)
            {
                ui.Settings.ClearChatOnPlaybackStart = value;
                ui.SaveSettings();
            }));

            parent.Add(ReplayUiTools.CreateSeparator());
            parent.Add(ReplayUiTools.CreateHeader("Motion"));

            parent.Add(ReplayUiTools.CreateToggleRow("Smooth slow motion playback", "Interpolates replay bodies, sticks, and pucks between recorded ticks while playback speed is below 1x. Paused, tick-by-tick, and seeked playback still uses exact recorded ticks.", ui.Settings.EnableSlowMotionInterpolation, delegate(bool value)
            {
                ui.Settings.EnableSlowMotionInterpolation = value;
                ui.SaveSettings();
            }));

            parent.Add(ReplayUiTools.CreateSeparator());
            parent.Add(ReplayUiTools.CreateHeader("Camera"));

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

            parent.Add(ReplayUiTools.CreateSeparator());
            parent.Add(ReplayUiTools.CreateHeader("Capture Mode"));
            parent.Add(ReplayUiTools.CreateNote("Capture Mode gives you a clean replay view for highlights and montage recording. It only hides UI while replay playback is active."));
            ReplayCaptureSettingsSection.AddCaptureSettings(ui, parent);
        }
    }
}
