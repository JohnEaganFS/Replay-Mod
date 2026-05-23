using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace PuckReplayMod
{
    public class ReplayModUiService
    {
        private readonly ReplayModSettings settings;
        private readonly ClientReplayRecorder recorder;
        private readonly ReplayStorageService storage;
        private readonly ReplayFileReader reader;
        private readonly ReplayPlaybackService playback;
        private readonly List<Button> sectionButtons = new List<Button>();
        private readonly Dictionary<string, ulong> playbackCameraTargets = new Dictionary<string, ulong>();
        private readonly Dictionary<VisualElement, StyleEnum<Visibility>> captureHiddenElements = new Dictionary<VisualElement, StyleEnum<Visibility>>();
        private readonly List<string> sectionNames = new List<string>
        {
            "Library",
            "Recording",
            "Playback",
            "Display / Interface",
            "Hotkeys",
            "Storage",
            "Advanced",
            "About / Updates"
        };

        private VisualElement root;
        private VisualElement managerPanel;
        private VisualElement contentHost;
        private VisualElement content;
        private VisualElement playbackControlsAnchor;
        private VisualElement playbackControlsPanel;
        private VisualElement playbackTimelineTrack;
        private VisualElement playbackProgressBar;
        private Label playbackTimelineTooltip;
        private Button playbackPlayPauseButton;
        private Button playbackCaptureModeButton;
        private PlaybackPlayPauseIcon playbackPlayPauseIcon;
        private Label playbackTimeLabel;
        private PopupField<string> playbackSpeedDropdown;
        private PopupField<string> playbackGameDropdown;
        private PopupField<string> playbackCameraModeDropdown;
        private PopupField<string> playbackCameraTargetDropdown;
        private Label statusLabel;
        private Label timelineLabel;
        private bool isManagerVisible;
        private bool playbackUiInputActive;
        private bool playbackUiMouseRequiredApplied;
        private bool mainMenuButtonAttached;
        private bool pauseMenuButtonAttached;
        private Button mainMenuReplayButton;
        private Button pauseMenuReplayButton;
        private Label mainMenuReplayUpdateBadge;
        private Label pauseMenuReplayUpdateBadge;
        private bool isMainMenuVisible = true;
        private string selectedSection = "Library";
        private bool isReplayImportPanelVisible;
        private string replayImportStatusMessage = string.Empty;
        private string replayLibraryStatusMessage = string.Empty;
        private bool replayLibraryStatusIsError;
        private string replayLibrarySearchText = string.Empty;
        private string replayLibraryFilterMode = "All";
        private string replayLibrarySortCategory = "Recorded date";
        private string replayLibrarySortDirection = "Descending";
        private float nextReplayIndexRealtime;
        private float playbackDropdownInteractionUntil;
        private float nextPlaybackUiDebugRealtime;
        private Task<ReplayUpdateCheckResult> updateCheckTask;
        private Label updateStatusLabel;
        private Label updatePatchNotesLabel;
        private Button updateBadgeButton;
        private Button storageBadgeButton;
        private ReplayUpdateStatus updateStatus = ReplayUpdateStatus.Unknown;
        private string updateStatusMessage = "Check whether a newer Replay Mod version is available.";
        private string updatePatchNotesMessage = "Check for updates to load the latest patch notes.";
        private string updateDownloadUrl;
        private bool hasStartedAutomaticUpdateCheck;
        private bool captureModeActive;
        private Action heldPlaybackControlAction;
        private float nextHeldPlaybackControlRealtime;
        private bool suppressNextPlaybackControlClick;
        private float suppressNextPlaybackControlClickUntil;
        private string loadedPlaybackTimelineFilePath;
        private readonly List<PlaybackTimelineMarkerData> playbackTimelineMarkers = new List<PlaybackTimelineMarkerData>();
        private readonly Dictionary<string, ReplayGameSegmentSummary> playbackGameSegments = new Dictionary<string, ReplayGameSegmentSummary>();
        private readonly Dictionary<string, VisualElement> playbackGameSegmentHighlights = new Dictionary<string, VisualElement>();
        private string selectedPlaybackGameChoice = "Full replay";

        internal ReplayModSettings Settings { get { return this.settings; } }
        internal ClientReplayRecorder Recorder { get { return this.recorder; } }
        internal ReplayStorageService Storage { get { return this.storage; } }
        internal ReplayFileReader Reader { get { return this.reader; } }
        internal ReplayPlaybackService Playback { get { return this.playback; } }
        internal VisualElement ReplayList { get; set; }
        internal Label StorageLabel { get; set; }
        internal Label StorageUsageLabel { get; set; }
        internal Label PlaybackLabel { get; set; }
        internal Label StatusLabel { get { return this.statusLabel; } }
        internal Label TimelineLabel { get { return this.timelineLabel; } }
        internal bool CaptureModeActive { get { return this.captureModeActive; } }
        internal bool IsReplayImportPanelVisible { get { return this.isReplayImportPanelVisible; } }
        internal string ReplayImportStatusMessage { get { return this.replayImportStatusMessage; } }
        internal string ReplayLibraryStatusMessage { get { return this.replayLibraryStatusMessage; } }
        internal bool ReplayLibraryStatusIsError { get { return this.replayLibraryStatusIsError; } }
        internal string ReplayLibrarySearchText { get { return this.replayLibrarySearchText; } }
        internal string ReplayLibraryFilterMode { get { return this.replayLibraryFilterMode; } }
        internal string ReplayLibrarySortCategory { get { return this.replayLibrarySortCategory; } }
        internal string ReplayLibrarySortDirection { get { return this.replayLibrarySortDirection; } }

        public ReplayModUiService(ReplayModSettings settings, ClientReplayRecorder recorder, ReplayStorageService storage, ReplayFileReader reader, ReplayPlaybackService playback)
        {
            this.settings = settings;
            this.recorder = recorder;
            this.storage = storage;
            this.reader = reader;
            this.playback = playback;
            this.recorder.RecordingStateChanged += this.RefreshStatusIndicator;
            this.recorder.TickAdvanced += this.RefreshStatusIndicator;
        }

        public void Initialize()
        {
            EventManager.AddEventListener("Event_OnClientStarted", this.Event_HideManager);
            EventManager.AddEventListener("Event_OnClientStopped", this.Event_HideManager);
            EventManager.AddEventListener("Event_OnMainMenuShow", this.Event_OnMainMenuShow);
            EventManager.AddEventListener("Event_OnMainMenuHide", this.Event_OnMainMenuHide);
            EventManager.AddEventListener("Event_OnMainMenuClickPlay", this.Event_HideManager);
            EventManager.AddEventListener("Event_OnPauseMenuClickSelectTeam", this.Event_HideManager);
            EventManager.AddEventListener("Event_OnPauseMenuClickSelectPosition", this.Event_HideManager);
            EventManager.AddEventListener("Event_OnPauseMenuClickServerBrowser", this.Event_HideManager);
            EventManager.AddEventListener("Event_OnPauseMenuClickDisconnect", this.Event_HideManager);
            EventManager.AddEventListener("Event_OnReplayManagerClickClose", this.Event_HideManager);
        }

        public void Dispose()
        {
            EventManager.RemoveEventListener("Event_OnClientStarted", this.Event_HideManager);
            EventManager.RemoveEventListener("Event_OnClientStopped", this.Event_HideManager);
            EventManager.RemoveEventListener("Event_OnMainMenuShow", this.Event_OnMainMenuShow);
            EventManager.RemoveEventListener("Event_OnMainMenuHide", this.Event_OnMainMenuHide);
            EventManager.RemoveEventListener("Event_OnMainMenuClickPlay", this.Event_HideManager);
            EventManager.RemoveEventListener("Event_OnPauseMenuClickSelectTeam", this.Event_HideManager);
            EventManager.RemoveEventListener("Event_OnPauseMenuClickSelectPosition", this.Event_HideManager);
            EventManager.RemoveEventListener("Event_OnPauseMenuClickServerBrowser", this.Event_HideManager);
            EventManager.RemoveEventListener("Event_OnPauseMenuClickDisconnect", this.Event_HideManager);
            EventManager.RemoveEventListener("Event_OnReplayManagerClickClose", this.Event_HideManager);
            this.recorder.RecordingStateChanged -= this.RefreshStatusIndicator;
            this.recorder.TickAdvanced -= this.RefreshStatusIndicator;
            this.RestoreCaptureHiddenElements();

            if (this.root != null && this.root.parent != null)
            {
                this.root.parent.Remove(this.root);
            }

            this.root = null;
            this.managerPanel = null;
            this.contentHost = null;
            this.content = null;
            this.playbackControlsAnchor = null;
            this.playbackControlsPanel = null;
            this.playbackTimelineTrack = null;
            this.playbackProgressBar = null;
            this.playbackTimelineTooltip = null;
            this.playbackPlayPauseButton = null;
            this.playbackCaptureModeButton = null;
            this.playbackPlayPauseIcon = null;
            this.playbackTimeLabel = null;
            this.playbackSpeedDropdown = null;
            this.playbackGameDropdown = null;
            this.playbackCameraModeDropdown = null;
            this.playbackCameraTargetDropdown = null;
            this.playbackCameraTargets.Clear();
            this.statusLabel = null;
            this.timelineLabel = null;
            this.playbackUiInputActive = false;
            this.playbackUiMouseRequiredApplied = false;
            this.loadedPlaybackTimelineFilePath = null;
            this.playbackTimelineMarkers.Clear();
            this.playbackGameSegments.Clear();
            this.playbackGameSegmentHighlights.Clear();
            this.selectedPlaybackGameChoice = "Full replay";
            this.ReplayList = null;
            this.StorageLabel = null;
            this.StorageUsageLabel = null;
            this.PlaybackLabel = null;
            this.updateStatusLabel = null;
            this.updateBadgeButton = null;
            this.storageBadgeButton = null;
            this.sectionButtons.Clear();
            this.isManagerVisible = false;
            this.isMainMenuVisible = true;
            this.mainMenuButtonAttached = false;
            this.pauseMenuButtonAttached = false;
            this.mainMenuReplayButton = null;
            this.pauseMenuReplayButton = null;
            this.mainMenuReplayUpdateBadge = null;
            this.pauseMenuReplayUpdateBadge = null;
            this.captureModeActive = false;
        }

        public void Tick()
        {
            this.PollManagerCloseHotkey();
            this.PollPlaybackUiInputHotkey();
            this.PollCaptureModeHotkey();
            this.PollDisplayHotkeys();
            this.PollHeldPlaybackControl();
            this.RefreshStatusIndicator();
            this.RefreshPlaybackStatus();
            this.ResetCaptureModeWhenPlaybackEnds();
            this.RefreshPlaybackControls();
            this.ApplyCaptureModeVisibility();
            this.TickReplayLibraryIndex();
            this.PollUpdateCheck();
        }

        internal bool IsPlaybackUiInputActive
        {
            get { return this.playbackUiInputActive && this.playback != null && this.playback.IsPlaybackActive; }
        }

        public void TryAttachToExistingUi()
        {
            UIManager uiManager = MonoBehaviourSingleton<UIManager>.Instance;
            if (uiManager == null || uiManager.RootVisualElement == null)
            {
                return;
            }

            this.AttachRoot(uiManager);
            this.AttachMainMenuButton(uiManager.RootVisualElement);
            this.AttachPauseMenuButton(uiManager.RootVisualElement);
        }

        public void AttachRoot(UIManager uiManager)
        {
            if (uiManager == null || uiManager.RootVisualElement == null)
            {
                return;
            }

            if (this.root != null && this.root.parent == null)
            {
                this.root = null;
                this.managerPanel = null;
                this.contentHost = null;
                this.content = null;
                this.playbackControlsAnchor = null;
                this.playbackControlsPanel = null;
                this.playbackTimelineTrack = null;
                this.playbackProgressBar = null;
                this.playbackTimelineTooltip = null;
                this.playbackPlayPauseButton = null;
                this.playbackCaptureModeButton = null;
                this.playbackPlayPauseIcon = null;
                this.playbackTimeLabel = null;
                this.playbackSpeedDropdown = null;
                this.playbackGameDropdown = null;
                this.playbackCameraModeDropdown = null;
                this.playbackCameraTargetDropdown = null;
                this.playbackCameraTargets.Clear();
                this.statusLabel = null;
                this.timelineLabel = null;
                this.playbackUiInputActive = false;
                this.playbackUiMouseRequiredApplied = false;
                this.loadedPlaybackTimelineFilePath = null;
                this.playbackTimelineMarkers.Clear();
                this.playbackGameSegments.Clear();
                this.playbackGameSegmentHighlights.Clear();
                this.selectedPlaybackGameChoice = "Full replay";
                this.ReplayList = null;
                this.StorageLabel = null;
                this.StorageUsageLabel = null;
                this.PlaybackLabel = null;
                this.updateStatusLabel = null;
                this.updateBadgeButton = null;
                this.storageBadgeButton = null;
                this.RestoreCaptureHiddenElements();
                this.sectionButtons.Clear();
                this.isManagerVisible = false;
                this.mainMenuButtonAttached = false;
                this.pauseMenuButtonAttached = false;
                this.mainMenuReplayButton = null;
                this.pauseMenuReplayButton = null;
                this.mainMenuReplayUpdateBadge = null;
                this.pauseMenuReplayUpdateBadge = null;
            }

            if (this.root != null)
            {
                return;
            }

            this.root = new VisualElement
            {
                name = "PuckReplayModRoot"
            };
            this.root.style.position = Position.Absolute;
            this.root.style.left = 0f;
            this.root.style.right = 0f;
            this.root.style.top = 0f;
            this.root.style.bottom = 0f;
            this.root.pickingMode = PickingMode.Ignore;

            this.CreateStatusIndicator();
            this.CreateTimelineIndicator();
            this.CreatePlaybackControlsPanel();
            this.CreateManagerPanel();
            uiManager.RootVisualElement.Add(this.root);
            this.SyncMainMenuVisibilityFromUi();
            this.RefreshStatusIndicator();
            this.RefreshTimelineIndicator();
        }

        public void AttachMainMenuButton(VisualElement rootVisualElement)
        {
            if (this.mainMenuButtonAttached)
            {
                return;
            }

            VisualElement mainMenu = rootVisualElement != null ? rootVisualElement.Q<VisualElement>("MainMenu") : null;
            Button button;
            Label badge;
            this.mainMenuButtonAttached = this.AttachReplayButton(mainMenu, "PuckReplayModMainMenuButton", out button, out badge);
            this.mainMenuReplayButton = button;
            this.mainMenuReplayUpdateBadge = badge;
            this.RefreshMenuUpdateBadges();
            this.StartAutomaticUpdateCheck();
        }

        public void AttachPauseMenuButton(VisualElement rootVisualElement)
        {
            if (this.pauseMenuButtonAttached)
            {
                return;
            }

            VisualElement pauseMenu = rootVisualElement != null ? rootVisualElement.Q<VisualElement>("PauseMenu") : null;
            Button button;
            Label badge;
            this.pauseMenuButtonAttached = this.AttachReplayButton(pauseMenu, "PuckReplayModPauseMenuButton", out button, out badge);
            this.pauseMenuReplayButton = button;
            this.pauseMenuReplayUpdateBadge = badge;
            this.RefreshMenuUpdateBadges();
            this.StartAutomaticUpdateCheck();
        }

        internal void SaveSettings()
        {
            this.settings.Save();
            this.RefreshLibraryText();
            this.RefreshStorageUsage();
        }

        internal void RefreshLibraryText()
        {
            if (this.StorageLabel == null)
            {
                return;
            }

            int replayCount = 0;
            if (Directory.Exists(this.storage.ReplaysDirectory))
            {
                replayCount = Directory.GetFiles(this.storage.ReplaysDirectory, "*" + ReplayModConstants.ReplayFileExtension).Length;
            }

            string recordingState = this.recorder.IsRecording ? "Recording now" : (this.settings.RecordingMode == ReplayRecordingMode.AutomaticSave ? "Ready to record" : "Manual recording mode");
            this.StorageLabel.text = "Saved replays: " + replayCount + "\nStatus: " + recordingState + "\nRecord rate: " + ReplayRecordingSettingsSection.FormatCaptureRate(this.settings.CaptureTickRate);
        }

        internal void RefreshReplayList()
        {
            ReplayLibrarySection.RefreshReplayList(this);
        }

        internal void SetReplayLibrarySearchText(string value)
        {
            this.replayLibrarySearchText = value ?? string.Empty;
            this.RefreshReplayList();
        }

        internal void SetReplayLibraryFilterMode(string value)
        {
            this.replayLibraryFilterMode = string.IsNullOrEmpty(value) ? "All" : value;
            this.RefreshReplayList();
        }

        internal void SetReplayLibrarySortCategory(string value)
        {
            this.replayLibrarySortCategory = string.IsNullOrEmpty(value) ? "Recorded date" : value;
            this.RefreshReplayList();
        }

        internal void SetReplayLibrarySortDirection(string value)
        {
            this.replayLibrarySortDirection = string.IsNullOrEmpty(value) ? "Descending" : value;
            this.RefreshReplayList();
        }

        internal void RefreshStorageUsage()
        {
            ReplayStorageSection.RefreshStorageUsage(this);
            this.RefreshStorageBadge();
        }

        internal void PlayReplay(string filePath)
        {
            if (this.recorder.IsRecording)
            {
                ReplayModLog.Warning("Cannot start replay playback while recording a live session.");
                return;
            }

            try
            {
                this.playback.Play(filePath);
                this.RefreshPlaybackStatus();
                this.HideManager();
            }
            catch (Exception exception)
            {
                ReplayModLog.Warning("Failed to play replay " + filePath + ": " + exception.Message);
            }
        }

        internal void DeleteReplay(ReplayFileSummary replay)
        {
            if (replay == null || string.IsNullOrEmpty(replay.FilePath))
            {
                return;
            }

            try
            {
                this.storage.DeleteReplay(replay.FilePath);
                this.RefreshLibraryText();
                this.RefreshReplayList();
                this.RefreshStorageUsage();
            }
            catch (Exception exception)
            {
                ReplayModLog.Warning("Failed to delete replay " + replay.FilePath + ": " + exception.Message);
            }
        }

        internal void RenameReplay(ReplayFileSummary replay, string displayName)
        {
            if (replay == null || string.IsNullOrEmpty(replay.FilePath))
            {
                return;
            }

            try
            {
                this.storage.SetReplayDisplayName(replay.FilePath, displayName);
                this.RefreshReplayList();
            }
            catch (Exception exception)
            {
                ReplayModLog.Warning("Failed to rename replay " + replay.FilePath + ": " + exception.Message);
            }
        }

        internal void SetReplayFavorite(ReplayFileSummary replay, bool isFavorite)
        {
            if (replay == null || string.IsNullOrEmpty(replay.FilePath))
            {
                return;
            }

            try
            {
                this.storage.SetReplayFavorite(replay.FilePath, isFavorite);
                this.RefreshReplayList();
            }
            catch (Exception exception)
            {
                ReplayModLog.Warning("Failed to update replay favorite " + replay.FilePath + ": " + exception.Message);
            }
        }

        internal void OpenReplayLocation(ReplayFileSummary replay)
        {
            if (replay == null || string.IsNullOrEmpty(replay.FilePath))
            {
                return;
            }

            try
            {
                if (File.Exists(replay.FilePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = "/select,\"" + replay.FilePath + "\"",
                        UseShellExecute = true
                    });
                    return;
                }

                if (Directory.Exists(this.storage.ReplaysDirectory))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = this.storage.ReplaysDirectory,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception exception)
            {
                ReplayModLog.Warning("Failed to open replay location " + replay.FilePath + ": " + exception.Message);
            }
        }

        internal void CopyReplayPath(ReplayFileSummary replay)
        {
            if (replay == null || string.IsNullOrEmpty(replay.FilePath))
            {
                return;
            }

            GUIUtility.systemCopyBuffer = replay.FilePath;
            ReplayModLog.Info("Copied replay path to clipboard: " + replay.FilePath);
        }

        internal void ToggleReplayImportPanel()
        {
            this.isReplayImportPanelVisible = !this.isReplayImportPanelVisible;
            if (this.isReplayImportPanelVisible)
            {
                this.replayImportStatusMessage = "Put .puckreplay files in the Imports folder, then click Import Files.";
                this.OpenImportsFolder();
            }

            this.RefreshReplayList();
        }

        internal void ImportReplaysFromImportsFolder()
        {
            try
            {
                ReplayImportBatchResult result = this.storage.ImportReplaysFromImportsFolder();
                if (result.FoundCount == 0)
                {
                    this.SetReplayLibraryStatus("No .puckreplay files were found in the Imports folder.", false);
                    this.replayImportStatusMessage = "No .puckreplay files were found in the Imports folder.";
                    this.RefreshReplayList();
                    return;
                }

                if (result.FailedCount > 0)
                {
                    string firstError = result.Errors != null && result.Errors.Count > 0 ? " " + result.Errors[0] : string.Empty;
                    this.SetReplayLibraryStatus("Imported " + result.ImportedCount + " of " + result.FoundCount + " replay files." + firstError, true);
                    this.replayImportStatusMessage = this.replayLibraryStatusMessage;
                }
                else
                {
                    this.SetReplayLibraryStatus("Imported " + result.ImportedCount + " replay file" + (result.ImportedCount == 1 ? string.Empty : "s") + ".", false);
                    this.replayImportStatusMessage = this.replayLibraryStatusMessage;
                    this.isReplayImportPanelVisible = false;
                }

                this.RefreshLibraryText();
                this.RefreshReplayList();
                this.RefreshStorageUsage();
            }
            catch (Exception exception)
            {
                this.replayImportStatusMessage = "Import failed: " + exception.Message;
                this.SetReplayLibraryStatus(this.replayImportStatusMessage, true);
                ReplayModLog.Warning("Failed to import replay: " + exception.Message);
                this.RefreshReplayList();
            }
        }

        internal void ExportReplay(ReplayFileSummary replay)
        {
            if (replay == null || string.IsNullOrEmpty(replay.FilePath))
            {
                return;
            }

            try
            {
                string exportedPath = this.storage.ExportReplay(replay.FilePath, string.IsNullOrEmpty(replay.DisplayName) ? replay.ServerName : replay.DisplayName);
                GUIUtility.systemCopyBuffer = exportedPath;
                this.SetReplayLibraryStatus("Exported " + Path.GetFileName(exportedPath) + ". Path copied to clipboard.", false);
                this.OpenExportsFolder();
                this.RefreshReplayList();
            }
            catch (Exception exception)
            {
                this.SetReplayLibraryStatus("Export failed: " + exception.Message, true);
                ReplayModLog.Warning("Failed to export replay " + replay.FilePath + ": " + exception.Message);
                this.RefreshReplayList();
            }
        }

        internal void OpenImportsFolder()
        {
            this.OpenFolder(this.storage.ImportsDirectory);
        }

        internal void OpenExportsFolder()
        {
            this.OpenFolder(this.storage.ExportsDirectory);
        }

        private void OpenFolder(string folderPath)
        {
            try
            {
                if (!string.IsNullOrEmpty(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = folderPath,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception exception)
            {
                ReplayModLog.Warning("Failed to open file location: " + exception.Message);
            }
        }

        private void SetReplayLibraryStatus(string message, bool isError)
        {
            this.replayLibraryStatusMessage = message ?? string.Empty;
            this.replayLibraryStatusIsError = isError;
        }

        internal void BindUpdateLabels(Label statusLabel, Label patchNotesLabel)
        {
            this.updateStatusLabel = statusLabel;
            this.updatePatchNotesLabel = patchNotesLabel;
            if (this.updateStatusLabel != null)
            {
                this.updateStatusLabel.text = this.updateStatusMessage;
            }

            if (this.updatePatchNotesLabel != null)
            {
                this.updatePatchNotesLabel.text = this.updatePatchNotesMessage;
            }
        }

        internal void CheckForUpdates(Label statusLabel)
        {
            this.updateStatusLabel = statusLabel;
            if (this.updateCheckTask == null || this.updateCheckTask.IsCompleted)
            {
                this.updateStatus = ReplayUpdateStatus.Unknown;
                this.updateDownloadUrl = null;
                this.SetUpdatePatchNotes("Check for updates to load the latest patch notes.");
                this.RefreshUpdateBadge();
            }

            if (this.updateCheckTask != null && !this.updateCheckTask.IsCompleted)
            {
                this.SetUpdateStatus("Already checking for updates...");
                return;
            }

            if (string.IsNullOrEmpty(ReplayModConstants.UpdateManifestUrl))
            {
                this.SetUpdateStatus("Update checking is not configured yet. Set ReplayModConstants.UpdateManifestUrl to a GitHub raw manifest URL before release.");
                return;
            }

            this.SetUpdateStatus("Checking for updates...");
            this.updateCheckTask = Task.Run(delegate
            {
                return CheckForUpdatesCore();
            });
        }

        internal string GetUpdateStatusMessage()
        {
            return this.updateStatusMessage;
        }

        internal string GetUpdatePatchNotesMessage()
        {
            return this.updatePatchNotesMessage;
        }

        internal void OpenUpdateDownloadUrl()
        {
            string url = !string.IsNullOrEmpty(this.updateDownloadUrl)
                ? this.updateDownloadUrl
                : ReplayModConstants.UpdateDownloadUrl;
            if (string.IsNullOrEmpty(url))
            {
                this.SetUpdateStatus("No update page is configured yet.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception exception)
            {
                ReplayModLog.Warning("Failed to open update URL " + url + ": " + exception.Message);
                this.SetUpdateStatus("Could not open update page.");
            }
        }

        internal void RefreshPlaybackStatus()
        {
            this.RefreshTimelineIndicator();
            if (this.PlaybackLabel == null)
            {
                return;
            }

            if (this.playback.IsPlaying)
            {
                this.PlaybackLabel.text = "Watching replay: " + this.FormatPlaybackTime(this.playback.CurrentTick) + " / " + this.FormatPlaybackTime(this.playback.TotalTicks);
                return;
            }

            this.PlaybackLabel.text = "Not watching a replay.";
        }

        private void RefreshPlaybackControls()
        {
            if (this.playbackControlsPanel == null)
            {
                return;
            }

            bool shouldShow = this.playback != null && this.playback.IsPlaybackActive && !this.isManagerVisible && !this.IsCaptureModeHidingPlaybackControls();
            this.playbackControlsPanel.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
            if (!shouldShow)
            {
                if (this.playback == null || !this.playback.IsPlaybackActive)
                {
                    this.playbackUiInputActive = false;
                    this.ClearPlaybackTimelineMarkers();
                }

                this.SetPlaybackUiMouseRequired(false);
                return;
            }

            bool controlsInteractive = this.playbackUiInputActive;
            this.playbackControlsPanel.pickingMode = PickingMode.Position;
            if (this.playbackProgressBar != null)
            {
                this.playbackProgressBar.pickingMode = PickingMode.Ignore;
            }

            this.SetPlaybackUiMouseRequired(controlsInteractive);

            int currentTick = Math.Max(0, this.playback.CurrentTick);
            int totalTicks = Math.Max(currentTick, this.playback.TotalTicks);
            this.RefreshPlaybackTimelineMarkers(totalTicks);
            if (this.playbackPlayPauseIcon != null)
            {
                this.playbackPlayPauseIcon.SetMode(this.playback.IsPaused ? PlaybackPlayPauseIconMode.Play : PlaybackPlayPauseIconMode.Pause);
            }

            if (this.playbackTimeLabel != null)
            {
                this.playbackTimeLabel.text = this.FormatPlaybackTime(currentTick) + " / " + this.FormatPlaybackTime(totalTicks);
            }

            if (this.playbackCaptureModeButton != null)
            {
                this.playbackCaptureModeButton.text = this.GetPlaybackCaptureModeButtonText();
            }

            if (this.playbackProgressBar != null)
            {
                float percent = totalTicks > 0 ? Mathf.Clamp01(currentTick / (float)totalTicks) * 100f : 0f;
                this.playbackProgressBar.style.width = new StyleLength(new Length(percent, LengthUnit.Percent));
            }

            this.RefreshPlaybackGameSegmentHighlight(totalTicks);

            bool shouldSyncDropdowns = !this.IsPlaybackDropdownInteractionActive();
            if (this.playbackGameDropdown != null && shouldSyncDropdowns && this.playbackGameDropdown.value != this.selectedPlaybackGameChoice)
            {
                this.playbackGameDropdown.SetValueWithoutNotify(this.selectedPlaybackGameChoice);
            }

            if (this.playbackSpeedDropdown != null && shouldSyncDropdowns)
            {
                string speedText = FormatSpeed(this.playback.PlaybackSpeed);
                if (this.playbackSpeedDropdown.value != speedText)
                {
                    this.playbackSpeedDropdown.SetValueWithoutNotify(speedText);
                }
            }

            if (this.playbackCameraModeDropdown != null && shouldSyncDropdowns)
            {
                string cameraModeText = FormatCameraMode(this.playback.CameraMode);
                if (this.playbackCameraModeDropdown.value != cameraModeText)
                {
                    this.playbackCameraModeDropdown.SetValueWithoutNotify(cameraModeText);
                }
            }

            this.RefreshPlaybackCameraTargetDropdown(shouldSyncDropdowns);
        }

        private void OnPlaybackTimelinePointerDown(PointerDownEvent evt)
        {
            if (!this.IsPlaybackControlInputAllowed())
            {
                this.LogPlaybackUiDebug("Blocked playback timeline pointer down: " + this.GetPlaybackInputDebugState(evt), true);
                evt.StopImmediatePropagation();
                return;
            }

            VisualElement target = evt.currentTarget as VisualElement;
            if (target == null || this.playback == null || !this.playback.IsPlaying || this.playback.TotalTicks <= 0)
            {
                return;
            }

            float normalized;
            string positionDebug;
            if (!this.TryGetPlaybackTimelineNormalizedPosition(evt.position, target, out normalized, out positionDebug))
            {
                this.LogPlaybackUiDebug("Ignored playback timeline pointer down with invalid coordinates: " + positionDebug, false);
                return;
            }

            int maxPlayableTick = Math.Max(0, this.playback.TotalTicks - 2);
            int targetTick = Mathf.Clamp(Mathf.RoundToInt(maxPlayableTick * normalized), 0, maxPlayableTick);
            this.LogPlaybackUiDebug(
                "Playback timeline seek: normalized=" + normalized.ToString("0.000", CultureInfo.InvariantCulture) +
                ", targetTick=" + targetTick +
                ", currentTick=" + this.playback.CurrentTick +
                ", totalTicks=" + this.playback.TotalTicks +
                ", " + positionDebug,
                false);
            this.SelectPlaybackGameForTick(targetTick);
            this.playback.SeekToTick(targetTick);
            this.RefreshPlaybackControls();
            this.RefreshPlaybackStatus();
            evt.StopPropagation();
        }

        private void OnPlaybackTimelinePointerMove(PointerMoveEvent evt)
        {
            if (this.playbackTimelineTrack == null || this.playbackTimelineTooltip == null)
            {
                this.HidePlaybackTimelineTooltip();
                return;
            }

            float width = this.playbackTimelineTrack.contentRect.width;
            if (width <= 0f)
            {
                this.HidePlaybackTimelineTooltip();
                return;
            }

            float normalized;
            string positionDebug;
            if (!this.TryGetPlaybackTimelineNormalizedPosition(evt.position, this.playbackTimelineTrack, out normalized, out positionDebug))
            {
                this.HidePlaybackTimelineTooltip();
                return;
            }

            float pointerX = Mathf.Clamp(normalized * width, 0f, width);
            PlaybackTimelineMarkerData nearest = null;
            float nearestDistance = 10f;
            for (int i = 0; i < this.playbackTimelineMarkers.Count; i++)
            {
                PlaybackTimelineMarkerData marker = this.playbackTimelineMarkers[i];
                float markerX = marker.NormalizedTick * width;
                float distance = Mathf.Abs(pointerX - markerX);
                if (distance <= nearestDistance)
                {
                    nearest = marker;
                    nearestDistance = distance;
                }
            }

            if (nearest == null)
            {
                ReplayGameSegmentSummary segment = this.GetPlaybackGameSegmentAtNormalizedPosition(width > 0f ? pointerX / width : 0f);
                if (segment == null)
                {
                    this.HidePlaybackTimelineTooltip();
                    return;
                }

                this.ShowPlaybackTimelineTooltip(this.GetPlaybackGameSegmentTooltip(segment), pointerX);
                return;
            }

            this.ShowPlaybackTimelineTooltip(nearest.Tooltip, pointerX);
        }

        private void ShowPlaybackTimelineTooltip(string text, float pointerX)
        {
            if (this.playbackTimelineTooltip == null || this.playbackTimelineTrack == null || this.playbackControlsPanel == null)
            {
                return;
            }

            this.playbackTimelineTooltip.text = text ?? string.Empty;
            this.playbackTimelineTooltip.style.display = DisplayStyle.Flex;
            float tooltipWidth = this.playbackTimelineTooltip.resolvedStyle.width > 0f ? this.playbackTimelineTooltip.resolvedStyle.width : 180f;
            float trackLeft = this.playbackTimelineTrack.worldBound.xMin - this.playbackControlsPanel.worldBound.xMin;
            float left = Mathf.Clamp(trackLeft + pointerX - tooltipWidth * 0.5f, 4f, Mathf.Max(4f, this.playbackControlsPanel.contentRect.width - tooltipWidth - 4f));
            this.playbackTimelineTooltip.style.left = left;
        }

        private void OnPlaybackTimelinePointerLeave(PointerLeaveEvent evt)
        {
            this.HidePlaybackTimelineTooltip();
        }

        private bool TryGetPlaybackTimelineNormalizedPosition(Vector3 panelPosition, VisualElement timelineTrack, out float normalized, out string debug)
        {
            normalized = 0f;
            debug = "track unavailable";
            if (timelineTrack == null)
            {
                return false;
            }

            Rect worldBound = timelineTrack.worldBound;
            float width = worldBound.width > 0f ? worldBound.width : timelineTrack.contentRect.width;
            if (width <= 0f)
            {
                debug = "track width=" + width.ToString("0.00", CultureInfo.InvariantCulture);
                return false;
            }

            float localX = panelPosition.x - worldBound.xMin;
            float rawNormalized = localX / width;
            debug = "panelX=" + panelPosition.x.ToString("0.00", CultureInfo.InvariantCulture) +
                ", trackLeft=" + worldBound.xMin.ToString("0.00", CultureInfo.InvariantCulture) +
                ", trackWidth=" + width.ToString("0.00", CultureInfo.InvariantCulture) +
                ", localX=" + localX.ToString("0.00", CultureInfo.InvariantCulture) +
                ", rawNormalized=" + rawNormalized.ToString("0.000", CultureInfo.InvariantCulture);

            if (float.IsNaN(rawNormalized) || float.IsInfinity(rawNormalized) || rawNormalized < -0.05f || rawNormalized > 1.05f)
            {
                return false;
            }

            normalized = Mathf.Clamp01(rawNormalized);
            return true;
        }

        private void HidePlaybackTimelineTooltip()
        {
            if (this.playbackTimelineTooltip != null)
            {
                this.playbackTimelineTooltip.style.display = DisplayStyle.None;
            }
        }

        private void RefreshPlaybackTimelineMarkers(int totalTicks)
        {
            if (this.playbackTimelineTrack == null || this.playback == null || !this.playback.IsPlaybackActive || totalTicks <= 0)
            {
                this.ClearPlaybackTimelineMarkers();
                return;
            }

            string filePath = this.playback.CurrentFilePath;
            if (string.IsNullOrEmpty(filePath))
            {
                this.ClearPlaybackTimelineMarkers();
                return;
            }

            if (string.Equals(this.loadedPlaybackTimelineFilePath, filePath, StringComparison.Ordinal))
            {
                return;
            }

            this.ClearPlaybackTimelineMarkers();
            this.loadedPlaybackTimelineFilePath = filePath;
            this.playbackTimelineMarkers.Clear();

            try
            {
                ReplayFileSummary summary = this.reader.ReadSummary(filePath, this.storage.SummariesDirectory);
                if (summary == null)
                {
                    return;
                }

                this.RefreshPlaybackGameDropdown(summary);
                this.RebuildPlaybackGameSegmentHighlights(totalTicks);
                if (summary.TimelineEvents == null || summary.TimelineEvents.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < summary.TimelineEvents.Count; i++)
                {
                    ReplayTimelineEntrySummary entry = summary.TimelineEvents[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    VisualElement marker = this.CreatePlaybackTimelineMarker(entry, totalTicks);
                    Color markerColor = GetPlaybackTimelineMarkerColor(entry);
                    this.playbackTimelineTrack.Add(marker);
                    this.playbackTimelineMarkers.Add(new PlaybackTimelineMarkerData(
                        Math.Max(0, entry.Tick),
                        Mathf.Clamp01(entry.Tick / (float)totalTicks),
                        this.GetPlaybackTimelineMarkerTooltip(entry),
                        marker,
                        markerColor));
                }

                this.RefreshPlaybackTimelineMarkerEmphasis();
            }
            catch (Exception exception)
            {
                ReplayModLog.Warning("Failed to load playback timeline markers: " + exception.Message);
            }
        }

        private void RefreshPlaybackGameDropdown(ReplayFileSummary summary)
        {
            if (this.playbackGameDropdown == null)
            {
                return;
            }

            this.playbackGameSegments.Clear();
            List<string> choices = new List<string>
            {
                "Full replay"
            };

            if (summary != null && summary.GameSegments != null)
            {
                for (int i = 0; i < summary.GameSegments.Count; i++)
                {
                    ReplayGameSegmentSummary segment = summary.GameSegments[i];
                    if (segment == null || segment.EndTick <= segment.StartTick)
                    {
                        continue;
                    }

                    string label = string.IsNullOrEmpty(segment.Label) ? "Game " + segment.Index : segment.Label;
                    if (this.playbackGameSegments.ContainsKey(label))
                    {
                        label = label + " #" + segment.Index;
                    }

                    this.playbackGameSegments[label] = segment;
                    choices.Add(label);
                }
            }

            this.playbackGameDropdown.choices = choices;
            if (!choices.Contains(this.selectedPlaybackGameChoice))
            {
                this.selectedPlaybackGameChoice = "Full replay";
            }

            this.playbackGameDropdown.SetValueWithoutNotify(this.selectedPlaybackGameChoice);
            this.playbackGameDropdown.SetEnabled(choices.Count > 1);
        }

        private void RefreshPlaybackGameSegmentHighlight(int totalTicks)
        {
            if (totalTicks <= 0 || this.playbackGameSegmentHighlights.Count == 0)
            {
                return;
            }

            bool hasSelectedGame = this.selectedPlaybackGameChoice != "Full replay" && this.playbackGameSegments.ContainsKey(this.selectedPlaybackGameChoice);
            foreach (KeyValuePair<string, VisualElement> entry in this.playbackGameSegmentHighlights)
            {
                if (entry.Value == null)
                {
                    continue;
                }

                bool isSelected = hasSelectedGame && entry.Key == this.selectedPlaybackGameChoice;
                entry.Value.style.backgroundColor = isSelected
                    ? new Color(0.42f, 0.68f, 1f, 0.36f)
                    : new Color(0.42f, 0.62f, 0.95f, hasSelectedGame ? 0.13f : 0.22f);
                entry.Value.style.display = DisplayStyle.Flex;
            }

            this.RefreshPlaybackTimelineMarkerEmphasis();
        }

        private void RebuildPlaybackGameSegmentHighlights(int totalTicks)
        {
            this.ClearPlaybackGameSegmentHighlights();
            if (this.playbackTimelineTrack == null || totalTicks <= 0)
            {
                return;
            }

            foreach (KeyValuePair<string, ReplayGameSegmentSummary> entry in this.playbackGameSegments)
            {
                ReplayGameSegmentSummary segment = entry.Value;
                if (segment == null || segment.EndTick <= segment.StartTick)
                {
                    continue;
                }

                float startPercent = Mathf.Clamp01(segment.StartTick / (float)totalTicks) * 100f;
                float endPercent = Mathf.Clamp01(segment.EndTick / (float)totalTicks) * 100f;
                VisualElement highlight = new VisualElement
                {
                    name = "PuckReplayModPlaybackGameSegmentHighlight"
                };
                highlight.style.position = Position.Absolute;
                highlight.style.top = 0f;
                highlight.style.bottom = 0f;
                highlight.style.left = new StyleLength(new Length(startPercent, LengthUnit.Percent));
                highlight.style.width = new StyleLength(new Length(Mathf.Max(0.5f, endPercent - startPercent), LengthUnit.Percent));
                highlight.style.backgroundColor = new Color(0.42f, 0.62f, 0.95f, 0.22f);
                highlight.pickingMode = PickingMode.Ignore;
                this.playbackTimelineTrack.Add(highlight);
                this.playbackGameSegmentHighlights[entry.Key] = highlight;
            }

            this.RefreshPlaybackGameSegmentHighlight(totalTicks);
        }

        private void ClearPlaybackGameSegmentHighlights()
        {
            if (this.playbackTimelineTrack != null)
            {
                for (int i = this.playbackTimelineTrack.childCount - 1; i >= 0; i--)
                {
                    VisualElement child = this.playbackTimelineTrack.ElementAt(i);
                    if (child != null && child.name == "PuckReplayModPlaybackGameSegmentHighlight")
                    {
                        this.playbackTimelineTrack.Remove(child);
                    }
                }
            }

            this.playbackGameSegmentHighlights.Clear();
        }

        private VisualElement CreatePlaybackTimelineMarker(ReplayTimelineEntrySummary entry, int totalTicks)
        {
            float percent = totalTicks > 0 ? Mathf.Clamp01(entry.Tick / (float)totalTicks) * 100f : 0f;
            VisualElement marker = new VisualElement
            {
                name = "PuckReplayModPlaybackTimelineMarker"
            };
            marker.style.position = Position.Absolute;
            marker.style.left = new StyleLength(new Length(percent, LengthUnit.Percent));
            marker.style.top = 3f;
            marker.style.bottom = 3f;
            marker.style.width = 5f;
            marker.style.marginLeft = -2.5f;
            marker.style.backgroundColor = GetPlaybackTimelineMarkerColor(entry);
            marker.style.borderTopLeftRadius = 2f;
            marker.style.borderTopRightRadius = 2f;
            marker.style.borderBottomLeftRadius = 2f;
            marker.style.borderBottomRightRadius = 2f;
            marker.tooltip = this.GetPlaybackTimelineMarkerTooltip(entry);
            marker.pickingMode = PickingMode.Ignore;
            return marker;
        }

        private void ClearPlaybackTimelineMarkers()
        {
            this.loadedPlaybackTimelineFilePath = null;
            this.playbackTimelineMarkers.Clear();
            this.playbackGameSegments.Clear();
            this.playbackGameSegmentHighlights.Clear();
            this.selectedPlaybackGameChoice = "Full replay";
            this.HidePlaybackTimelineTooltip();
            this.ClearPlaybackGameSegmentHighlights();
            if (this.playbackGameDropdown != null)
            {
                this.playbackGameDropdown.choices = new List<string> { "Full replay" };
                this.playbackGameDropdown.SetValueWithoutNotify("Full replay");
                this.playbackGameDropdown.SetEnabled(false);
            }

            if (this.playbackTimelineTrack == null)
            {
                return;
            }

            for (int i = this.playbackTimelineTrack.childCount - 1; i >= 0; i--)
            {
                VisualElement child = this.playbackTimelineTrack.ElementAt(i);
                if (child != null && child.name == "PuckReplayModPlaybackTimelineMarker")
                {
                    this.playbackTimelineTrack.Remove(child);
                }
            }
        }

        private static Color GetPlaybackTimelineMarkerColor(ReplayTimelineEntrySummary entry)
        {
            if (entry != null && entry.Type == "Goal")
            {
                if (entry.Team == "Blue")
                {
                    return new Color(0.18f, 0.56f, 1f, 1f);
                }

                if (entry.Team == "Red")
                {
                    return new Color(1f, 0.22f, 0.18f, 1f);
                }

                return new Color(0.95f, 0.95f, 0.95f, 1f);
            }

            if (entry != null && entry.Type == "PeriodStart")
            {
                return new Color(0.62f, 0.92f, 0.62f, 1f);
            }

            if (entry != null && entry.Type == "PeriodEnd")
            {
                return new Color(0.78f, 0.78f, 0.86f, 1f);
            }

            return new Color(1f, 0.82f, 0.18f, 1f);
        }

        private string GetPlaybackTimelineMarkerTooltip(ReplayTimelineEntrySummary entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            string label = !string.IsNullOrEmpty(entry.Tooltip) ? entry.Tooltip : (string.IsNullOrEmpty(entry.Label) ? entry.Type : entry.Label);
            return label + " - " + this.FormatPlaybackTime(Math.Max(0, entry.Tick));
        }

        private ReplayGameSegmentSummary GetPlaybackGameSegmentAtNormalizedPosition(float normalized)
        {
            if (this.playback == null || this.playback.TotalTicks <= 0)
            {
                return null;
            }

            int tick = Mathf.RoundToInt(Mathf.Clamp01(normalized) * this.playback.TotalTicks);
            foreach (ReplayGameSegmentSummary segment in this.playbackGameSegments.Values)
            {
                if (segment != null && tick >= segment.StartTick && tick <= segment.EndTick)
                {
                    return segment;
                }
            }

            return null;
        }

        private string GetPlaybackGameSegmentTooltip(ReplayGameSegmentSummary segment)
        {
            if (segment == null)
            {
                return string.Empty;
            }

            string label = string.IsNullOrEmpty(segment.Label) ? "Game " + segment.Index : segment.Label;
            return label + " - " + this.FormatPlaybackTime(Math.Max(0, segment.StartTick)) + " to " + this.FormatPlaybackTime(Math.Max(0, segment.EndTick));
        }

        private void RefreshPlaybackTimelineMarkerEmphasis()
        {
            bool hasSelectedGame = this.selectedPlaybackGameChoice != "Full replay" && this.playbackGameSegments.ContainsKey(this.selectedPlaybackGameChoice);
            ReplayGameSegmentSummary selectedSegment = null;
            if (hasSelectedGame)
            {
                this.playbackGameSegments.TryGetValue(this.selectedPlaybackGameChoice, out selectedSegment);
            }

            for (int i = 0; i < this.playbackTimelineMarkers.Count; i++)
            {
                PlaybackTimelineMarkerData marker = this.playbackTimelineMarkers[i];
                if (marker == null || marker.Element == null)
                {
                    continue;
                }

                bool isInsideSelectedGame = selectedSegment != null && marker.Tick >= selectedSegment.StartTick && marker.Tick <= selectedSegment.EndTick;
                float alpha = !hasSelectedGame || isInsideSelectedGame ? marker.BaseColor.a : 0.28f;
                marker.Element.style.backgroundColor = new Color(marker.BaseColor.r, marker.BaseColor.g, marker.BaseColor.b, alpha);
            }
        }

        private sealed class PlaybackTimelineMarkerData
        {
            public PlaybackTimelineMarkerData(int tick, float normalizedTick, string tooltip, VisualElement element, Color baseColor)
            {
                this.Tick = tick;
                this.NormalizedTick = normalizedTick;
                this.Tooltip = tooltip ?? string.Empty;
                this.Element = element;
                this.BaseColor = baseColor;
            }

            public int Tick { get; private set; }

            public float NormalizedTick { get; private set; }

            public string Tooltip { get; private set; }

            public VisualElement Element { get; private set; }

            public Color BaseColor { get; private set; }
        }

        private bool IsPlaybackControlInputAllowed()
        {
            if (this.playback == null || !this.playback.IsPlaybackActive || this.isManagerVisible)
            {
                return false;
            }

            if (this.IsCaptureModeHidingPlaybackControls())
            {
                return false;
            }

            if (this.playbackUiInputActive)
            {
                return true;
            }

            return this.settings != null &&
                this.settings.PlaybackUiInputMode == ReplayPlaybackUiInputMode.Hold &&
                this.IsKeyHeld(this.settings.PlaybackUiInputKey);
        }

        private void OnPlaybackSpeedChanged(string value)
        {
            if (string.IsNullOrEmpty(value) || this.playback == null)
            {
                return;
            }

            string rawValue = value.Replace("x", string.Empty);
            float speed;
            if (!float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out speed))
            {
                return;
            }

            this.playback.SetPlaybackSpeed(speed);
            this.RefreshPlaybackControls();
        }

        private void OnPlaybackGameChanged(string value)
        {
            if (this.playback == null || string.IsNullOrEmpty(value))
            {
                return;
            }

            this.selectedPlaybackGameChoice = value;
            ReplayGameSegmentSummary segment;
            if (this.playbackGameSegments.TryGetValue(value, out segment) && segment != null)
            {
                this.playback.SeekToTick(Math.Max(0, segment.StartTick));
            }

            this.RefreshPlaybackControls();
            this.RefreshPlaybackStatus();
        }

        private void SelectPlaybackGameForTick(int tick)
        {
            string selected = "Full replay";
            foreach (KeyValuePair<string, ReplayGameSegmentSummary> entry in this.playbackGameSegments)
            {
                ReplayGameSegmentSummary segment = entry.Value;
                if (segment == null)
                {
                    continue;
                }

                if (tick >= segment.StartTick && tick <= segment.EndTick)
                {
                    selected = entry.Key;
                    break;
                }
            }

            this.selectedPlaybackGameChoice = selected;
            if (this.playbackGameDropdown != null && this.playbackGameDropdown.value != selected)
            {
                this.playbackGameDropdown.SetValueWithoutNotify(selected);
            }
        }

        private void OnPlaybackCameraModeChanged(string value)
        {
            if (this.playback == null)
            {
                return;
            }

            this.playback.SetCameraMode(ParseCameraMode(value));
            this.RefreshPlaybackControls();
            this.RefreshPlaybackCameraTargetDropdown(true);
        }

        private void OnPlaybackCameraTargetChanged(string value)
        {
            if (this.playback == null || string.IsNullOrEmpty(value))
            {
                return;
            }

            ulong ownerClientId;
            if (!this.playbackCameraTargets.TryGetValue(value, out ownerClientId))
            {
                this.playback.SetCameraTarget(null);
                return;
            }

            this.playback.SetCameraTarget(ownerClientId);
        }

        private static string FormatSpeed(float speed)
        {
            if (Math.Abs(speed - 1f) < 0.01f)
            {
                return "1x";
            }

            return speed.ToString("0.##", CultureInfo.InvariantCulture) + "x";
        }

        private static string FormatCameraMode(ReplayPlaybackCameraMode mode)
        {
            if (mode == ReplayPlaybackCameraMode.FirstPerson)
            {
                return "1st person";
            }

            if (mode == ReplayPlaybackCameraMode.ThirdPerson)
            {
                return "3rd person";
            }

            return "Free";
        }

        private static ReplayPlaybackCameraMode ParseCameraMode(string value)
        {
            if (string.Equals(value, "1st person", StringComparison.OrdinalIgnoreCase))
            {
                return ReplayPlaybackCameraMode.FirstPerson;
            }

            if (string.Equals(value, "3rd person", StringComparison.OrdinalIgnoreCase))
            {
                return ReplayPlaybackCameraMode.ThirdPerson;
            }

            return ReplayPlaybackCameraMode.Free;
        }

        private void RefreshPlaybackCameraTargetDropdown(bool syncValue)
        {
            if (this.playbackCameraTargetDropdown == null || this.playback == null)
            {
                return;
            }

            this.playbackCameraTargets.Clear();
            List<string> choices = new List<string>();
            List<ReplayPlaybackPlayerTarget> targets = this.playback.CameraTargets;
            for (int i = 0; i < targets.Count; i++)
            {
                ReplayPlaybackPlayerTarget target = targets[i];
                string label = target.DisplayName;
                if (string.IsNullOrEmpty(label))
                {
                    label = "Replay Player " + target.OwnerClientId;
                }

                if (this.playbackCameraTargets.ContainsKey(label))
                {
                    label += " (" + target.OwnerClientId + ")";
                }

                this.playbackCameraTargets[label] = target.OwnerClientId;
                choices.Add(label);
            }

            if (choices.Count == 0)
            {
                choices.Add("No players");
            }

            bool choicesChanged = this.playbackCameraTargetDropdown.choices == null || this.playbackCameraTargetDropdown.choices.Count != choices.Count;
            if (!choicesChanged)
            {
                for (int i = 0; i < choices.Count; i++)
                {
                    if (this.playbackCameraTargetDropdown.choices[i] != choices[i])
                    {
                        choicesChanged = true;
                        break;
                    }
                }
            }

            if (choicesChanged)
            {
                this.playbackCameraTargetDropdown.choices = choices;
            }

            string selected = choices[0];
            if (this.playback.CameraTargetClientId.HasValue)
            {
                foreach (KeyValuePair<string, ulong> entry in this.playbackCameraTargets)
                {
                    if (entry.Value == this.playback.CameraTargetClientId.Value)
                    {
                        selected = entry.Key;
                        break;
                    }
                }
            }

            if (syncValue && this.playbackCameraTargetDropdown.value != selected)
            {
                this.playbackCameraTargetDropdown.SetValueWithoutNotify(selected);
            }
            else if (!syncValue)
            {
                this.LogPlaybackUiDebug("Skipped POV player selected-value sync during active dropdown interaction.", true);
            }

            this.playbackCameraTargetDropdown.SetEnabled(this.playbackCameraTargets.Count > 0);
        }

        internal void RefreshTimelineIndicator()
        {
            if (this.timelineLabel == null)
            {
                return;
            }

            if (!this.settings.ShowPlaybackTimeline || !this.playback.IsPlaying || this.IsCaptureModeHidingReplayOverlays())
            {
                this.timelineLabel.style.display = DisplayStyle.None;
                return;
            }

            int currentTick = Math.Max(0, this.playback.CurrentTick);
            int totalTicks = Math.Max(currentTick, this.playback.TotalTicks);
            this.timelineLabel.text = "REPLAY  " + this.FormatPlaybackTime(currentTick) + " / " + this.FormatPlaybackTime(totalTicks);
            this.timelineLabel.style.display = DisplayStyle.Flex;
        }

        internal void RefreshStatusIndicator()
        {
            if (this.statusLabel == null)
            {
                return;
            }

            if (!this.settings.ShowStatusIndicator || this.isMainMenuVisible || this.IsCaptureModeHidingReplayOverlays())
            {
                this.statusLabel.style.display = DisplayStyle.None;
                return;
            }

            bool shouldShowWithScoreboard = this.settings.StatusIndicatorVisibility == ReplayIndicatorVisibility.ScoreboardOnly && this.IsScoreboardHeld();
            bool shouldShowReady = this.settings.StatusIndicatorVisibility == ReplayIndicatorVisibility.Always ||
                shouldShowWithScoreboard;
            bool shouldShowPlayback = this.settings.StatusIndicatorVisibility == ReplayIndicatorVisibility.Always ||
                this.settings.StatusIndicatorVisibility == ReplayIndicatorVisibility.RecordingAndPlayback ||
                shouldShowWithScoreboard;
            bool shouldShowRecording = this.settings.StatusIndicatorVisibility == ReplayIndicatorVisibility.Always ||
                this.settings.StatusIndicatorVisibility == ReplayIndicatorVisibility.RecordingAndPlayback ||
                this.settings.StatusIndicatorVisibility == ReplayIndicatorVisibility.RecordingOnly ||
                shouldShowWithScoreboard;

            if (this.recorder.IsRecording)
            {
                this.statusLabel.style.display = shouldShowRecording ? DisplayStyle.Flex : DisplayStyle.None;
                this.statusLabel.text = !this.settings.SaveOnDisconnect && !this.recorder.IsCurrentRecordingSaveConfirmed
                    ? "REPLAY MOD  REC UNSAVED  " + this.recorder.CurrentTick
                    : "REPLAY MOD  REC  " + this.recorder.CurrentTick;
                this.statusLabel.style.backgroundColor = new Color(0.55f, 0.05f, 0.06f, 0.9f);
                return;
            }

            if (this.recorder.IsRecordingSuppressed)
            {
                this.statusLabel.style.display = shouldShowPlayback ? DisplayStyle.Flex : DisplayStyle.None;
                this.statusLabel.text = "REPLAY MOD  PLAYBACK";
                this.statusLabel.style.backgroundColor = new Color(0.1f, 0.18f, 0.32f, 0.9f);
                return;
            }

            this.statusLabel.style.display = shouldShowReady ? DisplayStyle.Flex : DisplayStyle.None;
            this.statusLabel.text = "REPLAY MOD  READY";
            this.statusLabel.style.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.72f);
        }

        internal void ApplyOverlayPosition(VisualElement element, ReplayOverlayPosition position, float offset)
        {
            if (element == null)
            {
                return;
            }

            element.style.left = StyleKeyword.Auto;
            element.style.right = StyleKeyword.Auto;
            element.style.top = StyleKeyword.Auto;
            element.style.bottom = StyleKeyword.Auto;

            switch (position)
            {
                case ReplayOverlayPosition.TopLeft:
                    element.style.left = 18f;
                    element.style.top = offset;
                    break;
                case ReplayOverlayPosition.BottomRight:
                    element.style.right = 18f;
                    element.style.bottom = offset;
                    break;
                case ReplayOverlayPosition.BottomLeft:
                    element.style.left = 18f;
                    element.style.bottom = offset;
                    break;
                default:
                    element.style.right = 18f;
                    element.style.top = offset;
                    break;
            }
        }

        private bool AttachReplayButton(VisualElement menu, string name, out Button attachedButton, out Label updateBadge)
        {
            attachedButton = null;
            updateBadge = null;
            if (menu == null || menu.Q<Button>(name) != null)
            {
                attachedButton = menu != null ? menu.Q<Button>(name) : null;
                updateBadge = attachedButton != null ? attachedButton.Q<Label>("PuckReplayModMenuUpdateBadge") : null;
                return menu != null;
            }

            Button referenceButton = menu.Q<Button>("ModsButton") ?? menu.Q<Button>("SettingsButton") ?? menu.Q<Button>("ServerBrowserButton");
            Button button = new Button(this.ToggleManager)
            {
                name = name,
                text = "REPLAYS"
            };
            ReplayUiTools.StyleMenuAccessButton(referenceButton, button);
            attachedButton = button;
            updateBadge = this.CreateReplayMenuUpdateBadge();
            button.style.position = Position.Relative;
            button.style.overflow = Overflow.Visible;
            button.Add(updateBadge);

            if (referenceButton != null && referenceButton.parent == menu)
            {
                menu.Insert(referenceButton.parent.IndexOf(referenceButton) + 1, button);
            }
            else
            {
                menu.Add(button);
            }

            return true;
        }

        private Label CreateReplayMenuUpdateBadge()
        {
            Label badge = new Label("!");
            badge.name = "PuckReplayModMenuUpdateBadge";
            badge.pickingMode = PickingMode.Ignore;
            badge.style.position = Position.Absolute;
            badge.style.right = 7f;
            badge.style.top = new StyleLength(new Length(50f, LengthUnit.Percent));
            badge.style.translate = new StyleTranslate(new Translate(0f, new Length(-50f, LengthUnit.Percent), 0f));
            badge.style.minWidth = 24f;
            badge.style.height = 24f;
            badge.style.paddingLeft = 7f;
            badge.style.paddingRight = 7f;
            badge.style.paddingTop = 0f;
            badge.style.paddingBottom = 0f;
            badge.style.fontSize = 15f;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.color = Color.black;
            badge.style.backgroundColor = new Color(0.86f, 0.66f, 0.18f, 1f);
            badge.style.borderTopLeftRadius = 12f;
            badge.style.borderTopRightRadius = 12f;
            badge.style.borderBottomLeftRadius = 12f;
            badge.style.borderBottomRightRadius = 12f;
            badge.style.display = DisplayStyle.None;
            return badge;
        }

        private void CreateStatusIndicator()
        {
            this.statusLabel = new Label("IDLE")
            {
                name = "PuckReplayModStatus"
            };
            this.statusLabel.style.position = Position.Absolute;
            this.statusLabel.style.paddingLeft = 8f;
            this.statusLabel.style.paddingRight = 8f;
            this.statusLabel.style.paddingTop = 4f;
            this.statusLabel.style.paddingBottom = 4f;
            this.statusLabel.style.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.72f);
            this.statusLabel.style.color = Color.white;
            this.statusLabel.style.fontSize = 13f;
            this.statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            this.statusLabel.pickingMode = PickingMode.Ignore;
            this.ApplyOverlayPosition(this.statusLabel, this.settings.StatusIndicatorPosition, 76f);
            this.root.Add(this.statusLabel);
        }

        private void CreateTimelineIndicator()
        {
            this.timelineLabel = new Label
            {
                name = "PuckReplayModTimeline"
            };
            this.timelineLabel.style.position = Position.Absolute;
            this.timelineLabel.style.paddingLeft = 8f;
            this.timelineLabel.style.paddingRight = 8f;
            this.timelineLabel.style.paddingTop = 4f;
            this.timelineLabel.style.paddingBottom = 4f;
            this.timelineLabel.style.backgroundColor = new Color(0.04f, 0.06f, 0.08f, 0.78f);
            this.timelineLabel.style.color = Color.white;
            this.timelineLabel.style.fontSize = 12f;
            this.timelineLabel.style.display = DisplayStyle.None;
            this.timelineLabel.pickingMode = PickingMode.Ignore;
            this.ApplyOverlayPosition(this.timelineLabel, this.settings.PlaybackTimelinePosition, 110f);
            this.root.Add(this.timelineLabel);
        }

        private void CreatePlaybackControlsPanel()
        {
            this.playbackControlsAnchor = new VisualElement
            {
                name = "PuckReplayModPlaybackControlsAnchor"
            };
            this.playbackControlsAnchor.style.position = Position.Absolute;
            this.playbackControlsAnchor.style.left = 0f;
            this.playbackControlsAnchor.style.right = 0f;
            this.playbackControlsAnchor.style.bottom = 22f;
            this.playbackControlsAnchor.style.height = 86f;
            this.playbackControlsAnchor.style.flexDirection = FlexDirection.Row;
            this.playbackControlsAnchor.style.alignItems = Align.Center;
            this.playbackControlsAnchor.style.justifyContent = Justify.Center;
            this.playbackControlsAnchor.pickingMode = PickingMode.Ignore;

            this.playbackControlsPanel = new VisualElement
            {
                name = "PuckReplayModPlaybackControls"
            };
            this.playbackControlsPanel.style.position = Position.Relative;
            this.playbackControlsPanel.style.width = new StyleLength(new Length(76f, LengthUnit.Percent));
            this.playbackControlsPanel.style.maxWidth = 1180f;
            this.playbackControlsPanel.style.minWidth = 820f;
            this.playbackControlsPanel.style.height = 86f;
            this.playbackControlsPanel.style.minHeight = 86f;
            this.playbackControlsPanel.style.flexDirection = FlexDirection.Column;
            this.playbackControlsPanel.style.paddingLeft = 10f;
            this.playbackControlsPanel.style.paddingRight = 10f;
            this.playbackControlsPanel.style.paddingTop = 8f;
            this.playbackControlsPanel.style.paddingBottom = 8f;
            this.playbackControlsPanel.style.backgroundColor = new Color(0.04f, 0.05f, 0.06f, 0.86f);
            this.playbackControlsPanel.style.display = DisplayStyle.None;
            this.playbackControlsPanel.pickingMode = PickingMode.Position;
            this.playbackControlsPanel.RegisterCallback<PointerDownEvent>(this.OnPlaybackControlsPointerDown, TrickleDown.TrickleDown);

            VisualElement scrubRow = new VisualElement();
            scrubRow.style.flexDirection = FlexDirection.Row;
            scrubRow.style.alignItems = Align.Center;
            scrubRow.style.height = 28f;
            scrubRow.style.marginBottom = 8f;

            VisualElement timelineTrack = new VisualElement
            {
                name = "PuckReplayModPlaybackScrubTrack"
            };
            this.playbackTimelineTrack = timelineTrack;
            timelineTrack.style.flexGrow = 1f;
            timelineTrack.style.height = 28f;
            timelineTrack.style.backgroundColor = new Color(0.22f, 0.24f, 0.26f, 1f);
            timelineTrack.RegisterCallback<PointerDownEvent>(this.OnPlaybackTimelinePointerDown);
            timelineTrack.RegisterCallback<PointerMoveEvent>(this.OnPlaybackTimelinePointerMove);
            timelineTrack.RegisterCallback<PointerLeaveEvent>(this.OnPlaybackTimelinePointerLeave);

            this.playbackProgressBar = new VisualElement
            {
                name = "PuckReplayModPlaybackScrubProgress"
            };
            this.playbackProgressBar.style.position = Position.Absolute;
            this.playbackProgressBar.style.left = 0f;
            this.playbackProgressBar.style.top = 0f;
            this.playbackProgressBar.style.bottom = 0f;
            this.playbackProgressBar.style.width = new StyleLength(new Length(0f, LengthUnit.Percent));
            this.playbackProgressBar.style.backgroundColor = new Color(0.72f, 0.72f, 0.72f, 0.55f);
            this.playbackProgressBar.pickingMode = PickingMode.Ignore;
            timelineTrack.Add(this.playbackProgressBar);
            scrubRow.Add(timelineTrack);
            this.playbackControlsPanel.Add(scrubRow);

            this.playbackTimelineTooltip = new Label();
            this.playbackTimelineTooltip.style.position = Position.Absolute;
            this.playbackTimelineTooltip.style.bottom = 92f;
            this.playbackTimelineTooltip.style.left = 0f;
            this.playbackTimelineTooltip.style.display = DisplayStyle.None;
            this.playbackTimelineTooltip.style.backgroundColor = new Color(0.02f, 0.025f, 0.03f, 0.96f);
            this.playbackTimelineTooltip.style.color = Color.white;
            this.playbackTimelineTooltip.style.fontSize = 12f;
            this.playbackTimelineTooltip.style.paddingLeft = 8f;
            this.playbackTimelineTooltip.style.paddingRight = 8f;
            this.playbackTimelineTooltip.style.paddingTop = 4f;
            this.playbackTimelineTooltip.style.paddingBottom = 4f;
            this.playbackTimelineTooltip.style.borderTopLeftRadius = 3f;
            this.playbackTimelineTooltip.style.borderTopRightRadius = 3f;
            this.playbackTimelineTooltip.style.borderBottomLeftRadius = 3f;
            this.playbackTimelineTooltip.style.borderBottomRightRadius = 3f;
            this.playbackTimelineTooltip.pickingMode = PickingMode.Ignore;
            this.playbackControlsPanel.Add(this.playbackTimelineTooltip);

            VisualElement controlsRow = new VisualElement();
            controlsRow.style.flexDirection = FlexDirection.Row;
            controlsRow.style.alignItems = Align.Center;
            controlsRow.style.height = 34f;

            Button stopButton = ReplayUiTools.CreateButton("EXIT", this.OnPlaybackExitClicked);
            stopButton.style.width = 70f;
            stopButton.style.minWidth = 70f;
            stopButton.style.height = 32f;
            stopButton.style.minHeight = 32f;
            stopButton.style.marginRight = 6f;
            this.CenterPlaybackTextButton(stopButton);
            controlsRow.Add(stopButton);

            this.playbackPlayPauseButton = ReplayUiTools.CreateButton(string.Empty, this.OnPlaybackPlayPauseClicked);
            this.playbackPlayPauseButton.style.width = 78f;
            this.playbackPlayPauseButton.style.minWidth = 78f;
            this.playbackPlayPauseButton.style.height = 32f;
            this.playbackPlayPauseButton.style.minHeight = 32f;
            this.playbackPlayPauseButton.style.marginRight = 8f;
            this.playbackPlayPauseButton.style.alignItems = Align.Center;
            this.playbackPlayPauseButton.style.justifyContent = Justify.Center;
            this.playbackPlayPauseButton.style.paddingLeft = 0f;
            this.playbackPlayPauseButton.style.paddingRight = 0f;
            this.playbackPlayPauseButton.style.paddingTop = 0f;
            this.playbackPlayPauseButton.style.paddingBottom = 0f;
            this.playbackPlayPauseIcon = new PlaybackPlayPauseIcon(PlaybackPlayPauseIconMode.Pause, 20f);
            this.playbackPlayPauseButton.Add(this.playbackPlayPauseIcon);
            this.playbackPlayPauseButton.RegisterCallback<MouseEnterEvent>(delegate
            {
                this.playbackPlayPauseIcon.SetColor(Color.black);
            });
            this.playbackPlayPauseButton.RegisterCallback<MouseLeaveEvent>(delegate
            {
                this.playbackPlayPauseIcon.SetColor(Color.white);
            });
            controlsRow.Add(this.playbackPlayPauseButton);

            Button backFiveButton = this.CreatePlaybackControlButton("seek-backward-5.svg", 24f, 46f, false, delegate
            {
                this.playback.SeekRelativeSeconds(-5f);
                this.RefreshPlaybackControls();
                this.RefreshPlaybackStatus();
            });
            backFiveButton.tooltip = "Back 5 seconds";
            controlsRow.Add(backFiveButton);

            Button backTickButton = this.CreatePlaybackControlButton("frame-previous.svg", 20f, 38f, true, delegate
            {
                this.playback.SeekRelativeTicks(-1);
                this.RefreshPlaybackControls();
                this.RefreshPlaybackStatus();
            });
            backTickButton.tooltip = "Back 1 replay tick";
            controlsRow.Add(backTickButton);

            this.playbackTimeLabel = new Label("00:00 / 00:00");
            this.playbackTimeLabel.style.width = 112f;
            this.playbackTimeLabel.style.minWidth = 112f;
            this.playbackTimeLabel.style.color = Color.white;
            this.playbackTimeLabel.style.fontSize = 12f;
            this.playbackTimeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            this.playbackTimeLabel.style.marginLeft = 4f;
            this.playbackTimeLabel.style.marginRight = 8f;
            controlsRow.Add(this.playbackTimeLabel);

            Button forwardTickButton = this.CreatePlaybackControlButton("frame-next.svg", 20f, 38f, true, delegate
            {
                this.playback.SeekRelativeTicks(1);
                this.RefreshPlaybackControls();
                this.RefreshPlaybackStatus();
            });
            forwardTickButton.tooltip = "Forward 1 replay tick";
            controlsRow.Add(forwardTickButton);

            Button forwardFiveButton = this.CreatePlaybackControlButton("seek-forward-5.svg", 24f, 46f, false, delegate
            {
                this.playback.SeekRelativeSeconds(5f);
                this.RefreshPlaybackControls();
                this.RefreshPlaybackStatus();
            });
            forwardFiveButton.tooltip = "Forward 5 seconds";
            controlsRow.Add(forwardFiveButton);

            this.playbackCaptureModeButton = ReplayUiTools.CreateButton(this.GetPlaybackCaptureModeButtonText(), delegate
            {
                this.SetCaptureModeActive(!this.captureModeActive);
            });
            this.playbackCaptureModeButton.style.width = 122f;
            this.playbackCaptureModeButton.style.minWidth = 122f;
            this.playbackCaptureModeButton.style.maxWidth = 122f;
            this.playbackCaptureModeButton.style.height = 32f;
            this.playbackCaptureModeButton.style.minHeight = 32f;
            this.playbackCaptureModeButton.style.marginLeft = 8f;
            this.playbackCaptureModeButton.tooltip = "Toggle clean-screen Capture Mode for recording clips.";
            this.CenterPlaybackTextButton(this.playbackCaptureModeButton);
            controlsRow.Add(this.playbackCaptureModeButton);

            this.playbackGameDropdown = ReplayUiTools.CreateDropdown(new List<string>
            {
                "Full replay"
            }, "Full replay", this.OnPlaybackGameChanged);
            this.playbackGameDropdown.style.width = 132f;
            this.playbackGameDropdown.style.minWidth = 132f;
            this.playbackGameDropdown.style.maxWidth = 132f;
            this.playbackGameDropdown.style.marginLeft = 8f;
            this.playbackGameDropdown.tooltip = "Game section";
            this.RegisterPlaybackDropdownDebugCallbacks(this.playbackGameDropdown, "game");
            controlsRow.Add(this.playbackGameDropdown);

            this.playbackSpeedDropdown = ReplayUiTools.CreateDropdown(new List<string>
            {
                "0.25x",
                "0.5x",
                "1x",
                "2x",
                "4x"
            }, "1x", this.OnPlaybackSpeedChanged);
            this.playbackSpeedDropdown.style.width = 82f;
            this.playbackSpeedDropdown.style.minWidth = 82f;
            this.playbackSpeedDropdown.style.maxWidth = 82f;
            this.playbackSpeedDropdown.style.marginLeft = 8f;
            this.playbackSpeedDropdown.tooltip = "Playback speed";
            this.RegisterPlaybackDropdownDebugCallbacks(this.playbackSpeedDropdown, "speed");
            controlsRow.Add(this.playbackSpeedDropdown);

            this.playbackCameraModeDropdown = ReplayUiTools.CreateDropdown(new List<string>
            {
                "Free",
                "1st person",
                "3rd person"
            }, "Free", this.OnPlaybackCameraModeChanged);
            this.playbackCameraModeDropdown.style.width = 118f;
            this.playbackCameraModeDropdown.style.minWidth = 118f;
            this.playbackCameraModeDropdown.style.maxWidth = 118f;
            this.playbackCameraModeDropdown.style.marginLeft = 8f;
            this.playbackCameraModeDropdown.tooltip = "Camera mode";
            this.RegisterPlaybackDropdownDebugCallbacks(this.playbackCameraModeDropdown, "camera mode");
            controlsRow.Add(this.playbackCameraModeDropdown);

            this.playbackCameraTargetDropdown = ReplayUiTools.CreateDropdown(new List<string>
            {
                "No players"
            }, "No players", this.OnPlaybackCameraTargetChanged);
            this.playbackCameraTargetDropdown.style.width = 170f;
            this.playbackCameraTargetDropdown.style.minWidth = 170f;
            this.playbackCameraTargetDropdown.style.maxWidth = 170f;
            this.playbackCameraTargetDropdown.style.marginLeft = 6f;
            this.playbackCameraTargetDropdown.tooltip = "POV player";
            this.RegisterPlaybackDropdownDebugCallbacks(this.playbackCameraTargetDropdown, "POV player");
            controlsRow.Add(this.playbackCameraTargetDropdown);

            this.playbackControlsPanel.Add(controlsRow);

            this.playbackControlsAnchor.Add(this.playbackControlsPanel);
            this.root.Add(this.playbackControlsAnchor);
        }

        private Button CreatePlaybackControlButton(string iconFileName, float iconSize, float width, bool repeatOnHold, Action action)
        {
            Button button = ReplayUiTools.CreateButton(string.Empty, delegate
            {
                if (this.suppressNextPlaybackControlClick)
                {
                    if (Time.realtimeSinceStartup <= this.suppressNextPlaybackControlClickUntil)
                    {
                        this.suppressNextPlaybackControlClick = false;
                        return;
                    }

                    this.suppressNextPlaybackControlClick = false;
                }

                if (!this.IsPlaybackControlInputAllowed())
                {
                    return;
                }

                action();
            });
            button.style.width = width;
            button.style.minWidth = width;
            button.style.height = 32f;
            button.style.minHeight = 32f;
            button.style.marginRight = 4f;
            button.style.paddingLeft = 0f;
            button.style.paddingRight = 0f;
            button.style.paddingTop = 0f;
            button.style.paddingBottom = 0f;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;

            VisualElement icon = ReplayUiTools.CreateSvgIcon(iconFileName, iconSize, Color.white);
            button.Add(icon);
            button.RegisterCallback<MouseEnterEvent>(delegate
            {
                ReplayUiTools.SetSvgIconColor(icon, Color.black);
            });
            button.RegisterCallback<MouseLeaveEvent>(delegate
            {
                ReplayUiTools.SetSvgIconColor(icon, Color.white);
            });

            if (repeatOnHold)
            {
                button.RegisterCallback<PointerDownEvent>(delegate(PointerDownEvent evt)
                {
                    this.BeginHeldPlaybackControl(action);
                    evt.StopPropagation();
                }, TrickleDown.TrickleDown);

                button.RegisterCallback<PointerUpEvent>(delegate
                {
                    this.EndHeldPlaybackControl(action);
                }, TrickleDown.TrickleDown);

                button.RegisterCallback<PointerCancelEvent>(delegate
                {
                    this.EndHeldPlaybackControl(action);
                }, TrickleDown.TrickleDown);

                button.RegisterCallback<PointerLeaveEvent>(delegate
                {
                    this.EndHeldPlaybackControl(action);
                }, TrickleDown.TrickleDown);

                button.RegisterCallback<DetachFromPanelEvent>(delegate
                {
                    this.EndHeldPlaybackControl(action);
                });
            }

            return button;
        }

        private void BeginHeldPlaybackControl(Action action)
        {
            if (action == null || !this.IsPlaybackControlInputAllowed())
            {
                return;
            }

            this.heldPlaybackControlAction = action;
            this.nextHeldPlaybackControlRealtime = Time.realtimeSinceStartup + 0.28f;
            this.suppressNextPlaybackControlClick = true;
            this.suppressNextPlaybackControlClickUntil = Time.realtimeSinceStartup + 0.5f;
            this.LogPlaybackUiDebug("Held playback control started.", false);
            action();
        }

        private void EndHeldPlaybackControl(Action action)
        {
            if (this.heldPlaybackControlAction == action)
            {
                this.heldPlaybackControlAction = null;
                this.suppressNextPlaybackControlClick = true;
                this.suppressNextPlaybackControlClickUntil = Time.realtimeSinceStartup + 0.15f;
                this.LogPlaybackUiDebug("Held playback control ended.", false);
            }
        }

        private void PollHeldPlaybackControl()
        {
            if (this.heldPlaybackControlAction == null)
            {
                return;
            }

            if (!this.IsPlaybackControlInputAllowed())
            {
                this.heldPlaybackControlAction = null;
                return;
            }

            float realtime = Time.realtimeSinceStartup;
            if (realtime < this.nextHeldPlaybackControlRealtime)
            {
                return;
            }

            this.nextHeldPlaybackControlRealtime = realtime + 0.08f;
            this.LogPlaybackUiDebug("Held playback control repeat.", true);
            this.heldPlaybackControlAction();
        }

        private void CenterPlaybackTextButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.paddingLeft = 0f;
            button.style.paddingRight = 0f;
            button.style.paddingTop = 0f;
            button.style.paddingBottom = 0f;
        }

        private void OnPlaybackControlsPointerDown(PointerDownEvent evt)
        {
            if (this.IsPlaybackControlInputAllowed())
            {
                this.LogPlaybackUiDebug("Allowed playback controls pointer down: " + this.GetPlaybackInputDebugState(evt), true);
                return;
            }

            this.LogPlaybackUiDebug("Blocked playback controls pointer down: " + this.GetPlaybackInputDebugState(evt), true);
            evt.StopImmediatePropagation();
        }

        private void RegisterPlaybackDropdownDebugCallbacks(PopupField<string> dropdown, string name)
        {
            if (dropdown == null)
            {
                return;
            }

            dropdown.RegisterCallback<PointerDownEvent>(delegate(PointerDownEvent evt)
            {
                this.ExtendPlaybackDropdownInteractionWindow();
                this.LogPlaybackUiDebug("Dropdown pointer down (" + name + "): " + this.GetPlaybackInputDebugState(evt), false);
                this.SchedulePlaybackDropdownGeometryDiagnostics(dropdown, name);
            }, TrickleDown.TrickleDown);

            dropdown.RegisterCallback<FocusInEvent>(delegate
            {
                this.ExtendPlaybackDropdownInteractionWindow();
                this.LogPlaybackUiDebug("Dropdown focus in (" + name + "), value=" + dropdown.value + ".", false);
            });

            dropdown.RegisterCallback<FocusOutEvent>(delegate
            {
                this.ExtendPlaybackDropdownInteractionWindow();
                this.LogPlaybackUiDebug("Dropdown focus out (" + name + "), value=" + dropdown.value + ".", false);
            });

            dropdown.RegisterValueChangedCallback(delegate(ChangeEvent<string> evt)
            {
                this.ExtendPlaybackDropdownInteractionWindow();
                this.LogPlaybackUiDebug("Dropdown changed (" + name + "): " + evt.previousValue + " -> " + evt.newValue + ".", false);
                this.SchedulePlaybackDropdownGeometryDiagnostics(dropdown, name);
            });
        }

        private void SchedulePlaybackDropdownGeometryDiagnostics(PopupField<string> dropdown, string name)
        {
            if (this.settings == null || !this.settings.EnableDebugProfiling || dropdown == null)
            {
                return;
            }

            dropdown.schedule.Execute((Action)delegate
            {
                this.LogPlaybackDropdownGeometry(dropdown, name, "2ms");
            }).ExecuteLater(2L);

            dropdown.schedule.Execute((Action)delegate
            {
                this.LogPlaybackDropdownGeometry(dropdown, name, "40ms");
            }).ExecuteLater(40L);

            dropdown.schedule.Execute((Action)delegate
            {
                this.LogPlaybackDropdownGeometry(dropdown, name, "120ms");
            }).ExecuteLater(120L);
        }

        private void LogPlaybackDropdownGeometry(PopupField<string> dropdown, string name, string phase)
        {
            if (this.settings == null || !this.settings.EnableDebugProfiling || dropdown == null)
            {
                return;
            }

            IPanel panel = dropdown.panel;
            VisualElement visualTree = panel != null ? panel.visualTree : null;
            VisualElement popup = this.FindClosestDropdownPopup(visualTree, dropdown.worldBound);
            VisualElement popupInner = this.FindClosestDropdownInner(visualTree, dropdown.worldBound);
            Rect dropdownRect = dropdown.worldBound;
            Rect popupRect = popup != null ? popup.worldBound : default(Rect);
            Rect popupInnerRect = popupInner != null ? popupInner.worldBound : default(Rect);
            float verticalGap = popupInner != null ? GetVerticalGap(dropdownRect, popupInnerRect) : -1f;
            bool suspicious = popupInner == null || verticalGap > 24f;

            ReplayModLog.Info(
                "[Playback UI Dropdown Diagnostics] " +
                "name=" + name +
                ", phase=" + phase +
                ", suspicious=" + suspicious +
                ", value=" + dropdown.value +
                ", dropdown=" + FormatRect(dropdownRect) +
                ", popup=" + (popup != null ? FormatRect(popupRect) : "none") +
                ", inner=" + (popupInner != null ? FormatRect(popupInnerRect) : "none") +
                ", verticalGap=" + verticalGap.ToString("0.0", CultureInfo.InvariantCulture) +
                ", items=" + FormatDropdownItemRects(popupInner) +
                ", anchor=" + FormatElementRect(this.playbackControlsAnchor) +
                ", panel=" + FormatElementRect(this.playbackControlsPanel) +
                ", root=" + FormatElementRect(this.root) +
                ", managerVisible=" + this.isManagerVisible +
                ", captureMode=" + this.captureModeActive +
                ", uiInputActive=" + this.playbackUiInputActive +
                ", mouseRequired=" + this.playbackUiMouseRequiredApplied);
        }

        private VisualElement FindClosestDropdownPopup(VisualElement visualTree, Rect dropdownRect)
        {
            if (visualTree == null)
            {
                return null;
            }

            VisualElement closest = null;
            float closestDistance = float.MaxValue;
            foreach (VisualElement popup in visualTree.Query<VisualElement>(null, "unity-base-dropdown").Build())
            {
                if (popup == null)
                {
                    continue;
                }

                float distance = Mathf.Abs(GetRectCenterX(popup.worldBound) - GetRectCenterX(dropdownRect)) +
                    Mathf.Abs(GetRectCenterY(popup.worldBound) - GetRectCenterY(dropdownRect));
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = popup;
                }
            }

            return closest;
        }

        private VisualElement FindClosestDropdownInner(VisualElement visualTree, Rect dropdownRect)
        {
            if (visualTree == null)
            {
                return null;
            }

            VisualElement closest = null;
            float closestDistance = float.MaxValue;
            foreach (VisualElement inner in visualTree.Query<VisualElement>(null, "unity-base-dropdown__container-inner").Build())
            {
                if (inner == null)
                {
                    continue;
                }

                Rect rect = inner.worldBound;
                float distance = Mathf.Abs(GetRectCenterX(rect) - GetRectCenterX(dropdownRect)) +
                    Mathf.Abs(GetRectCenterY(rect) - GetRectCenterY(dropdownRect));
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = inner;
                }
            }

            return closest;
        }

        private static string FormatDropdownItemRects(VisualElement popupInner)
        {
            if (popupInner == null)
            {
                return "none";
            }

            List<VisualElement> items = popupInner.Query<VisualElement>(null, "unity-base-dropdown__item").Build().ToList();
            if (items.Count == 0)
            {
                return "0";
            }

            return items.Count +
                ",first=" + FormatRect(items[0].worldBound) +
                ",last=" + FormatRect(items[items.Count - 1].worldBound);
        }

        private static float GetVerticalGap(Rect first, Rect second)
        {
            if (second.yMin > first.yMax)
            {
                return second.yMin - first.yMax;
            }

            if (first.yMin > second.yMax)
            {
                return first.yMin - second.yMax;
            }

            return 0f;
        }

        private static float GetRectCenterX(Rect rect)
        {
            return rect.xMin + rect.width * 0.5f;
        }

        private static float GetRectCenterY(Rect rect)
        {
            return rect.yMin + rect.height * 0.5f;
        }

        private static string FormatElementRect(VisualElement element)
        {
            return element != null ? FormatRect(element.worldBound) : "none";
        }

        private static string FormatRect(Rect rect)
        {
            return "x=" + rect.x.ToString("0.0", CultureInfo.InvariantCulture) +
                ",y=" + rect.y.ToString("0.0", CultureInfo.InvariantCulture) +
                ",w=" + rect.width.ToString("0.0", CultureInfo.InvariantCulture) +
                ",h=" + rect.height.ToString("0.0", CultureInfo.InvariantCulture);
        }

        private void ExtendPlaybackDropdownInteractionWindow()
        {
            this.playbackDropdownInteractionUntil = Mathf.Max(this.playbackDropdownInteractionUntil, Time.realtimeSinceStartup + 1.25f);
        }

        private bool IsPlaybackDropdownInteractionActive()
        {
            return Time.realtimeSinceStartup < this.playbackDropdownInteractionUntil;
        }

        private string GetPlaybackInputDebugState(EventBase evt)
        {
            VisualElement target = evt != null ? evt.target as VisualElement : null;
            string targetName = target != null ? (string.IsNullOrEmpty(target.name) ? target.GetType().Name : target.name) : "unknown";
            bool keyHeld = this.settings != null && this.IsKeyHeld(this.settings.PlaybackUiInputKey);
            return "target=" + targetName +
                ", active=" + this.playbackUiInputActive +
                ", mode=" + (this.settings != null ? this.settings.PlaybackUiInputMode.ToString() : "none") +
                ", keyHeld=" + keyHeld +
                ", mouseRequiredApplied=" + this.playbackUiMouseRequiredApplied +
                ", managerVisible=" + this.isManagerVisible +
                ", playbackActive=" + (this.playback != null && this.playback.IsPlaybackActive);
        }

        private void LogPlaybackUiDebug(string message, bool throttle)
        {
            if (this.settings == null || !this.settings.EnableDebugProfiling)
            {
                return;
            }

            float realtime = Time.realtimeSinceStartup;
            if (throttle && realtime < this.nextPlaybackUiDebugRealtime)
            {
                return;
            }

            if (throttle)
            {
                this.nextPlaybackUiDebugRealtime = realtime + 0.25f;
            }

            ReplayModLog.Info("[Playback UI] " + message);
        }

        private void OnPlaybackExitClicked()
        {
            if (!this.IsPlaybackControlInputAllowed())
            {
                return;
            }

            this.SetCaptureModeActive(false);
            this.playback.Close();
            this.RefreshPlaybackControls();
            this.RefreshPlaybackStatus();
        }

        private void OnPlaybackPlayPauseClicked()
        {
            if (!this.IsPlaybackControlInputAllowed())
            {
                return;
            }

            this.playback.TogglePause();
            this.RefreshPlaybackControls();
        }

        private enum PlaybackPlayPauseIconMode
        {
            Play,
            Pause
        }

        private sealed class PlaybackPlayPauseIcon : VisualElement
        {
            private PlaybackPlayPauseIconMode mode;
            private Color iconColor = Color.white;

            public PlaybackPlayPauseIcon(PlaybackPlayPauseIconMode mode, float size)
            {
                this.mode = mode;
                base.pickingMode = PickingMode.Ignore;
                base.style.width = size;
                base.style.height = size;
                base.generateVisualContent += this.OnGenerateVisualContent;
            }

            public void SetMode(PlaybackPlayPauseIconMode value)
            {
                if (this.mode == value)
                {
                    return;
                }

                this.mode = value;
                base.MarkDirtyRepaint();
            }

            public void SetColor(Color color)
            {
                if (this.iconColor == color)
                {
                    return;
                }

                this.iconColor = color;
                base.MarkDirtyRepaint();
            }

            private void OnGenerateVisualContent(MeshGenerationContext context)
            {
                Rect rect = base.contentRect;
                float size = Mathf.Min(rect.width, rect.height);
                if (size <= 0f)
                {
                    return;
                }

                Painter2D painter = context.painter2D;
                painter.fillColor = this.iconColor;
                if (this.mode == PlaybackPlayPauseIconMode.Play)
                {
                    this.DrawPlay(painter, rect, size);
                    return;
                }

                this.DrawPause(painter, rect, size);
            }

            private void DrawPlay(Painter2D painter, Rect rect, float size)
            {
                float left = rect.x + (rect.width * 0.34f);
                float right = rect.x + (rect.width * 0.72f);
                float top = rect.y + (rect.height * 0.22f);
                float middle = rect.y + (rect.height * 0.5f);
                float bottom = rect.y + (rect.height * 0.78f);

                painter.BeginPath();
                painter.MoveTo(new Vector2(left, top));
                painter.LineTo(new Vector2(right, middle));
                painter.LineTo(new Vector2(left, bottom));
                painter.ClosePath();
                painter.Fill();
            }

            private void DrawPause(Painter2D painter, Rect rect, float size)
            {
                float top = rect.y + (rect.height * 0.18f);
                float bottom = rect.y + (rect.height * 0.82f);
                float barWidth = size * 0.18f;
                float gap = size * 0.18f;
                float totalWidth = (barWidth * 2f) + gap;
                float left = rect.x + ((rect.width - totalWidth) * 0.5f);

                this.DrawRect(painter, left, top, barWidth, bottom - top);
                this.DrawRect(painter, left + barWidth + gap, top, barWidth, bottom - top);
            }

            private void DrawRect(Painter2D painter, float x, float y, float width, float height)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, y));
                painter.LineTo(new Vector2(x + width, y));
                painter.LineTo(new Vector2(x + width, y + height));
                painter.LineTo(new Vector2(x, y + height));
                painter.ClosePath();
                painter.Fill();
            }
        }

        private void CreateManagerPanel()
        {
            this.managerPanel = new VisualElement
            {
                name = "PuckReplayModManager"
            };
            this.managerPanel.style.position = Position.Absolute;
            this.managerPanel.style.left = new StyleLength(new Length(50f, LengthUnit.Percent));
            this.managerPanel.style.top = new StyleLength(new Length(50f, LengthUnit.Percent));
            this.managerPanel.style.translate = new Translate(new Length(-50f, LengthUnit.Percent), new Length(-50f, LengthUnit.Percent));
            this.managerPanel.style.minWidth = 620f;
            this.managerPanel.style.maxWidth = new StyleLength(new Length(96f, LengthUnit.Percent));
            this.managerPanel.style.minHeight = 420f;
            this.managerPanel.style.maxHeight = new StyleLength(new Length(94f, LengthUnit.Percent));
            this.managerPanel.style.backgroundColor = new StyleColor(ReplayUiTools.PanelColor);
            this.managerPanel.style.display = DisplayStyle.None;
            this.managerPanel.pickingMode = PickingMode.Ignore;

            this.CreateManagerHeader();
            this.CreateManagerBody();
            this.CreateManagerFooter();
            this.ApplyManagerWindowSize();
            this.ApplyManagerScale();

            this.root.Add(this.managerPanel);
            this.ShowSection(this.selectedSection);
        }

        private void CreateManagerHeader()
        {
            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.height = 70f;
            header.style.minHeight = 70f;
            header.style.paddingLeft = 14f;
            header.style.paddingRight = 14f;
            header.style.backgroundColor = new StyleColor(ReplayUiTools.HeaderColor);

            Label title = new Label("Replay Manager");
            title.style.fontSize = 28f;
            title.style.color = Color.white;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);

            VisualElement headerActions = new VisualElement();
            headerActions.style.flexDirection = FlexDirection.Row;
            headerActions.style.alignItems = Align.Center;
            header.Add(headerActions);

            this.updateBadgeButton = ReplayUiTools.CreateButton("UPDATE AVAILABLE", delegate
            {
                this.ShowSection("About / Updates");
            });
            this.updateBadgeButton.style.height = 32f;
            this.updateBadgeButton.style.minHeight = 32f;
            this.updateBadgeButton.style.marginRight = 10f;
            this.updateBadgeButton.style.paddingLeft = 10f;
            this.updateBadgeButton.style.paddingRight = 10f;
            this.updateBadgeButton.style.fontSize = 13f;
            this.updateBadgeButton.style.backgroundColor = new StyleColor(new Color(0.86f, 0.66f, 0.18f, 1f));
            this.updateBadgeButton.style.color = Color.black;
            this.updateBadgeButton.style.display = DisplayStyle.None;
            this.updateBadgeButton.RegisterCallback<MouseLeaveEvent>(delegate
            {
                this.RefreshUpdateBadge();
            });
            headerActions.Add(this.updateBadgeButton);

            this.storageBadgeButton = ReplayUiTools.CreateButton("STORAGE 0%", delegate
            {
                this.ShowSection("Storage");
            });
            this.storageBadgeButton.style.height = 32f;
            this.storageBadgeButton.style.minHeight = 32f;
            this.storageBadgeButton.style.marginRight = 10f;
            this.storageBadgeButton.style.paddingLeft = 10f;
            this.storageBadgeButton.style.paddingRight = 10f;
            this.storageBadgeButton.style.fontSize = 13f;
            this.storageBadgeButton.style.backgroundColor = new StyleColor(new Color(0.86f, 0.66f, 0.18f, 1f));
            this.storageBadgeButton.style.color = Color.black;
            this.storageBadgeButton.style.display = DisplayStyle.None;
            this.storageBadgeButton.RegisterCallback<MouseLeaveEvent>(delegate
            {
                this.RefreshStorageBadge();
            });
            headerActions.Add(this.storageBadgeButton);

            headerActions.Add(this.CreateVanillaCloseButton());
            this.managerPanel.Add(header);
            this.RefreshUpdateBadge();
            this.RefreshStorageBadge();
        }

        private VisualElement CreateVanillaCloseButton()
        {
            VisualElement closeButtonContainer = new VisualElement
            {
                name = "CloseIconButtonContainer"
            };
            closeButtonContainer.AddToClassList("CloseIconButtonContainer");
            closeButtonContainer.style.width = 42f;
            closeButtonContainer.style.minWidth = 42f;
            closeButtonContainer.style.height = 42f;
            closeButtonContainer.style.minHeight = 42f;
            closeButtonContainer.style.alignItems = Align.Center;
            closeButtonContainer.style.justifyContent = Justify.Center;

            Button closeButton = new Button(this.RequestManagerClose)
            {
                name = "IconButton",
                text = "X"
            };
            closeButton.AddToClassList("IconButton");
            closeButton.AddToClassList("CloseIconButton");
            ReplayUiTools.StyleConfigButton(closeButton);
            closeButton.style.width = 42f;
            closeButton.style.minWidth = 42f;
            closeButton.style.height = 42f;
            closeButton.style.minHeight = 42f;
            closeButton.style.paddingLeft = 0f;
            closeButton.style.paddingRight = 0f;
            closeButton.style.paddingTop = 0f;
            closeButton.style.paddingBottom = 0f;
            closeButton.style.fontSize = 22f;
            closeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            closeButtonContainer.Add(closeButton);
            return closeButtonContainer;
        }

        private void CreateManagerBody()
        {
            VisualElement body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1f;

            VisualElement sidebar = new VisualElement();
            sidebar.style.width = new StyleLength(new Length(28f, LengthUnit.Percent));
            sidebar.style.minWidth = 190f;
            sidebar.style.maxWidth = 250f;
            sidebar.style.backgroundColor = new StyleColor(ReplayUiTools.ControlColor);
            sidebar.style.flexShrink = 0f;
            body.Add(sidebar);

            ScrollView sidebarScroll = new ScrollView();
            sidebarScroll.style.flexGrow = 1f;
            sidebar.Add(sidebarScroll);

            for (int i = 0; i < this.sectionNames.Count; i++)
            {
                string sectionName = this.sectionNames[i];
                Button sectionButton = ReplayUiTools.CreateSidebarButton(this.GetSectionDisplayName(sectionName), delegate
                {
                    this.ShowSection(sectionName);
                });
                this.sectionButtons.Add(sectionButton);
                sidebarScroll.Add(sectionButton);
            }

            this.contentHost = new VisualElement
            {
                name = "PuckReplayModManagerContentHost"
            };
            this.contentHost.style.flexGrow = 1f;
            body.Add(this.contentHost);
            this.managerPanel.Add(body);
        }

        private void CreateManagerFooter()
        {
            VisualElement footer = new VisualElement();
            footer.style.height = 10f;
            footer.style.minHeight = 10f;
            footer.style.backgroundColor = new StyleColor(ReplayUiTools.HeaderColor);
            this.managerPanel.Add(footer);
        }

        private void ShowSection(string sectionName)
        {
            if (this.contentHost == null)
            {
                return;
            }

            if (sectionName == "Display" || sectionName == "Interface")
            {
                sectionName = "Display / Interface";
            }
            else if (sectionName == "Capture")
            {
                sectionName = "Playback";
            }

            this.selectedSection = sectionName;
            this.ReplayList = null;
            this.StorageLabel = null;
            this.StorageUsageLabel = null;
            this.PlaybackLabel = null;
            this.updateStatusLabel = null;
            this.content = null;
            this.contentHost.Clear();

            if (sectionName == "Library")
            {
                this.content = this.CreateSectionContent();
                this.contentHost.Add(this.content);
            }
            else
            {
                ScrollView scrollView = new ScrollView(ScrollViewMode.Vertical);
                scrollView.style.flexGrow = 1f;
                scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                this.contentHost.Add(scrollView);

                this.content = this.CreateSectionContent();
                scrollView.Add(this.content);
            }

            this.content.Clear();

            if (sectionName == "Recording")
            {
                ReplayRecordingSettingsSection.Create(this, this.content);
            }
            else if (sectionName == "Hotkeys")
            {
                ReplayHotkeySettingsSection.Create(this, this.content);
            }
            else if (sectionName == "Display / Interface")
            {
                ReplayOverlaySettingsSection.Create(this, this.content);
            }
            else if (sectionName == "Playback")
            {
                ReplayPlaybackSettingsSection.Create(this, this.content);
            }
            else if (sectionName == "Storage")
            {
                ReplayStorageSection.Create(this, this.content);
            }
            else if (sectionName == "About / Updates")
            {
                ReplayAboutSection.Create(this, this.content);
            }
            else if (sectionName == "Advanced")
            {
                ReplayAdvancedSettingsSection.Create(this, this.content);
            }
            else
            {
                ReplayLibrarySection.Create(this, this.content);
            }

            this.UpdateSidebarSelection();
        }

        private void PollUpdateCheck()
        {
            if (this.updateCheckTask == null || !this.updateCheckTask.IsCompleted)
            {
                return;
            }

            ReplayUpdateCheckResult result;
            try
            {
                result = this.updateCheckTask.Result;
            }
            catch (Exception exception)
            {
                result = new ReplayUpdateCheckResult
                {
                    Status = ReplayUpdateStatus.Error,
                    Message = "Update check failed: " + exception.GetBaseException().Message
                };
            }

            this.updateCheckTask = null;
            if (result == null)
            {
                this.updateStatus = ReplayUpdateStatus.Error;
                this.SetUpdateStatus("Update check failed.");
                this.RefreshUpdateBadge();
                return;
            }

            this.updateStatus = result.Status;
            this.updateDownloadUrl = result.DownloadUrl;
            this.SetUpdateStatus(result.Message);
            this.SetUpdatePatchNotes(result.PatchNotes);
            this.RefreshUpdateBadge();
        }

        private void SetUpdateStatus(string message)
        {
            this.updateStatusMessage = string.IsNullOrEmpty(message)
                ? "Check whether a newer Replay Mod version is available."
                : message;

            if (this.updateStatusLabel != null)
            {
                this.updateStatusLabel.text = this.updateStatusMessage;
            }

            ReplayModLog.Info("Update check: " + this.updateStatusMessage);
        }

        private void SetUpdatePatchNotes(string message)
        {
            this.updatePatchNotesMessage = string.IsNullOrEmpty(message)
                ? "No patch notes were provided by the update manifest."
                : message;

            if (this.updatePatchNotesLabel != null)
            {
                this.updatePatchNotesLabel.text = this.updatePatchNotesMessage;
            }
        }

        private void RefreshUpdateBadge()
        {
            this.RefreshMenuUpdateBadges();
            if (this.updateBadgeButton == null)
            {
                return;
            }

            bool shouldShow = this.updateStatus == ReplayUpdateStatus.UpdateAvailable ||
                this.updateStatus == ReplayUpdateStatus.UpdateRecommended;
            this.updateBadgeButton.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
            if (!shouldShow)
            {
                this.UpdateSidebarSelection();
                return;
            }

            bool recommended = this.updateStatus == ReplayUpdateStatus.UpdateRecommended;
            this.updateBadgeButton.text = recommended ? "UPDATE RECOMMENDED" : "UPDATE AVAILABLE";
            this.updateBadgeButton.tooltip = this.updateStatusMessage;
            this.updateBadgeButton.style.backgroundColor = new StyleColor(recommended
                ? new Color(0.9f, 0.28f, 0.2f, 1f)
                : new Color(0.86f, 0.66f, 0.18f, 1f));
            this.updateBadgeButton.style.color = Color.black;
            this.UpdateSidebarSelection();
        }

        private void RefreshMenuUpdateBadges()
        {
            bool shouldShow = this.updateStatus == ReplayUpdateStatus.UpdateAvailable ||
                this.updateStatus == ReplayUpdateStatus.UpdateRecommended;
            bool recommended = this.updateStatus == ReplayUpdateStatus.UpdateRecommended;
            this.RefreshMenuUpdateBadge(this.mainMenuReplayButton, this.mainMenuReplayUpdateBadge, shouldShow, recommended);
            this.RefreshMenuUpdateBadge(this.pauseMenuReplayButton, this.pauseMenuReplayUpdateBadge, shouldShow, recommended);
        }

        private void RefreshMenuUpdateBadge(Button button, Label badge, bool shouldShow, bool recommended)
        {
            if (button == null)
            {
                return;
            }

            button.text = "REPLAYS";
            button.tooltip = shouldShow
                ? (recommended ? "Replay Mod update recommended. Open About / Updates for details." : "Replay Mod update available. Open About / Updates for details.")
                : "Open Replay Mod.";

            if (badge == null)
            {
                return;
            }

            badge.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
            badge.text = recommended ? "!" : "!";
            badge.tooltip = button.tooltip;
            badge.style.backgroundColor = new StyleColor(recommended
                ? new Color(0.9f, 0.16f, 0.12f, 1f)
                : new Color(0.95f, 0.72f, 0.16f, 1f));
            badge.style.color = Color.black;
        }

        private void RefreshStorageBadge()
        {
            if (this.storageBadgeButton == null)
            {
                return;
            }

            if (this.settings == null || this.settings.StorageLimitMb <= 0 || this.storage == null)
            {
                this.storageBadgeButton.style.display = DisplayStyle.None;
                return;
            }

            long usedBytes = this.GetReplayStorageBytes();
            long limitBytes = (long)this.settings.StorageLimitMb * 1024L * 1024L;
            if (limitBytes <= 0L)
            {
                this.storageBadgeButton.style.display = DisplayStyle.None;
                return;
            }

            float ratio = usedBytes / (float)limitBytes;
            bool critical = ratio >= 0.95f;
            bool warning = ratio >= 0.8f;
            int percent = Mathf.RoundToInt(ratio * 100f);
            this.storageBadgeButton.style.display = DisplayStyle.Flex;
            this.storageBadgeButton.text = "STORAGE " + percent + "%";
            this.storageBadgeButton.tooltip =
                "Replay storage is at " + percent + "% of the configured " + this.settings.StorageLimitMb +
                " MB limit. Oldest replays are cleaned up automatically after saves.";
            this.storageBadgeButton.style.backgroundColor = new StyleColor(critical
                ? new Color(0.9f, 0.28f, 0.2f, 1f)
                : (warning ? new Color(0.86f, 0.66f, 0.18f, 1f) : new Color(0.24f, 0.58f, 0.32f, 1f)));
            this.storageBadgeButton.style.color = Color.black;
        }

        internal long GetReplayStorageBytes()
        {
            if (this.storage == null || string.IsNullOrEmpty(this.storage.ReplaysDirectory) || !Directory.Exists(this.storage.ReplaysDirectory))
            {
                return 0L;
            }

            long totalBytes = 0L;
            FileInfo[] files = new DirectoryInfo(this.storage.ReplaysDirectory).GetFiles("*" + ReplayModConstants.ReplayFileExtension);
            for (int i = 0; i < files.Length; i++)
            {
                totalBytes += files[i].Length;
            }

            return totalBytes;
        }

        private static ReplayUpdateCheckResult CheckForUpdatesCore()
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "PuckReplayMod/" + ReplayModConstants.ModVersion);
                    string json = client.DownloadString(ReplayModConstants.UpdateManifestUrl);
                    ReplayUpdateManifest manifest = JsonConvert.DeserializeObject<ReplayUpdateManifest>(json);
                    if (manifest == null || string.IsNullOrEmpty(manifest.LatestVersion))
                    {
                        return new ReplayUpdateCheckResult
                        {
                            Status = ReplayUpdateStatus.Error,
                            Message = "Update manifest did not include a latest version."
                        };
                    }

                    string latest = manifest.LatestVersion.Trim();
                    string minimum = manifest.MinimumRecommendedVersion != null ? manifest.MinimumRecommendedVersion.Trim() : string.Empty;
                    string downloadUrl = !string.IsNullOrEmpty(manifest.DownloadUrl) ? manifest.DownloadUrl : ReplayModConstants.UpdateDownloadUrl;
                    int latestCompare = CompareVersions(ReplayModConstants.ModVersion, latest);
                    int minimumCompare = string.IsNullOrEmpty(minimum) ? 0 : CompareVersions(ReplayModConstants.ModVersion, minimum);
                    bool isBehindLatest = latestCompare < 0;
                    string patchNotes = BuildPatchNotesMessage(manifest, latest, isBehindLatest);

                    if (minimumCompare < 0)
                    {
                        return new ReplayUpdateCheckResult
                        {
                            Status = ReplayUpdateStatus.UpdateRecommended,
                            DownloadUrl = downloadUrl,
                            Message = "Update recommended. Installed " + ReplayModConstants.ModVersion + ", recommended " + minimum + ", latest " + latest + ".",
                            PatchNotes = patchNotes
                        };
                    }

                    if (isBehindLatest)
                    {
                        return new ReplayUpdateCheckResult
                        {
                            Status = ReplayUpdateStatus.UpdateAvailable,
                            DownloadUrl = downloadUrl,
                            Message = "Update available. Installed " + ReplayModConstants.ModVersion + ", latest " + latest + ".",
                            PatchNotes = patchNotes
                        };
                    }

                    return new ReplayUpdateCheckResult
                    {
                        Status = ReplayUpdateStatus.UpToDate,
                        DownloadUrl = downloadUrl,
                        Message = "Replay Mod is up to date. Installed " + ReplayModConstants.ModVersion + ", latest " + latest + ".",
                        PatchNotes = patchNotes
                    };
                }
            }
            catch (Exception exception)
            {
                return new ReplayUpdateCheckResult
                {
                    Status = ReplayUpdateStatus.Error,
                    Message = "Update check failed: " + exception.Message,
                    PatchNotes = "Patch notes could not be loaded because the update check failed."
                };
            }
        }

        private static string BuildPatchNotesMessage(ReplayUpdateManifest manifest, string latestVersion, bool isBehindLatest)
        {
            List<ReplayUpdateRelease> releases = manifest.Releases ?? new List<ReplayUpdateRelease>();
            releases.RemoveAll(delegate(ReplayUpdateRelease release)
            {
                return release == null || string.IsNullOrEmpty(release.Version) || string.IsNullOrEmpty(release.Notes);
            });
            releases.Sort(delegate(ReplayUpdateRelease left, ReplayUpdateRelease right)
            {
                return CompareVersions(right.Version, left.Version);
            });

            if (releases.Count > 0)
            {
                List<ReplayUpdateRelease> selected = new List<ReplayUpdateRelease>();
                for (int i = 0; i < releases.Count; i++)
                {
                    ReplayUpdateRelease release = releases[i];
                    if (isBehindLatest && CompareVersions(ReplayModConstants.ModVersion, release.Version) < 0)
                    {
                        selected.Add(release);
                    }
                    else if (!isBehindLatest && CompareVersions(release.Version, latestVersion) == 0)
                    {
                        selected.Add(release);
                    }

                    if (selected.Count >= 5)
                    {
                        break;
                    }
                }

                if (selected.Count == 0)
                {
                    selected.Add(releases[0]);
                }

                string header = isBehindLatest ? "Changes Since Your Version" : "Latest Patch Notes";
                List<string> blocks = new List<string>
                {
                    header
                };
                for (int i = 0; i < selected.Count; i++)
                {
                    string version = selected[i].Version.Trim();
                    string notes = selected[i].Notes.Trim();
                    blocks.Add("v" + version + "\n" + notes);
                }

                return string.Join("\n\n", blocks.ToArray());
            }

            if (!string.IsNullOrEmpty(manifest.Notes))
            {
                return "Latest Patch Notes - v" + latestVersion + "\n" + manifest.Notes.Trim();
            }

            return "No patch notes were provided by the update manifest.";
        }

        private static int CompareVersions(string installedVersion, string remoteVersion)
        {
            Version installed;
            Version remote;
            if (Version.TryParse(NormalizeVersion(installedVersion), out installed) &&
                Version.TryParse(NormalizeVersion(remoteVersion), out remote))
            {
                return installed.CompareTo(remote);
            }

            return string.Compare(installedVersion ?? string.Empty, remoteVersion ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeVersion(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "0.0.0";
            }

            value = value.Trim();
            if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(1);
            }

            int suffixIndex = value.IndexOf('-');
            if (suffixIndex >= 0)
            {
                value = value.Substring(0, suffixIndex);
            }

            return string.IsNullOrEmpty(value) ? "0.0.0" : value;
        }

        private class ReplayUpdateManifest
        {
            public string LatestVersion { get; set; }
            public string MinimumRecommendedVersion { get; set; }
            public string DownloadUrl { get; set; }
            public string Notes { get; set; }
            public List<ReplayUpdateRelease> Releases { get; set; }
        }

        private class ReplayUpdateRelease
        {
            public string Version { get; set; }
            public string Date { get; set; }
            public string Notes { get; set; }
        }

        private class ReplayUpdateCheckResult
        {
            public ReplayUpdateStatus Status { get; set; }
            public string Message { get; set; }
            public string DownloadUrl { get; set; }
            public string PatchNotes { get; set; }
        }

        private enum ReplayUpdateStatus
        {
            Unknown,
            UpToDate,
            UpdateAvailable,
            UpdateRecommended,
            Error
        }

        private VisualElement CreateSectionContent()
        {
            VisualElement sectionContent = new VisualElement
            {
                name = "PuckReplayModManagerContent"
            };
            sectionContent.style.flexGrow = 1f;
            sectionContent.style.paddingLeft = 18f;
            sectionContent.style.paddingRight = 18f;
            sectionContent.style.paddingTop = 16f;
            sectionContent.style.paddingBottom = 16f;
            return sectionContent;
        }

        internal void ApplyManagerScale()
        {
            if (this.managerPanel == null)
            {
                return;
            }

            float scale = this.settings != null ? Mathf.Clamp(this.settings.ManagerUiScale, 0.85f, 1.3f) : 1f;
            this.managerPanel.style.scale = new StyleScale(new Scale(new Vector2(scale, scale)));
        }

        internal void ApplyManagerWindowSize()
        {
            if (this.managerPanel == null)
            {
                return;
            }

            float widthPercent = this.settings != null ? Mathf.Clamp(this.settings.ManagerWindowWidthPercent, 58f, 94f) : 72f;
            float heightPercent = this.settings != null ? Mathf.Clamp(this.settings.ManagerWindowHeightPercent, 58f, 92f) : 76f;
            this.managerPanel.style.width = new StyleLength(new Length(widthPercent, LengthUnit.Percent));
            this.managerPanel.style.height = new StyleLength(new Length(heightPercent, LengthUnit.Percent));
        }

        private void UpdateSidebarSelection()
        {
            for (int i = 0; i < this.sectionButtons.Count; i++)
            {
                if (i < this.sectionNames.Count)
                {
                    this.sectionButtons[i].text = this.GetSectionDisplayName(this.sectionNames[i]);
                }

                bool selected = i < this.sectionNames.Count && this.sectionNames[i] == this.selectedSection;
                ReplayUiTools.SetSidebarButtonSelected(this.sectionButtons[i], selected);
            }
        }

        private string GetSectionDisplayName(string sectionName)
        {
            if (sectionName == "About / Updates" &&
                (this.updateStatus == ReplayUpdateStatus.UpdateAvailable || this.updateStatus == ReplayUpdateStatus.UpdateRecommended))
            {
                return "About / Updates !";
            }

            return sectionName;
        }

        private void ToggleManager()
        {
            if (this.isManagerVisible)
            {
                this.HideManager();
                return;
            }

            this.ShowManager();
        }

        private void ShowManager()
        {
            if (this.managerPanel == null)
            {
                return;
            }

            this.isManagerVisible = true;
            this.managerPanel.style.display = DisplayStyle.Flex;
            this.root.pickingMode = PickingMode.Ignore;
            this.managerPanel.pickingMode = PickingMode.Position;
            this.ShowSection(this.selectedSection);
            this.RefreshPlaybackControls();
            this.RefreshStorageBadge();
            this.StartAutomaticUpdateCheck();

            try
            {
                GlobalStateManager.SetUIState(new Dictionary<string, object>
                {
                    { "isMouseRequired", true }
                });
            }
            catch (Exception)
            {
            }
        }

        private void StartAutomaticUpdateCheck()
        {
            if (this.hasStartedAutomaticUpdateCheck ||
                string.IsNullOrEmpty(ReplayModConstants.UpdateManifestUrl) ||
                (this.updateCheckTask != null && !this.updateCheckTask.IsCompleted))
            {
                return;
            }

            this.hasStartedAutomaticUpdateCheck = true;
            this.updateCheckTask = Task.Run(delegate
            {
                return CheckForUpdatesCore();
            });
        }

        private void TickReplayLibraryIndex()
        {
            if (!this.isManagerVisible || this.ReplayList == null || this.reader == null || this.storage == null)
            {
                return;
            }

            float realtime = Time.realtimeSinceStartup;
            if (realtime < this.nextReplayIndexRealtime)
            {
                return;
            }

            this.nextReplayIndexRealtime = realtime + 0.2f;
            if (this.reader.IndexNextMissingSummary(this.storage.ReplaysDirectory, this.storage.SummariesDirectory, 12))
            {
                this.RefreshReplayList();
            }
        }

        private void HideManager()
        {
            if (this.managerPanel == null)
            {
                return;
            }

            this.isManagerVisible = false;
            this.managerPanel.style.display = DisplayStyle.None;
            this.root.pickingMode = PickingMode.Ignore;
            this.managerPanel.pickingMode = PickingMode.Ignore;
            this.RefreshPlaybackControls();
        }

        internal bool TryCloseManager()
        {
            if (!this.isManagerVisible)
            {
                return false;
            }

            this.RequestManagerClose();
            return true;
        }

        private void RequestManagerClose()
        {
            EventManager.TriggerEvent("Event_OnReplayManagerClickClose", null);
        }

        private void PollManagerCloseHotkey()
        {
            if (!this.isManagerVisible)
            {
                return;
            }

            if (this.IsKeyPressed(KeyCode.Escape))
            {
                this.RequestManagerClose();
            }
        }

        private void PollPlaybackUiInputHotkey()
        {
            if (this.settings == null || this.playback == null || !this.playback.IsPlaybackActive)
            {
                if (this.playbackUiInputActive)
                {
                    this.playbackUiInputActive = false;
                    this.RefreshPlaybackControls();
                }

                return;
            }

            if (this.IsCaptureModeHidingPlaybackControls())
            {
                if (this.playbackUiInputActive)
                {
                    this.playbackUiInputActive = false;
                    this.RefreshPlaybackControls();
                }

                this.SetPlaybackUiMouseRequired(false);
                return;
            }

            bool previous = this.playbackUiInputActive;
            if (this.settings.PlaybackUiInputMode == ReplayPlaybackUiInputMode.Hold)
            {
                this.playbackUiInputActive = this.IsKeyHeld(this.settings.PlaybackUiInputKey);
            }
            else if (this.IsKeyPressed(this.settings.PlaybackUiInputKey))
            {
                this.playbackUiInputActive = !this.playbackUiInputActive;
            }

            if (previous != this.playbackUiInputActive)
            {
                this.LogPlaybackUiDebug("Playback UI input active changed: " + previous + " -> " + this.playbackUiInputActive + ".", false);
                this.RefreshPlaybackControls();
            }
        }

        private void PollCaptureModeHotkey()
        {
            if (this.settings == null)
            {
                return;
            }

            if (!this.IsKeyPressed(this.settings.CaptureModeKey))
            {
                return;
            }

            this.SetCaptureModeActive(!this.captureModeActive);
        }

        private void ResetCaptureModeWhenPlaybackEnds()
        {
            if (!this.captureModeActive || this.playback == null || this.playback.IsPlaybackActive)
            {
                return;
            }

            this.SetCaptureModeActive(false);
        }

        private string GetPlaybackCaptureModeButtonText()
        {
            string keyText = this.settings != null ? FormatKey(this.settings.CaptureModeKey) : "F10";
            return (this.captureModeActive ? "Show UI" : "Hide UI") + " (" + keyText + ")";
        }

        internal void SetCaptureModeActive(bool isActive)
        {
            if (this.captureModeActive == isActive)
            {
                return;
            }

            this.captureModeActive = isActive;
            if (this.captureModeActive && this.settings != null && this.settings.CaptureModeHidePlaybackControls)
            {
                this.playbackUiInputActive = false;
                this.SetPlaybackUiMouseRequired(false);
            }

            if (!this.captureModeActive)
            {
                this.RestoreCaptureHiddenElements();
            }

            this.RefreshStatusIndicator();
            this.RefreshTimelineIndicator();
            this.RefreshPlaybackControls();
            this.ApplyCaptureModeVisibility();
        }

        internal void RefreshCaptureModeVisibility()
        {
            this.RestoreCaptureHiddenElements();
            this.RefreshStatusIndicator();
            this.RefreshTimelineIndicator();
            this.RefreshPlaybackControls();
            this.ApplyCaptureModeVisibility();
        }

        private bool IsCaptureModeApplicable()
        {
            return this.captureModeActive && this.playback != null && this.playback.IsPlaybackActive && !this.isManagerVisible;
        }

        private bool IsCaptureModeHidingPlaybackControls()
        {
            return this.IsCaptureModeApplicable() && this.settings != null && this.settings.CaptureModeHidePlaybackControls;
        }

        private bool IsCaptureModeHidingReplayOverlays()
        {
            return this.IsCaptureModeApplicable() && this.settings != null && this.settings.CaptureModeHideReplayOverlays;
        }

        private void ApplyCaptureModeVisibility()
        {
            if (!this.IsCaptureModeApplicable() || this.settings == null)
            {
                this.RestoreCaptureHiddenElements();
                return;
            }

            UIManager uiManager = MonoBehaviourSingleton<UIManager>.Instance;
            if (uiManager == null)
            {
                return;
            }

            if (this.settings.CaptureModeHideGameHud)
            {
                this.HideForCapture(uiManager.GameState != null ? uiManager.GameState.View : null);
                this.HideForCapture(uiManager.Hud != null ? uiManager.Hud.View : null);
                this.HideForCapture(uiManager.Scoreboard != null ? uiManager.Scoreboard.View : null);
            }

            if (this.settings.CaptureModeHideChat)
            {
                this.HideForCapture(uiManager.Chat != null ? uiManager.Chat.View : null);
            }

            if (this.settings.CaptureModeHideMinimap)
            {
                this.HideForCapture(uiManager.Minimap != null ? uiManager.Minimap.View : null);
            }

            if (this.settings.CaptureModeHidePlayerNames)
            {
                this.HideForCapture(uiManager.Usernames != null ? uiManager.Usernames.View : null);
                this.HideForCapture(uiManager.Announcements != null ? uiManager.Announcements.View : null);
            }
        }

        private void HideForCapture(VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            if (!this.captureHiddenElements.ContainsKey(element))
            {
                this.captureHiddenElements[element] = element.style.visibility;
            }

            element.style.visibility = Visibility.Hidden;
        }

        private void RestoreCaptureHiddenElements()
        {
            if (this.captureHiddenElements.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<VisualElement, StyleEnum<Visibility>> entry in this.captureHiddenElements)
            {
                if (entry.Key != null)
                {
                    entry.Key.style.visibility = entry.Value;
                }
            }

            this.captureHiddenElements.Clear();
        }

        private void SetPlaybackUiMouseRequired(bool isRequired)
        {
            if (this.playbackUiMouseRequiredApplied == isRequired)
            {
                return;
            }

            this.playbackUiMouseRequiredApplied = isRequired;
            try
            {
                GlobalStateManager.SetUIState(new Dictionary<string, object>
                {
                    { "isMouseRequired", isRequired }
                });
            }
            catch (Exception)
            {
            }
        }

        private void PollDisplayHotkeys()
        {
            if (this.settings == null)
            {
                return;
            }

            if (this.IsKeyPressed(this.settings.ToggleStatusBadgeKey))
            {
                this.settings.ShowStatusIndicator = !this.settings.ShowStatusIndicator;
                this.settings.Save();
                this.RefreshStatusIndicator();
            }

            if (this.IsKeyPressed(this.settings.ToggleReplayTimeKey))
            {
                this.settings.ShowPlaybackTimeline = !this.settings.ShowPlaybackTimeline;
                this.settings.Save();
                this.RefreshTimelineIndicator();
            }
        }

        private bool IsKeyPressed(KeyCode keyCode)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            Key key;
            if (!TryConvertKey(keyCode, out key))
            {
                return false;
            }

            return keyboard[key] != null && keyboard[key].wasPressedThisFrame;
        }

        private bool IsKeyHeld(KeyCode keyCode)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            Key key;
            if (!TryConvertKey(keyCode, out key))
            {
                return false;
            }

            return keyboard[key] != null && keyboard[key].isPressed;
        }

        private bool IsScoreboardHeld()
        {
            try
            {
                if (InputManager.ScoreboardAction != null)
                {
                    return InputManager.ScoreboardAction.IsPressed();
                }
            }
            catch
            {
            }

            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.tabKey != null && keyboard.tabKey.isPressed;
        }

        private static bool TryConvertKey(KeyCode keyCode, out Key key)
        {
            string keyName = keyCode.ToString();
            if (keyName.StartsWith("Alpha", StringComparison.Ordinal))
            {
                keyName = "Digit" + keyName.Substring("Alpha".Length);
            }
            else if (keyName.EndsWith("Control", StringComparison.Ordinal))
            {
                keyName = keyName.Substring(0, keyName.Length - "Control".Length) + "Ctrl";
            }

            if (Enum.TryParse(keyName, true, out key))
            {
                return key != Key.None;
            }

            return false;
        }

        private static string FormatKey(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.LeftAlt:
                    return "Left Alt";
                case KeyCode.RightAlt:
                    return "Right Alt";
                case KeyCode.LeftControl:
                    return "Left Ctrl";
                case KeyCode.RightControl:
                    return "Right Ctrl";
                case KeyCode.LeftShift:
                    return "Left Shift";
                case KeyCode.RightShift:
                    return "Right Shift";
                default:
                    return keyCode.ToString();
            }
        }

        private void Event_HideManager(Dictionary<string, object> message)
        {
            this.HideManager();
            this.SyncMainMenuVisibilityFromUi();
            this.RefreshStatusIndicator();
        }

        private void Event_OnMainMenuShow(Dictionary<string, object> message)
        {
            this.isMainMenuVisible = true;
            this.RefreshStatusIndicator();
        }

        private void Event_OnMainMenuHide(Dictionary<string, object> message)
        {
            this.isMainMenuVisible = false;
            this.HideManager();
            this.RefreshStatusIndicator();
        }

        private void SyncMainMenuVisibilityFromUi()
        {
            UIManager uiManager = MonoBehaviourSingleton<UIManager>.Instance;
            this.isMainMenuVisible = uiManager == null || uiManager.MainMenu == null || uiManager.MainMenu.IsVisible;
        }

        private string FormatPlaybackTime(int tick)
        {
            int tickRate = Math.Max(1, this.playback.TickRate);
            TimeSpan timeSpan = TimeSpan.FromSeconds((double)tick / tickRate);
            if (timeSpan.TotalHours >= 1.0)
            {
                return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)timeSpan.TotalHours, timeSpan.Minutes, timeSpan.Seconds);
            }

            return string.Format("{0:D2}:{1:D2}", timeSpan.Minutes, timeSpan.Seconds);
        }
    }
}
