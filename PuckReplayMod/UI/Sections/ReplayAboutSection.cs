using UnityEngine;
using UnityEngine.UIElements;

namespace PuckReplayMod
{
    internal static class ReplayAboutSection
    {
        public static void Create(ReplayModUiService ui, VisualElement parent)
        {
            parent.Add(ReplayUiTools.CreateSectionTitle("About / Updates"));
            parent.Add(ReplayUiTools.CreateNote("Version details, compatibility information, update checks, and latest release notes."));

            parent.Add(CreateInfoRow("Installed version", ReplayModConstants.ModVersion));
            parent.Add(CreateInfoRow("Target Puck version", ReplayModConstants.TargetGameVersion));
            parent.Add(CreateInfoRow("Replay format", ReplayModConstants.ReplayBinaryMagic + " v" + ReplayModConstants.ReplayBinaryFormatVersion));
            parent.Add(CreateInfoRow("Replay data version", ReplayModConstants.ReplayDtoFormatVersion.ToString()));
            parent.Add(CreateInfoRow("Summary cache version", ReplayModConstants.ReplaySummaryCacheVersion.ToString()));
            parent.Add(CreateInfoRow("Replay folder", ui.Storage.ReplaysDirectory));

            parent.Add(ReplayUiTools.CreateSeparator());
            parent.Add(ReplayUiTools.CreateHeader("Updates"));

            Label statusLabel = ReplayUiTools.CreateNote(ui.GetUpdateStatusMessage());
            parent.Add(statusLabel);

            parent.Add(ReplayUiTools.CreateSeparator());
            parent.Add(ReplayUiTools.CreateHeader("Latest Patch Notes"));

            Label patchNotesLabel = ReplayUiTools.CreateNote(ui.GetUpdatePatchNotesMessage());
            patchNotesLabel.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(patchNotesLabel);
            ui.BindUpdateLabels(statusLabel, patchNotesLabel);

            VisualElement actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.marginTop = 8f;
            parent.Add(actions);

            Button checkButton = ReplayUiTools.CreateButton("CHECK FOR UPDATES", delegate
            {
                ui.CheckForUpdates(statusLabel);
            });
            checkButton.tooltip = "Check the GitHub-hosted update manifest for the latest Replay Mod version.";
            checkButton.style.marginRight = 8f;
            actions.Add(checkButton);

            Button openButton = ReplayUiTools.CreateButton("OPEN UPDATE PAGE", ui.OpenUpdateDownloadUrl);
            openButton.tooltip = "Open the Steam Workshop page for Replay Mod.";
            actions.Add(openButton);
        }

        private static VisualElement CreateInfoRow(string labelText, string valueText)
        {
            VisualElement row = ReplayUiTools.CreateConfigurationRow();
            row.style.alignItems = Align.FlexStart;

            Label label = ReplayUiTools.CreateConfigurationLabel(labelText);
            label.style.maxWidth = 210f;
            label.style.minWidth = 210f;
            label.style.flexGrow = 0f;
            row.Add(label);

            Label value = ReplayUiTools.CreateConfigurationLabel(valueText ?? string.Empty);
            value.style.unityTextAlign = TextAnchor.MiddleLeft;
            value.style.color = ReplayUiTools.MutedTextColor;
            value.style.whiteSpace = WhiteSpace.Normal;
            row.Add(value);

            return row;
        }
    }
}
