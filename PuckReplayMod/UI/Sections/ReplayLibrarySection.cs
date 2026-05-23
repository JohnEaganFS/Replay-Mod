using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuckReplayMod
{
    internal static class ReplayLibrarySection
    {
        private const int MaxLibrarySummaries = 500;
        private const int MaxVisibleReplayRows = 200;

        public static void Create(ReplayModUiService ui, VisualElement parent)
        {
            parent.style.flexGrow = 1f;
            parent.style.paddingTop = 12f;
            parent.style.paddingBottom = 10f;

            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.marginBottom = 8f;
            parent.Add(header);

            Label title = ReplayUiTools.CreateSectionTitle("Replay Library");
            title.style.marginBottom = 0f;
            header.Add(title);

            VisualElement headerButtons = new VisualElement();
            headerButtons.style.flexDirection = FlexDirection.Row;
            headerButtons.style.alignItems = Align.Center;
            header.Add(headerButtons);

            Button importButton = ReplayUiTools.CreateButton(ui.IsReplayImportPanelVisible ? "CANCEL IMPORT" : "IMPORT", delegate
            {
                ui.ToggleReplayImportPanel();
            });
            importButton.style.width = ui.IsReplayImportPanelVisible ? 124f : 88f;
            importButton.style.minWidth = importButton.style.width;
            importButton.style.height = 34f;
            importButton.style.minHeight = 34f;
            importButton.style.marginRight = 6f;
            importButton.tooltip = "Open the Imports folder flow for shared .puckreplay files.";
            headerButtons.Add(importButton);

            Button exportsButton = ReplayUiTools.CreateButton("EXPORTS", delegate
            {
                ui.OpenExportsFolder();
            });
            exportsButton.style.width = 94f;
            exportsButton.style.minWidth = 94f;
            exportsButton.style.height = 34f;
            exportsButton.style.minHeight = 34f;
            exportsButton.style.marginRight = 6f;
            exportsButton.tooltip = "Open the folder where exported replays are copied.";
            headerButtons.Add(exportsButton);

            Button refreshButton = ReplayUiTools.CreateButton("REFRESH", delegate
            {
                ui.RefreshLibraryText();
                ui.RefreshReplayList();
                ui.RefreshPlaybackStatus();
            });
            refreshButton.tooltip = "Refresh replay summaries and library status.";
            refreshButton.style.width = 104f;
            refreshButton.style.minWidth = 104f;
            refreshButton.style.height = 34f;
            refreshButton.style.minHeight = 34f;
            headerButtons.Add(refreshButton);

            parent.Add(CreateLibraryControls(ui));

            VisualElement libraryBody = new VisualElement();
            libraryBody.style.flexDirection = FlexDirection.Row;
            libraryBody.style.flexGrow = 1f;
            parent.Add(libraryBody);

            VisualElement listFrame = new VisualElement
            {
                name = "PuckReplayModReplayListFrame"
            };
            listFrame.style.flexGrow = 1f;
            listFrame.style.minHeight = 360f;
            listFrame.style.backgroundColor = new Color(0.11f, 0.11f, 0.11f, 0.95f);
            listFrame.style.borderTopWidth = 1f;
            listFrame.style.borderBottomWidth = 1f;
            listFrame.style.borderLeftWidth = 1f;
            listFrame.style.borderRightWidth = 1f;
            listFrame.style.borderTopColor = new Color(0.32f, 0.32f, 0.32f, 1f);
            listFrame.style.borderBottomColor = new Color(0.32f, 0.32f, 0.32f, 1f);
            listFrame.style.borderLeftColor = new Color(0.32f, 0.32f, 0.32f, 1f);
            listFrame.style.borderRightColor = new Color(0.32f, 0.32f, 0.32f, 1f);
            libraryBody.Add(listFrame);

            ScrollView replayScrollView = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "PuckReplayModReplayListScroll"
            };
            replayScrollView.style.flexGrow = 1f;
            replayScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            listFrame.Add(replayScrollView);

            ui.ReplayList = new VisualElement
            {
                name = "PuckReplayModReplayList"
            };
            ui.ReplayList.style.paddingLeft = 4f;
            ui.ReplayList.style.paddingRight = 4f;
            ui.ReplayList.style.paddingTop = 4f;
            ui.ReplayList.style.paddingBottom = 4f;
            replayScrollView.Add(ui.ReplayList);

            VisualElement miniPanel = CreateLibraryMiniPanel(ui);
            libraryBody.Add(miniPanel);

            ui.RefreshLibraryText();
            ui.RefreshPlaybackStatus();
            ui.RefreshReplayList();
        }

        private static VisualElement CreateLibraryMiniPanel(ReplayModUiService ui)
        {
            VisualElement miniPanel = new VisualElement
            {
                name = "PuckReplayModLibraryMiniPanel"
            };
            miniPanel.style.width = 214f;
            miniPanel.style.minWidth = 214f;
            miniPanel.style.marginLeft = 10f;
            miniPanel.style.paddingLeft = 10f;
            miniPanel.style.paddingRight = 10f;
            miniPanel.style.paddingTop = 10f;
            miniPanel.style.paddingBottom = 10f;
            miniPanel.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.98f);
            miniPanel.style.borderTopWidth = 1f;
            miniPanel.style.borderBottomWidth = 1f;
            miniPanel.style.borderLeftWidth = 1f;
            miniPanel.style.borderRightWidth = 1f;
            miniPanel.style.borderTopColor = new Color(0.32f, 0.32f, 0.32f, 1f);
            miniPanel.style.borderBottomColor = new Color(0.32f, 0.32f, 0.32f, 1f);
            miniPanel.style.borderLeftColor = new Color(0.32f, 0.32f, 0.32f, 1f);
            miniPanel.style.borderRightColor = new Color(0.32f, 0.32f, 0.32f, 1f);

            Label nowPlayingTitle = ReplayUiTools.CreateHeader("Now Playing");
            nowPlayingTitle.style.marginTop = 0f;
            nowPlayingTitle.style.marginBottom = 4f;
            miniPanel.Add(nowPlayingTitle);

            ui.PlaybackLabel = ReplayUiTools.CreateConfigurationLabel(string.Empty);
            ui.PlaybackLabel.style.fontSize = 13f;
            ui.PlaybackLabel.style.color = Color.white;
            ui.PlaybackLabel.style.marginBottom = 12f;
            miniPanel.Add(ui.PlaybackLabel);

            VisualElement separator = ReplayUiTools.CreateSeparator();
            separator.style.marginTop = 4f;
            separator.style.marginBottom = 10f;
            miniPanel.Add(separator);

            Label libraryTitle = ReplayUiTools.CreateHeader("Library");
            libraryTitle.style.marginTop = 0f;
            libraryTitle.style.marginBottom = 4f;
            miniPanel.Add(libraryTitle);

            ui.StorageLabel = ReplayUiTools.CreateConfigurationLabel(string.Empty);
            ui.StorageLabel.style.fontSize = 13f;
            ui.StorageLabel.style.color = ReplayUiTools.BodyTextColor;
            miniPanel.Add(ui.StorageLabel);

            return miniPanel;
        }

        private static VisualElement CreateLibraryControls(ReplayModUiService ui)
        {
            VisualElement controls = new VisualElement
            {
                name = "PuckReplayModLibraryControls"
            };
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.alignItems = Align.Center;
            controls.style.flexWrap = Wrap.Wrap;
            controls.style.marginBottom = 8f;

            controls.Add(CreateToolbarLabel("Search"));

            TextField searchField = new TextField
            {
                name = "PuckReplayModLibrarySearchField",
                value = ui.ReplayLibrarySearchText ?? string.Empty
            };
            searchField.style.flexGrow = 1f;
            searchField.style.minWidth = 190f;
            searchField.style.height = 32f;
            searchField.style.minHeight = 32f;
            searchField.style.marginRight = 8f;
            searchField.style.color = Color.white;
            searchField.style.fontSize = 13f;
            searchField.RegisterValueChangedCallback(delegate(ChangeEvent<string> evt)
            {
                ui.SetReplayLibrarySearchText(evt.newValue);
            });
            searchField.RegisterCallback<AttachToPanelEvent>(delegate
            {
                VisualElement input = searchField.Q<VisualElement>(null, "unity-base-text-field__input");
                if (input != null)
                {
                    input.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);
                    input.style.color = Color.white;
                    input.style.paddingLeft = 8f;
                    input.style.paddingRight = 8f;
                    input.style.paddingTop = 0f;
                    input.style.paddingBottom = 0f;
                    input.style.unityTextAlign = TextAnchor.MiddleLeft;
                    input.style.fontSize = 13f;
                }
            });
            controls.Add(searchField);

            controls.Add(CreateToolbarLabel("Filter"));

            PopupField<string> filterDropdown = ReplayUiTools.CreateDropdown(GetLibraryFilterChoices(), ui.ReplayLibraryFilterMode, delegate(string value)
            {
                ui.SetReplayLibraryFilterMode(value);
            });
            filterDropdown.style.width = 156f;
            filterDropdown.style.minWidth = 156f;
            filterDropdown.style.maxWidth = 156f;
            filterDropdown.style.marginRight = 8f;
            controls.Add(filterDropdown);

            controls.Add(CreateToolbarLabel("Sort"));

            PopupField<string> sortDropdown = ReplayUiTools.CreateDropdown(GetLibrarySortCategories(), ui.ReplayLibrarySortCategory, delegate(string value)
            {
                ui.SetReplayLibrarySortCategory(value);
            });
            sortDropdown.style.width = 150f;
            sortDropdown.style.minWidth = 150f;
            sortDropdown.style.maxWidth = 150f;
            sortDropdown.style.marginRight = 8f;
            controls.Add(sortDropdown);

            controls.Add(CreateToolbarLabel("Order"));

            Button orderButton = ReplayUiTools.CreateButton(GetSortDirectionButtonText(ui.ReplayLibrarySortDirection), null);
            orderButton.clicked += delegate
            {
                string nextDirection = ui.ReplayLibrarySortDirection == "Ascending" ? "Descending" : "Ascending";
                orderButton.text = GetSortDirectionButtonText(nextDirection);
                ui.SetReplayLibrarySortDirection(nextDirection);
            };
            orderButton.style.width = 126f;
            orderButton.style.minWidth = 126f;
            orderButton.style.maxWidth = 126f;
            orderButton.style.height = 32f;
            orderButton.style.minHeight = 32f;
            orderButton.style.maxHeight = 32f;
            orderButton.style.paddingLeft = 0f;
            orderButton.style.paddingRight = 0f;
            orderButton.style.unityTextAlign = TextAnchor.MiddleCenter;
            controls.Add(orderButton);

            return controls;
        }

        private static Label CreateToolbarLabel(string text)
        {
            Label label = ReplayUiTools.CreateConfigurationLabel(text);
            label.style.flexGrow = 0f;
            label.style.flexShrink = 0f;
            label.style.marginRight = 3f;
            label.style.fontSize = 13f;
            label.style.color = ReplayUiTools.MutedTextColor;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            return label;
        }

        public static void RefreshReplayList(ReplayModUiService ui)
        {
            if (ui.ReplayList == null)
            {
                return;
            }

            ui.ReplayList.Clear();
            if (!string.IsNullOrEmpty(ui.ReplayLibraryStatusMessage))
            {
                ui.ReplayList.Add(CreateLibraryStatusPanel(ui));
            }

            if (ui.IsReplayImportPanelVisible)
            {
                ui.ReplayList.Add(CreateImportPanel(ui));
            }

            List<ReplayFileSummary> allReplays = ui.Reader.GetRecentReplays(ui.Storage.ReplaysDirectory, ui.Storage.SummariesDirectory, MaxLibrarySummaries);
            if (allReplays.Count == 0)
            {
                Label emptyLabel = ReplayUiTools.CreateConfigurationLabel("No saved replays yet. Join a server with recording enabled, then leave to save your first replay.");
                emptyLabel.style.color = ReplayUiTools.MutedTextColor;
                ui.ReplayList.Add(emptyLabel);
                return;
            }

            List<ReplayFileSummary> replays = ApplyLibrarySearchFilterAndSort(ui, allReplays);
            if (replays.Count == 0)
            {
                Label emptyLabel = ReplayUiTools.CreateConfigurationLabel("No replays match the current search and filter.");
                emptyLabel.style.color = ReplayUiTools.MutedTextColor;
                ui.ReplayList.Add(emptyLabel);
                return;
            }

            int visibleRows = Math.Min(replays.Count, MaxVisibleReplayRows);
            for (int i = 0; i < visibleRows; i++)
            {
                ui.ReplayList.Add(CreateReplayRow(ui, replays[i]));
            }

            if (replays.Count > MaxVisibleReplayRows)
            {
                Label cappedLabel = ReplayUiTools.CreateConfigurationLabel("Showing " + MaxVisibleReplayRows + " of " + replays.Count + " matches. Refine the search or filter to narrow the list.");
                cappedLabel.style.color = ReplayUiTools.MutedTextColor;
                cappedLabel.style.fontSize = 12f;
                cappedLabel.style.marginTop = 8f;
                ui.ReplayList.Add(cappedLabel);
            }
        }

        private static List<string> GetLibraryFilterChoices()
        {
            return new List<string>
            {
                "All",
                "Favorites",
                "Imported",
                "Current format",
                "Older format",
                "Needs reindex",
                "Indexing",
                "Unsupported"
            };
        }

        private static List<string> GetLibrarySortCategories()
        {
            return new List<string>
            {
                "Recorded date",
                "Server",
                "Duration",
                "Game version",
                "File size",
                "File name"
            };
        }

        private static string GetSortDirectionButtonText(string direction)
        {
            return direction == "Ascending" ? "Ascending" : "Descending";
        }

        private static List<ReplayFileSummary> ApplyLibrarySearchFilterAndSort(ReplayModUiService ui, List<ReplayFileSummary> replays)
        {
            IEnumerable<ReplayFileSummary> query = replays.Where(delegate(ReplayFileSummary replay)
            {
                return MatchesLibraryFilter(replay, ui.ReplayLibraryFilterMode) &&
                    MatchesLibrarySearch(replay, ui.ReplayLibrarySearchText);
            });

            bool ascending = ui.ReplayLibrarySortDirection == "Ascending";
            switch (ui.ReplayLibrarySortCategory)
            {
                case "Server":
                    query = ascending
                        ? query.OrderBy(replay => GetServerTitle(replay), StringComparer.OrdinalIgnoreCase).ThenByDescending(replay => GetRecordedSortTime(replay))
                        : query.OrderByDescending(replay => GetServerTitle(replay), StringComparer.OrdinalIgnoreCase).ThenByDescending(replay => GetRecordedSortTime(replay));
                    break;
                case "Duration":
                    query = ascending
                        ? query.OrderBy(replay => replay.DurationSeconds).ThenByDescending(replay => GetRecordedSortTime(replay))
                        : query.OrderByDescending(replay => replay.DurationSeconds).ThenByDescending(replay => GetRecordedSortTime(replay));
                    break;
                case "Game version":
                    query = ascending
                        ? query.OrderBy(replay => replay.GameVersion ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenByDescending(replay => GetRecordedSortTime(replay))
                        : query.OrderByDescending(replay => replay.GameVersion ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenByDescending(replay => GetRecordedSortTime(replay));
                    break;
                case "File size":
                    query = ascending
                        ? query.OrderBy(replay => replay.SizeBytes).ThenByDescending(replay => GetRecordedSortTime(replay))
                        : query.OrderByDescending(replay => replay.SizeBytes).ThenByDescending(replay => GetRecordedSortTime(replay));
                    break;
                case "File name":
                    query = ascending
                        ? query.OrderBy(replay => replay.FileName ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenByDescending(replay => GetRecordedSortTime(replay))
                        : query.OrderByDescending(replay => replay.FileName ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenByDescending(replay => GetRecordedSortTime(replay));
                    break;
                case "Recorded date":
                default:
                    query = ascending
                        ? query.OrderBy(replay => GetRecordedSortTime(replay)).ThenBy(replay => GetDisplayTitle(replay), StringComparer.OrdinalIgnoreCase)
                        : query.OrderByDescending(replay => GetRecordedSortTime(replay)).ThenBy(replay => GetDisplayTitle(replay), StringComparer.OrdinalIgnoreCase);
                    break;
            }

            return query.ToList();
        }

        private static bool MatchesLibraryFilter(ReplayFileSummary replay, string filter)
        {
            if (replay == null || string.IsNullOrEmpty(filter) || filter == "All")
            {
                return true;
            }

            CompatibilityBadge badge = GetCompatibilityBadge(replay);
            switch (filter)
            {
                case "Favorites":
                    return replay.IsFavorite;
                case "Imported":
                    return replay.IsImported;
                case "Current format":
                    return badge.Text == "CURRENT";
                case "Older format":
                    return badge.Text == "OLDER";
                case "Needs reindex":
                    return badge.Text == "REINDEX";
                case "Indexing":
                    return badge.Text == "INDEXING";
                case "Unsupported":
                    return badge.Text == "UNSUPPORTED";
                default:
                    return true;
            }
        }

        private static bool MatchesLibrarySearch(ReplayFileSummary replay, string searchText)
        {
            if (replay == null || string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }

            string haystack = BuildLibrarySearchHaystack(replay);
            string[] tokens = searchText
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (haystack.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static string BuildLibrarySearchHaystack(ReplayFileSummary replay)
        {
            StringBuilder builder = new StringBuilder();
            AppendSearchValue(builder, GetDisplayTitle(replay));
            AppendSearchValue(builder, GetServerTitle(replay));
            AppendSearchValue(builder, replay.FileName);
            AppendSearchValue(builder, replay.DisplayName);
            AppendSearchValue(builder, replay.RecordedBy);
            AppendSearchValue(builder, replay.GameVersion);
            AppendSearchValue(builder, replay.ModVersion);
            AppendSearchValue(builder, replay.ReplayContainerFormat);
            AppendSearchValue(builder, replay.ReplayContainerVersion.ToString());
            AppendSearchValue(builder, replay.ReplayFormatVersion.ToString());
            AppendSearchValue(builder, GetCompatibilityBadge(replay).Text);
            AppendSearchValue(builder, FormatDuration(replay.DurationSeconds));
            AppendSearchValue(builder, ReplayUiTools.FormatBytes(replay.SizeBytes));
            AppendSearchValue(builder, replay.SizeBytes.ToString());
            AppendSearchValue(builder, replay.TotalTicks.ToString());
            AppendSearchValue(builder, replay.TickRate.ToString());
            AppendSearchValue(builder, replay.EventCount.ToString());
            AppendSearchValue(builder, replay.GoalCount + " goals");
            AppendSearchValue(builder, replay.MarkerCount + " markers");
            AppendSearchValue(builder, replay.GameSegments != null ? replay.GameSegments.Count + " games" : "0 games");

            DateTime recorded = GetRecordedSortTime(replay).ToLocalTime();
            AppendSearchValue(builder, recorded.ToString("MM/dd/yyyy HH:mm"));
            AppendSearchValue(builder, recorded.ToString("yyyy-MM-dd HH:mm"));
            AppendSearchValue(builder, recorded.ToString("MMMM d yyyy"));

            if (replay.IsFavorite)
            {
                AppendSearchValue(builder, "favorite starred");
            }

            if (replay.IsImported)
            {
                AppendSearchValue(builder, "imported shared");
                if (replay.ImportedUtcTicks > 0L)
                {
                    DateTime imported = new DateTime(replay.ImportedUtcTicks, DateTimeKind.Utc).ToLocalTime();
                    AppendSearchValue(builder, imported.ToString("MM/dd/yyyy HH:mm"));
                    AppendSearchValue(builder, imported.ToString("yyyy-MM-dd HH:mm"));
                }
            }

            if (replay.HasGoals)
            {
                AppendSearchValue(builder, "goals");
            }

            if (replay.HasMarkers)
            {
                AppendSearchValue(builder, "markers");
            }

            if (replay.HasChat)
            {
                AppendSearchValue(builder, "chat");
            }

            if (replay.HasScoreboard)
            {
                AppendSearchValue(builder, "scoreboard");
            }

            return builder.ToString();
        }

        private static void AppendSearchValue(StringBuilder builder, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            builder.Append(value);
            builder.Append(' ');
        }

        private static DateTime GetRecordedSortTime(ReplayFileSummary replay)
        {
            if (replay != null && replay.StartedUtcTicks > 0L)
            {
                return new DateTime(replay.StartedUtcTicks, DateTimeKind.Utc);
            }

            return replay != null ? replay.LastWriteUtc : DateTime.MinValue;
        }

        private static VisualElement CreateLibraryStatusPanel(ReplayModUiService ui)
        {
            VisualElement panel = new VisualElement();
            panel.style.marginTop = 2f;
            panel.style.marginBottom = 6f;
            panel.style.paddingLeft = 10f;
            panel.style.paddingRight = 10f;
            panel.style.paddingTop = 7f;
            panel.style.paddingBottom = 7f;
            panel.style.backgroundColor = ui.ReplayLibraryStatusIsError
                ? new Color(0.35f, 0.08f, 0.06f, 0.98f)
                : new Color(0.11f, 0.24f, 0.14f, 0.98f);

            Label label = ReplayUiTools.CreateConfigurationLabel(ui.ReplayLibraryStatusMessage);
            label.style.color = Color.white;
            label.style.fontSize = 12f;
            label.style.marginBottom = 0f;
            panel.Add(label);
            return panel;
        }

        private static VisualElement CreateImportPanel(ReplayModUiService ui)
        {
            VisualElement panel = new VisualElement();
            panel.style.marginTop = 2f;
            panel.style.marginBottom = 6f;
            panel.style.paddingLeft = 10f;
            panel.style.paddingRight = 10f;
            panel.style.paddingTop = 10f;
            panel.style.paddingBottom = 10f;
            panel.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.98f);
            panel.style.borderTopWidth = 1f;
            panel.style.borderBottomWidth = 1f;
            panel.style.borderLeftWidth = 1f;
            panel.style.borderRightWidth = 1f;
            panel.style.borderTopColor = new Color(0.32f, 0.32f, 0.32f, 1f);
            panel.style.borderBottomColor = new Color(0.32f, 0.32f, 0.32f, 1f);
            panel.style.borderLeftColor = new Color(0.32f, 0.32f, 0.32f, 1f);
            panel.style.borderRightColor = new Color(0.32f, 0.32f, 0.32f, 1f);

            Label title = ReplayUiTools.CreateHeader("Import Replay");
            title.style.marginTop = 0f;
            title.style.marginBottom = 4f;
            panel.Add(title);

            Label note = ReplayUiTools.CreateConfigurationLabel("Put shared .puckreplay files in the Imports folder, then import them here. Successful imports are copied into your library and moved to Imports/Imported.");
            note.style.color = ReplayUiTools.BodyTextColor;
            note.style.fontSize = 12f;
            note.style.marginBottom = 8f;
            panel.Add(note);

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            panel.Add(row);

            Button openFolderButton = ReplayUiTools.CreateButton("OPEN IMPORTS FOLDER", delegate
            {
                ui.OpenImportsFolder();
            });
            openFolderButton.tooltip = "Open the folder where shared .puckreplay files should be placed before importing.";
            openFolderButton.style.width = 178f;
            openFolderButton.style.minWidth = 178f;
            openFolderButton.style.height = 32f;
            openFolderButton.style.minHeight = 32f;
            openFolderButton.style.marginRight = 8f;
            row.Add(openFolderButton);

            Button importButton = ReplayUiTools.CreateButton("IMPORT FILES", delegate
            {
                ui.ImportReplaysFromImportsFolder();
            });
            importButton.tooltip = "Copy valid .puckreplay files from Imports into your replay library.";
            importButton.style.width = 112f;
            importButton.style.minWidth = 112f;
            importButton.style.height = 32f;
            importButton.style.minHeight = 32f;
            row.Add(importButton);

            if (!string.IsNullOrEmpty(ui.ReplayImportStatusMessage))
            {
                Label status = ReplayUiTools.CreateConfigurationLabel(ui.ReplayImportStatusMessage);
                status.style.color = ui.ReplayImportStatusMessage.StartsWith("Import failed", StringComparison.OrdinalIgnoreCase)
                    ? new Color(1f, 0.5f, 0.42f, 1f)
                    : ReplayUiTools.MutedTextColor;
                status.style.fontSize = 12f;
                status.style.marginTop = 8f;
                panel.Add(status);
            }

            return panel;
        }

        private static VisualElement CreateReplayRow(ReplayModUiService ui, ReplayFileSummary replay)
        {
            VisualElement container = new VisualElement();
            container.style.marginTop = 2f;
            container.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 0.95f);

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.minHeight = 42f;
            row.style.paddingLeft = 8f;
            row.style.paddingRight = 8f;
            row.style.paddingTop = 3f;
            row.style.paddingBottom = 3f;
            container.Add(row);

            VisualElement details = new VisualElement();
            details.style.flexDirection = FlexDirection.Column;
            details.style.flexGrow = 1f;
            details.style.marginRight = 8f;

            VisualElement titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            details.Add(titleRow);

            Label title = ReplayUiTools.CreateConfigurationLabel(GetDisplayTitle(replay));
            title.style.color = Color.white;
            title.style.fontSize = 14f;
            title.style.marginBottom = 0f;
            title.style.whiteSpace = WhiteSpace.NoWrap;
            titleRow.Add(title);

            CompatibilityBadge compatibilityBadge = GetCompatibilityBadge(replay);
            titleRow.Add(CreateCompatibilityBadge(compatibilityBadge));
            if (replay.IsImported)
            {
                titleRow.Add(CreateSourceBadge("IMPORTED", GetImportedTooltip(replay)));
            }

            string date = replay.LastWriteUtc.ToLocalTime().ToString("MM/dd/yyyy HH:mm");
            string duration = replay.IsMetadataComplete ? FormatDuration(replay.DurationSeconds) : "Indexing...";
            string sourceName = string.IsNullOrEmpty(replay.DisplayName) ? string.Empty : "    " + GetServerTitle(replay);
            string versionInfo = string.IsNullOrEmpty(replay.GameVersion) ? string.Empty : "    Puck " + replay.GameVersion;
            string importedInfo = replay.IsImported && replay.ImportedUtcTicks > 0L
                ? "    Imported " + new DateTime(replay.ImportedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("MM/dd/yyyy HH:mm")
                : string.Empty;
            Label meta = ReplayUiTools.CreateConfigurationLabel(date + "    " + duration + "    " + ReplayUiTools.FormatBytes(replay.SizeBytes) + versionInfo + importedInfo + sourceName);
            meta.style.fontSize = 11f;
            meta.style.color = ReplayUiTools.MutedTextColor;
            meta.style.whiteSpace = WhiteSpace.NoWrap;
            details.Add(meta);
            row.Add(details);

            ReplayFileSummary selectedReplay = replay;
            Button playButton = ReplayUiTools.CreateButton("PLAY", delegate
            {
                ui.PlayReplay(selectedReplay.FilePath);
            });
            playButton.tooltip = "Load this replay into a local playback session.";
            playButton.style.width = 74f;
            playButton.style.minWidth = 74f;
            playButton.style.height = 32f;
            playButton.style.minHeight = 32f;
            playButton.style.marginRight = 4f;
            playButton.SetEnabled(!compatibilityBadge.IsUnsupported);
            if (compatibilityBadge.IsUnsupported)
            {
                playButton.tooltip = "This replay was made with a newer replay format than this mod supports.";
            }

            row.Add(playButton);

            VisualElement actionPanel = CreateActionPanel(ui, selectedReplay);
            actionPanel.style.display = DisplayStyle.None;
            container.Add(actionPanel);

            Button actionButton = ReplayUiTools.CreateButton("...", delegate
            {
                actionPanel.style.display = actionPanel.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            });
            actionButton.tooltip = "Show replay actions.";
            actionButton.style.width = 34f;
            actionButton.style.minWidth = 34f;
            actionButton.style.height = 32f;
            actionButton.style.minHeight = 32f;
            actionButton.style.marginRight = 4f;
            actionButton.style.paddingLeft = 0f;
            actionButton.style.paddingRight = 0f;
            row.Add(actionButton);

            row.Add(CreateFavoriteButton(ui, selectedReplay));

            return container;
        }

        private static Label CreateCompatibilityBadge(CompatibilityBadge badge)
        {
            Label label = new Label(badge.Text);
            label.tooltip = badge.Tooltip;
            label.style.fontSize = 10f;
            label.style.color = badge.TextColor;
            label.style.backgroundColor = badge.BackgroundColor;
            label.style.marginLeft = 8f;
            label.style.paddingLeft = 5f;
            label.style.paddingRight = 5f;
            label.style.paddingTop = 1f;
            label.style.paddingBottom = 1f;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.flexShrink = 0f;
            return label;
        }

        private static Label CreateSourceBadge(string text, string tooltip)
        {
            Label label = new Label(text);
            label.tooltip = tooltip;
            label.style.fontSize = 10f;
            label.style.color = Color.black;
            label.style.backgroundColor = new Color(0.45f, 0.72f, 0.95f, 1f);
            label.style.marginLeft = 6f;
            label.style.paddingLeft = 5f;
            label.style.paddingRight = 5f;
            label.style.paddingTop = 1f;
            label.style.paddingBottom = 1f;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.flexShrink = 0f;
            return label;
        }

        private static string GetImportedTooltip(ReplayFileSummary replay)
        {
            if (replay == null || replay.ImportedUtcTicks <= 0L)
            {
                return "This replay was imported into this library.";
            }

            return "Imported " + new DateTime(replay.ImportedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("MM/dd/yyyy HH:mm");
        }

        private static CompatibilityBadge GetCompatibilityBadge(ReplayFileSummary replay)
        {
            if (replay == null || !replay.IsMetadataComplete)
            {
                return new CompatibilityBadge(
                    "INDEXING",
                    "Replay metadata is still being indexed.",
                    ReplayUiTools.MutedTextColor,
                    new Color(0.24f, 0.24f, 0.24f, 1f),
                    false);
            }

            if (replay.SummaryCacheVersion > ReplayModConstants.ReplaySummaryCacheVersion ||
                replay.ReplayFormatVersion > ReplayModConstants.ReplayDtoFormatVersion ||
                (replay.ReplayContainerFormat == ReplayModConstants.ReplayBinaryMagic && replay.ReplayContainerVersion > ReplayModConstants.ReplayBinaryFormatVersion))
            {
                return new CompatibilityBadge(
                    "UNSUPPORTED",
                    "This replay uses a newer replay format than this mod supports.",
                    Color.white,
                    new Color(0.55f, 0.05f, 0.06f, 1f),
                    true);
            }

            if (replay.SummaryCacheVersion < ReplayModConstants.ReplaySummaryCacheVersion)
            {
                return new CompatibilityBadge(
                    "REINDEX",
                    "This replay has an older summary cache. Replay Mod will refresh it in the background.",
                    Color.black,
                    new Color(0.95f, 0.7f, 0.18f, 1f),
                    false);
            }

            if (replay.ReplayFormatVersion < ReplayModConstants.ReplayDtoFormatVersion ||
                replay.ReplayContainerFormat != ReplayModConstants.ReplayBinaryMagic ||
                replay.ReplayContainerVersion < ReplayModConstants.ReplayBinaryFormatVersion)
            {
                return new CompatibilityBadge(
                    "OLDER",
                    "This replay is from an older Replay Mod format. It is readable, but newer playback features may be missing.",
                    Color.black,
                    new Color(0.95f, 0.82f, 0.3f, 1f),
                    false);
            }

            return new CompatibilityBadge(
                "CURRENT",
                "This replay uses the current Replay Mod format.",
                Color.white,
                new Color(0.12f, 0.42f, 0.2f, 1f),
                false);
        }

        private struct CompatibilityBadge
        {
            public CompatibilityBadge(string text, string tooltip, Color textColor, Color backgroundColor, bool isUnsupported)
            {
                this.Text = text;
                this.Tooltip = tooltip;
                this.TextColor = textColor;
                this.BackgroundColor = backgroundColor;
                this.IsUnsupported = isUnsupported;
            }

            public string Text;
            public string Tooltip;
            public Color TextColor;
            public Color BackgroundColor;
            public bool IsUnsupported;
        }

        private static Button CreateFavoriteButton(ReplayModUiService ui, ReplayFileSummary replay)
        {
            Button favoriteButton = new Button(delegate
            {
                ui.SetReplayFavorite(replay, !replay.IsFavorite);
            });
            ReplayUiTools.StyleConfigButton(favoriteButton);
            favoriteButton.tooltip = replay.IsFavorite ? "Remove from favorites" : "Add to favorites";
            favoriteButton.text = string.Empty;
            favoriteButton.style.width = 34f;
            favoriteButton.style.minWidth = 34f;
            favoriteButton.style.height = 32f;
            favoriteButton.style.minHeight = 32f;
            favoriteButton.style.alignItems = Align.Center;
            favoriteButton.style.justifyContent = Justify.Center;
            favoriteButton.style.paddingLeft = 5f;
            favoriteButton.style.paddingRight = 5f;
            favoriteButton.style.paddingTop = 5f;
            favoriteButton.style.paddingBottom = 5f;
            favoriteButton.Add(new FavoriteStarIcon(replay.IsFavorite, 20f));
            return favoriteButton;
        }

        private sealed class FavoriteStarIcon : VisualElement
        {
            private readonly bool filled;

            public FavoriteStarIcon(bool filled, float size)
            {
                this.filled = filled;
                base.pickingMode = PickingMode.Ignore;
                base.style.width = size;
                base.style.height = size;
                base.generateVisualContent += this.OnGenerateVisualContent;
            }

            private void OnGenerateVisualContent(MeshGenerationContext context)
            {
                Rect rect = base.contentRect;
                float size = Mathf.Min(rect.width, rect.height);
                if (size <= 0f)
                {
                    return;
                }

                Vector2 center = new Vector2(rect.x + (rect.width * 0.5f), rect.y + (rect.height * 0.5f));
                Vector2[] points = this.CreateStarPoints(center, size * 0.43f, size * 0.2f);
                Painter2D painter = context.painter2D;
                painter.lineJoin = LineJoin.Round;
                painter.lineCap = LineCap.Round;
                painter.lineWidth = Mathf.Max(1.8f, size * 0.075f);
                painter.strokeColor = new Color(1f, 0.78f, 0.16f, 1f);
                painter.fillColor = this.filled ? new Color(1f, 0.67f, 0.08f, 1f) : new Color(0f, 0f, 0f, 0f);

                painter.BeginPath();
                painter.MoveTo(points[0]);
                for (int i = 1; i < points.Length; i++)
                {
                    painter.LineTo(points[i]);
                }

                painter.ClosePath();
                if (this.filled)
                {
                    painter.Fill();
                }

                painter.Stroke();
            }

            private Vector2[] CreateStarPoints(Vector2 center, float outerRadius, float innerRadius)
            {
                Vector2[] points = new Vector2[10];
                for (int i = 0; i < points.Length; i++)
                {
                    float radius = i % 2 == 0 ? outerRadius : innerRadius;
                    float angle = (-90f + (36f * i)) * Mathf.Deg2Rad;
                    points[i] = new Vector2(center.x + (Mathf.Cos(angle) * radius), center.y + (Mathf.Sin(angle) * radius));
                }

                return points;
            }
        }

        private static VisualElement CreateActionPanel(ReplayModUiService ui, ReplayFileSummary replay)
        {
            VisualElement panel = new VisualElement();
            panel.style.flexDirection = FlexDirection.Row;
            panel.style.flexWrap = Wrap.Wrap;
            panel.style.alignItems = Align.Center;
            panel.style.justifyContent = Justify.FlexEnd;
            panel.style.paddingLeft = 10f;
            panel.style.paddingRight = 10f;
            panel.style.paddingTop = 8f;
            panel.style.paddingBottom = 8f;
            panel.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.98f);

            ShowDefaultActions(ui, replay, panel);
            return panel;
        }

        private static void ShowDefaultActions(ReplayModUiService ui, ReplayFileSummary replay, VisualElement panel)
        {
            panel.Clear();
            panel.style.justifyContent = Justify.FlexEnd;

            Button renameButton = CreateActionButton("RENAME", "Set a local display name for this replay.", delegate
            {
                ShowRenameForm(ui, replay, panel);
            });
            panel.Add(renameButton);

            Button openButton = CreateActionButton("OPEN LOCATION", "Open this replay in File Explorer.", delegate
            {
                ui.OpenReplayLocation(replay);
            });
            openButton.style.width = 104f;
            openButton.style.minWidth = 104f;
            panel.Add(openButton);

            Button copyPathButton = CreateActionButton("COPY PATH", "Copy the replay file path to the clipboard.", delegate
            {
                ui.CopyReplayPath(replay);
            });
            panel.Add(copyPathButton);

            Button exportButton = CreateActionButton("EXPORT", "Copy this .puckreplay file to Exports and copy the exported path.", delegate
            {
                ui.ExportReplay(replay);
            });
            panel.Add(exportButton);

            Button deleteButton = ReplayUiTools.CreateButton("DELETE", delegate
            {
                ShowDeleteConfirmation(ui, replay, panel);
            });
            deleteButton.tooltip = "Delete the replay file and its local summary cache.";
            StyleCompactActionButton(deleteButton);
            panel.Add(deleteButton);
        }

        private static Button CreateActionButton(string text, Action action)
        {
            return CreateActionButton(text, null, action);
        }

        private static Button CreateActionButton(string text, string tooltip, Action action)
        {
            Button button = ReplayUiTools.CreateButton(text, action);
            button.tooltip = tooltip ?? string.Empty;
            StyleCompactActionButton(button);
            return button;
        }

        private static void StyleCompactActionButton(Button button)
        {
            button.style.width = 82f;
            button.style.minWidth = 82f;
            button.style.height = 28f;
            button.style.minHeight = 28f;
            button.style.marginLeft = 4f;
            button.style.paddingLeft = 4f;
            button.style.paddingRight = 4f;
            button.style.paddingTop = 4f;
            button.style.paddingBottom = 4f;
            button.style.fontSize = 12f;
        }

        private static void ShowRenameForm(ReplayModUiService ui, ReplayFileSummary replay, VisualElement panel)
        {
            panel.Clear();
            panel.style.justifyContent = Justify.SpaceBetween;

            TextField nameField = new TextField();
            nameField.value = string.IsNullOrEmpty(replay.DisplayName) ? GetServerTitle(replay) : replay.DisplayName;
            nameField.style.flexGrow = 1f;
            nameField.style.marginRight = 8f;
            nameField.style.height = 32f;
            nameField.style.color = Color.white;
            Action saveRename = delegate
            {
                ui.RenameReplay(replay, nameField.value);
            };
            nameField.RegisterCallback<KeyDownEvent>(delegate(KeyDownEvent evt)
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    saveRename();
                    evt.StopPropagation();
                }
            });
            nameField.RegisterCallback<AttachToPanelEvent>(delegate
            {
                VisualElement input = nameField.Q<VisualElement>(null, "unity-base-text-field__input");
                if (input != null)
                {
                    input.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);
                    input.style.color = Color.white;
                    input.style.paddingLeft = 8f;
                    input.style.paddingRight = 8f;
                }
            });
            panel.Add(nameField);
            nameField.schedule.Execute((Action)delegate
            {
                nameField.Focus();
                nameField.SelectAll();
            });

            VisualElement buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.alignItems = Align.Center;
            panel.Add(buttons);

            Button clearButton = ReplayUiTools.CreateButton("CLEAR", delegate
            {
                ui.RenameReplay(replay, string.Empty);
            });
            clearButton.tooltip = "Remove this replay's local display name.";
            clearButton.style.width = 84f;
            clearButton.style.minWidth = 84f;
            clearButton.style.marginRight = 8f;
            buttons.Add(clearButton);

            Button cancelButton = ReplayUiTools.CreateButton("CANCEL", delegate
            {
                ShowDefaultActions(ui, replay, panel);
            });
            cancelButton.tooltip = "Cancel rename.";
            cancelButton.style.width = 96f;
            cancelButton.style.minWidth = 96f;
            cancelButton.style.marginRight = 8f;
            buttons.Add(cancelButton);

            Button saveButton = ReplayUiTools.CreateButton("SAVE", saveRename);
            saveButton.tooltip = "Save this local display name.";
            saveButton.style.width = 84f;
            saveButton.style.minWidth = 84f;
            buttons.Add(saveButton);
        }

        private static void ShowDeleteConfirmation(ReplayModUiService ui, ReplayFileSummary replay, VisualElement panel)
        {
            panel.Clear();
            panel.style.justifyContent = Justify.SpaceBetween;

            Label confirmation = ReplayUiTools.CreateConfigurationLabel("Delete this replay?");
            confirmation.style.color = Color.white;
            confirmation.style.fontSize = 13f;
            panel.Add(confirmation);

            VisualElement buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.alignItems = Align.Center;
            panel.Add(buttons);

            Button cancelButton = ReplayUiTools.CreateButton("CANCEL", delegate
            {
                ShowDefaultActions(ui, replay, panel);
            });
            cancelButton.tooltip = "Keep this replay.";
            cancelButton.style.width = 100f;
            cancelButton.style.minWidth = 100f;
            cancelButton.style.marginRight = 8f;
            buttons.Add(cancelButton);

            Button confirmButton = ReplayUiTools.CreateButton("DELETE REPLAY", delegate
            {
                ui.DeleteReplay(replay);
            });
            confirmButton.tooltip = "Permanently delete this replay file.";
            confirmButton.style.width = 150f;
            confirmButton.style.minWidth = 150f;
            confirmButton.style.backgroundColor = new Color(0.55f, 0.05f, 0.06f, 1f);
            buttons.Add(confirmButton);
        }

        private static string GetDisplayTitle(ReplayFileSummary replay)
        {
            string title = string.IsNullOrEmpty(replay.DisplayName) ? GetServerTitle(replay) : replay.DisplayName;
            return title;
        }

        private static string GetServerTitle(ReplayFileSummary replay)
        {
            return string.IsNullOrEmpty(replay.ServerName) ? "Unknown Server" : replay.ServerName;
        }

        private static string FormatDuration(float seconds)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(Math.Max(0f, seconds));
            if (timeSpan.TotalHours >= 1.0)
            {
                return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)timeSpan.TotalHours, timeSpan.Minutes, timeSpan.Seconds);
            }

            return string.Format("{0:D2}:{1:D2}", timeSpan.Minutes, timeSpan.Seconds);
        }
    }
}
