using UnityEngine;
using UnityEngine.UIElements;

namespace PuckReplayMod
{
    internal static class ReplayInterfaceSettingsSection
    {
        public static void Create(ReplayModUiService ui, VisualElement parent)
        {
            parent.Add(ReplayUiTools.CreateSectionTitle("Interface"));
            parent.Add(ReplayUiTools.CreateNote("Adjust the Replay Manager window itself. Size changes give the window more room; scale changes make the whole interface larger or smaller. Press Apply when the layout feels right."));

            AddInterfaceSettings(ui, parent);
        }

        public static void AddInterfaceSettings(ReplayModUiService ui, VisualElement parent)
        {
            float draftWidth = Mathf.Clamp(ui.Settings.ManagerWindowWidthPercent, 58f, 94f);
            float draftHeight = Mathf.Clamp(ui.Settings.ManagerWindowHeightPercent, 58f, 92f);
            float draftScale = Mathf.Clamp(ui.Settings.ManagerUiScale, 0.85f, 1.3f);

            parent.Add(ReplayUiTools.CreateFloatSliderRow(
                "Window width",
                "Changes the horizontal size of the Replay Manager window without zooming the UI.",
                draftWidth,
                58f,
                94f,
                delegate(float value)
            {
                draftWidth = Mathf.Clamp(value, 58f, 94f);
            }));

            parent.Add(ReplayUiTools.CreateFloatSliderRow(
                "Window height",
                "Changes the vertical size of the Replay Manager window without zooming the UI.",
                draftHeight,
                58f,
                92f,
                delegate(float value)
            {
                draftHeight = Mathf.Clamp(value, 58f, 92f);
            }));

            parent.Add(ReplayUiTools.CreateFloatSliderRow(
                "Replay Manager scale",
                "Scales this settings and replay library window. The minimum is limited so the window remains usable.",
                draftScale,
                0.85f,
                1.3f,
                delegate(float value)
            {
                draftScale = Mathf.Clamp(value, 0.85f, 1.3f);
            }));

            VisualElement applyRow = new VisualElement();
            applyRow.style.flexDirection = FlexDirection.Row;
            applyRow.style.justifyContent = Justify.FlexEnd;
            applyRow.style.marginTop = 10f;

            Button applyButton = ReplayUiTools.CreateButton("APPLY INTERFACE SETTINGS", delegate
            {
                ui.Settings.ManagerWindowWidthPercent = Mathf.Clamp(draftWidth, 58f, 94f);
                ui.Settings.ManagerWindowHeightPercent = Mathf.Clamp(draftHeight, 58f, 92f);
                ui.Settings.ManagerUiScale = Mathf.Clamp(draftScale, 0.85f, 1.3f);
                ui.SaveSettings();
                ui.ApplyManagerWindowSize();
                ui.ApplyManagerScale();
            });
            applyButton.tooltip = "Applies the selected Replay Manager width, height, and scale.";
            applyButton.style.width = 235f;
            applyButton.style.minWidth = 235f;
            applyButton.style.maxWidth = 235f;
            applyRow.Add(applyButton);
            parent.Add(applyRow);
        }
    }
}
