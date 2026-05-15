using UnityEngine.UIElements;

namespace PuckReplayMod
{
    internal static class ReplayAdvancedSettingsSection
    {
        public static void Create(ReplayModUiService ui, VisualElement parent)
        {
            parent.Add(ReplayUiTools.CreateSectionTitle("Advanced"));
            parent.Add(ReplayUiTools.CreateNote("Extra options for troubleshooting and older replay files."));

            parent.Add(ReplayUiTools.CreateToggleRow("Show old replay files", ui.Settings.EnableLegacyImport, delegate(bool value)
            {
                ui.Settings.EnableLegacyImport = value;
                ui.SaveSettings();
            }));

            parent.Add(ReplayUiTools.CreateToggleRow("Detailed logging", ui.Settings.EnableDebugProfiling, delegate(bool value)
            {
                ui.Settings.EnableDebugProfiling = value;
                ui.SaveSettings();
            }));
        }
    }
}
