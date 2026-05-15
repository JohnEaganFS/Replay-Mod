using UnityEngine.UIElements;

namespace PuckReplayMod
{
    internal static class ReplayPlaybackSection
    {
        public static void Create(ReplayModUiService ui, VisualElement parent)
        {
            parent.Add(ReplayUiTools.CreateSectionTitle("Playback"));
            parent.Add(ReplayUiTools.CreateNote("Choose how replays look while you are watching them."));

            ui.PlaybackLabel = ReplayUiTools.CreateConfigurationLabel(string.Empty);
            parent.Add(ui.PlaybackLabel);

            Button stopPlaybackButton = ReplayUiTools.CreateButton("STOP WATCHING", delegate
            {
                ui.Playback.Close();
                ui.RefreshPlaybackStatus();
            });
            stopPlaybackButton.SetEnabled(ui.Playback.IsPlaying);
            stopPlaybackButton.style.width = 220f;
            stopPlaybackButton.style.marginTop = 12f;
            parent.Add(stopPlaybackButton);

            parent.Add(ReplayUiTools.CreateSeparator());
            parent.Add(ReplayUiTools.CreateHeader("On-screen replay info"));
            parent.Add(ReplayUiTools.CreateToggleRow("Show time while watching", ui.Settings.ShowPlaybackTimeline, delegate(bool value)
            {
                ui.Settings.ShowPlaybackTimeline = value;
                ui.SaveSettings();
                ui.RefreshTimelineIndicator();
            }));
            parent.Add(ReplayUiTools.CreateToggleRow("Show recorded chat", ui.Settings.ShowReplayChat, delegate(bool value)
            {
                ui.Settings.ShowReplayChat = value;
                ui.SaveSettings();
            }));
            parent.Add(ReplayUiTools.CreateToggleRow("Clear old chat first", ui.Settings.ClearChatOnPlaybackStart, delegate(bool value)
            {
                ui.Settings.ClearChatOnPlaybackStart = value;
                ui.SaveSettings();
            }));

            ui.RefreshPlaybackStatus();
        }
    }
}
