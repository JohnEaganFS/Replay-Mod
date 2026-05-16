using UnityEngine;
using UnityEngine.UIElements;

namespace PuckReplayMod
{
    internal static class ReplayAboutSection
    {
        public static void Create(ReplayModUiService ui, VisualElement parent)
        {
            parent.Add(ReplayUiTools.CreateSectionTitle("About"));
            parent.Add(ReplayUiTools.CreateNote("Version and compatibility information for troubleshooting and updates."));

            parent.Add(CreateInfoRow("Installed version", ReplayModConstants.ModVersion));
            parent.Add(CreateInfoRow("Target Puck version", ReplayModConstants.TargetGameVersion));
            parent.Add(CreateInfoRow("Replay format", ReplayModConstants.ReplayBinaryMagic + " v" + ReplayModConstants.ReplayBinaryFormatVersion));
            parent.Add(CreateInfoRow("Replay data version", ReplayModConstants.ReplayDtoFormatVersion.ToString()));
            parent.Add(CreateInfoRow("Summary cache version", ReplayModConstants.ReplaySummaryCacheVersion.ToString()));
            parent.Add(CreateInfoRow("Replay folder", ui.Storage.ReplaysDirectory));

            parent.Add(ReplayUiTools.CreateSeparator());
            parent.Add(ReplayUiTools.CreateHeader("Updates"));

            string manifestText = string.IsNullOrEmpty(ReplayModConstants.UpdateManifestUrl)
                ? "Not configured"
                : ReplayModConstants.UpdateManifestUrl;
            parent.Add(CreateInfoRow("Update source", manifestText));

            Label statusLabel = ReplayUiTools.CreateNote("Manual update checks are ready, but no GitHub manifest URL is configured yet.");
            parent.Add(statusLabel);

            VisualElement actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.marginTop = 8f;
            parent.Add(actions);

            Button checkButton = ReplayUiTools.CreateButton("CHECK FOR UPDATES", delegate
            {
                ui.CheckForUpdates(statusLabel);
            });
            checkButton.style.marginRight = 8f;
            actions.Add(checkButton);

            Button openButton = ReplayUiTools.CreateButton("OPEN UPDATE PAGE", ui.OpenUpdateDownloadUrl);
            actions.Add(openButton);

            parent.Add(ReplayUiTools.CreateNote("Expected manifest JSON fields: latestVersion, minimumRecommendedVersion, downloadUrl, and notes. A GitHub raw file is enough for this."));
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
