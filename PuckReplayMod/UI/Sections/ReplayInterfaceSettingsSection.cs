using UnityEngine;
using UnityEngine.UIElements;

namespace PuckReplayMod
{
    internal static class ReplayInterfaceSettingsSection
    {
        public static void Create(ReplayModUiService ui, VisualElement parent)
        {
            parent.Add(ReplayUiTools.CreateSectionTitle("Interface"));
            parent.Add(ReplayUiTools.CreateNote("Adjust the Replay Manager window itself. Scale changes apply only after you press Apply."));

            parent.Add(ReplayUiTools.CreateFloatSliderApplyRow(
                "Replay Manager scale",
                "Scales this settings and replay library window. The minimum is limited so the window remains usable.",
                ui.Settings.ManagerUiScale,
                0.85f,
                1.3f,
                delegate(float value)
            {
                ui.Settings.ManagerUiScale = Mathf.Clamp(value, 0.85f, 1.3f);
                ui.SaveSettings();
                ui.ApplyManagerScale();
            }));
        }
    }
}
