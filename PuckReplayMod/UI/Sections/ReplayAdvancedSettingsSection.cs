using UnityEngine.UIElements;

namespace PuckReplayMod
{
    internal static class ReplayAdvancedSettingsSection
    {
        public static void Create(ReplayModUiService ui, VisualElement parent)
        {
            parent.Add(ReplayUiTools.CreateSectionTitle("Advanced"));
            parent.Add(ReplayUiTools.CreateNote("Extra options for troubleshooting."));

            parent.Add(ReplayUiTools.CreateToggleRow("Detailed logging", "Writes extra Replay Mod timing and diagnostic logs. Leave off unless you are troubleshooting a bug or performance issue.", ui.Settings.EnableDebugProfiling, delegate(bool value)
            {
                ui.Settings.EnableDebugProfiling = value;
                ui.SaveSettings();
            }));

            parent.Add(ReplayUiTools.CreateToggleRow("Fast keyframe seeking", "Uses 30-second replay keyframes for faster large timeline jumps. Turn this off to compare seek speed against the full replay seek path when testing.", ui.Settings.EnableKeyframeSeeking, delegate(bool value)
            {
                ui.Settings.EnableKeyframeSeeking = value;
                ui.SaveSettings();
            }));
        }
    }
}
