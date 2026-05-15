using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
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
        private readonly List<string> sectionNames = new List<string>
        {
            "Library",
            "Playback",
            "Recording",
            "Display",
            "Storage",
            "Advanced"
        };

        private VisualElement root;
        private VisualElement managerPanel;
        private VisualElement content;
        private Label statusLabel;
        private Label timelineLabel;
        private bool isManagerVisible;
        private bool mainMenuButtonAttached;
        private bool pauseMenuButtonAttached;
        private bool isMainMenuVisible = true;
        private string selectedSection = "Library";
        private float nextReplayIndexRealtime;

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
            this.recorder.RecordingStateChanged -= this.RefreshStatusIndicator;
            this.recorder.TickAdvanced -= this.RefreshStatusIndicator;

            if (this.root != null && this.root.parent != null)
            {
                this.root.parent.Remove(this.root);
            }

            this.root = null;
            this.managerPanel = null;
            this.content = null;
            this.statusLabel = null;
            this.timelineLabel = null;
            this.ReplayList = null;
            this.StorageLabel = null;
            this.StorageUsageLabel = null;
            this.PlaybackLabel = null;
            this.sectionButtons.Clear();
            this.isManagerVisible = false;
            this.isMainMenuVisible = true;
            this.mainMenuButtonAttached = false;
            this.pauseMenuButtonAttached = false;
        }

        public void Tick()
        {
            this.RefreshStatusIndicator();
            this.RefreshPlaybackStatus();
            this.TickReplayLibraryIndex();
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
                this.content = null;
                this.statusLabel = null;
                this.timelineLabel = null;
                this.ReplayList = null;
                this.StorageLabel = null;
                this.StorageUsageLabel = null;
                this.PlaybackLabel = null;
                this.sectionButtons.Clear();
                this.isManagerVisible = false;
                this.mainMenuButtonAttached = false;
                this.pauseMenuButtonAttached = false;
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
            this.mainMenuButtonAttached = this.AttachReplayButton(mainMenu, "PuckReplayModMainMenuButton");
        }

        public void AttachPauseMenuButton(VisualElement rootVisualElement)
        {
            if (this.pauseMenuButtonAttached)
            {
                return;
            }

            VisualElement pauseMenu = rootVisualElement != null ? rootVisualElement.Q<VisualElement>("PauseMenu") : null;
            this.pauseMenuButtonAttached = this.AttachReplayButton(pauseMenu, "PuckReplayModPauseMenuButton");
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

            string recordingState = this.recorder.IsRecording ? "Recording now" : (this.settings.AutoRecord ? "Ready to record" : "Automatic recording is off");
            this.StorageLabel.text = "Saved replays: " + replayCount + "\nStatus: " + recordingState + "\nReplay smoothness: " + ReplayRecordingSettingsSection.FormatCaptureRate(this.settings.CaptureTickRate);
        }

        internal void RefreshReplayList()
        {
            ReplayLibrarySection.RefreshReplayList(this);
        }

        internal void RefreshStorageUsage()
        {
            ReplayStorageSection.RefreshStorageUsage(this);
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

        internal void RefreshTimelineIndicator()
        {
            if (this.timelineLabel == null)
            {
                return;
            }

            if (!this.settings.ShowPlaybackTimeline || !this.playback.IsPlaying)
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

            if (this.isMainMenuVisible)
            {
                this.statusLabel.style.display = DisplayStyle.None;
                return;
            }

            bool shouldShowReady = this.settings.StatusIndicatorVisibility == ReplayIndicatorVisibility.Always;
            bool shouldShowPlayback = this.settings.StatusIndicatorVisibility == ReplayIndicatorVisibility.Always ||
                this.settings.StatusIndicatorVisibility == ReplayIndicatorVisibility.RecordingAndPlayback;
            bool shouldShowRecording = this.settings.StatusIndicatorVisibility == ReplayIndicatorVisibility.Always ||
                this.settings.StatusIndicatorVisibility == ReplayIndicatorVisibility.RecordingAndPlayback ||
                this.settings.StatusIndicatorVisibility == ReplayIndicatorVisibility.RecordingOnly;

            if (this.recorder.IsRecording)
            {
                this.statusLabel.style.display = shouldShowRecording ? DisplayStyle.Flex : DisplayStyle.None;
                this.statusLabel.text = "REPLAY MOD  REC  " + this.recorder.CurrentTick;
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

        private bool AttachReplayButton(VisualElement menu, string name)
        {
            if (menu == null || menu.Q<Button>(name) != null)
            {
                return menu != null;
            }

            Button referenceButton = menu.Q<Button>("ModsButton") ?? menu.Q<Button>("SettingsButton") ?? menu.Q<Button>("ServerBrowserButton");
            Button button = new Button(this.ToggleManager)
            {
                name = name,
                text = "REPLAYS"
            };
            ReplayUiTools.StyleMenuAccessButton(referenceButton, button);

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
            this.managerPanel.style.width = new StyleLength(new Length(72f, LengthUnit.Percent));
            this.managerPanel.style.maxWidth = 980f;
            this.managerPanel.style.minWidth = 660f;
            this.managerPanel.style.height = new StyleLength(new Length(76f, LengthUnit.Percent));
            this.managerPanel.style.maxHeight = 720f;
            this.managerPanel.style.minHeight = 460f;
            this.managerPanel.style.backgroundColor = new StyleColor(ReplayUiTools.PanelColor);
            this.managerPanel.style.display = DisplayStyle.None;
            this.managerPanel.pickingMode = PickingMode.Ignore;

            this.CreateManagerHeader();
            this.CreateManagerBody();
            this.CreateManagerFooter();

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

            Button closeButton = ReplayUiTools.CreateButton("CLOSE", this.HideManager);
            closeButton.text = "X";
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
            header.Add(closeButton);
            this.managerPanel.Add(header);
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
                Button sectionButton = ReplayUiTools.CreateSidebarButton(sectionName, delegate
                {
                    this.ShowSection(sectionName);
                });
                this.sectionButtons.Add(sectionButton);
                sidebarScroll.Add(sectionButton);
            }

            ScrollView scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1f;
            scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            this.content = new VisualElement
            {
                name = "PuckReplayModManagerContent"
            };
            this.content.style.paddingLeft = 18f;
            this.content.style.paddingRight = 18f;
            this.content.style.paddingTop = 16f;
            this.content.style.paddingBottom = 16f;
            scrollView.Add(this.content);
            body.Add(scrollView);
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
            if (this.content == null)
            {
                return;
            }

            this.selectedSection = sectionName;
            this.ReplayList = null;
            this.StorageLabel = null;
            this.StorageUsageLabel = null;
            this.PlaybackLabel = null;
            this.content.Clear();

            if (sectionName == "Playback")
            {
                ReplayPlaybackSection.Create(this, this.content);
            }
            else if (sectionName == "Recording")
            {
                ReplayRecordingSettingsSection.Create(this, this.content);
            }
            else if (sectionName == "Display")
            {
                ReplayOverlaySettingsSection.Create(this, this.content);
            }
            else if (sectionName == "Storage")
            {
                ReplayStorageSection.Create(this, this.content);
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

        private void UpdateSidebarSelection()
        {
            for (int i = 0; i < this.sectionButtons.Count; i++)
            {
                bool selected = i < this.sectionNames.Count && this.sectionNames[i] == this.selectedSection;
                ReplayUiTools.SetSidebarButtonSelected(this.sectionButtons[i], selected);
            }
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
