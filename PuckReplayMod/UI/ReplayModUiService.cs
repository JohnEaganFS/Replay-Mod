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

        private VisualElement root;
        private VisualElement libraryPanel;
        private VisualElement replayList;
        private Label statusLabel;
        private Label timelineLabel;
        private Label storageLabel;
        private Label playbackLabel;
        private bool isLibraryVisible;
        private bool mainMenuButtonAttached;
        private bool pauseMenuButtonAttached;

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
            EventManager.AddEventListener("Event_OnClientStarted", this.Event_HideLibrary);
            EventManager.AddEventListener("Event_OnClientStopped", this.Event_HideLibrary);
            EventManager.AddEventListener("Event_OnMainMenuHide", this.Event_HideLibrary);
            EventManager.AddEventListener("Event_OnMainMenuClickPlay", this.Event_HideLibrary);
            EventManager.AddEventListener("Event_OnPauseMenuClickSelectTeam", this.Event_HideLibrary);
            EventManager.AddEventListener("Event_OnPauseMenuClickSelectPosition", this.Event_HideLibrary);
            EventManager.AddEventListener("Event_OnPauseMenuClickServerBrowser", this.Event_HideLibrary);
            EventManager.AddEventListener("Event_OnPauseMenuClickDisconnect", this.Event_HideLibrary);
        }

        public void Dispose()
        {
            EventManager.RemoveEventListener("Event_OnClientStarted", this.Event_HideLibrary);
            EventManager.RemoveEventListener("Event_OnClientStopped", this.Event_HideLibrary);
            EventManager.RemoveEventListener("Event_OnMainMenuHide", this.Event_HideLibrary);
            EventManager.RemoveEventListener("Event_OnMainMenuClickPlay", this.Event_HideLibrary);
            EventManager.RemoveEventListener("Event_OnPauseMenuClickSelectTeam", this.Event_HideLibrary);
            EventManager.RemoveEventListener("Event_OnPauseMenuClickSelectPosition", this.Event_HideLibrary);
            EventManager.RemoveEventListener("Event_OnPauseMenuClickServerBrowser", this.Event_HideLibrary);
            EventManager.RemoveEventListener("Event_OnPauseMenuClickDisconnect", this.Event_HideLibrary);
            this.recorder.RecordingStateChanged -= this.RefreshStatusIndicator;
            this.recorder.TickAdvanced -= this.RefreshStatusIndicator;

            if (this.root != null && this.root.parent != null)
            {
                this.root.parent.Remove(this.root);
            }

            this.root = null;
            this.libraryPanel = null;
            this.replayList = null;
            this.statusLabel = null;
            this.timelineLabel = null;
            this.storageLabel = null;
            this.playbackLabel = null;
            this.mainMenuButtonAttached = false;
            this.pauseMenuButtonAttached = false;
        }

        public void Tick()
        {
            this.RefreshStatusIndicator();
            this.RefreshPlaybackStatus();
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
                this.libraryPanel = null;
                this.replayList = null;
                this.statusLabel = null;
                this.timelineLabel = null;
                this.storageLabel = null;
                this.playbackLabel = null;
                this.isLibraryVisible = false;
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
            this.CreateLibraryPanel();
            uiManager.RootVisualElement.Add(this.root);
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

        private bool AttachReplayButton(VisualElement menu, string name)
        {
            if (menu == null || menu.Q<Button>(name) != null)
            {
                return menu != null;
            }

            Button referenceButton = menu.Q<Button>("ModsButton") ?? menu.Q<Button>("SettingsButton") ?? menu.Q<Button>("ServerBrowserButton");
            Button button = new Button(this.ToggleLibrary)
            {
                name = name,
                text = "REPLAYS"
            };
            this.MatchMenuButtonStyle(referenceButton, button);

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

        private void MatchMenuButtonStyle(Button referenceButton, Button button)
        {
            if (referenceButton == null)
            {
                return;
            }

            foreach (string className in referenceButton.GetClasses())
            {
                button.AddToClassList(className);
            }

            if (!float.IsNaN(referenceButton.resolvedStyle.width) && referenceButton.resolvedStyle.width > 0f)
            {
                button.style.width = referenceButton.resolvedStyle.width;
            }

            if (!float.IsNaN(referenceButton.resolvedStyle.height) && referenceButton.resolvedStyle.height > 0f)
            {
                button.style.height = referenceButton.resolvedStyle.height;
                button.style.marginLeft = referenceButton.resolvedStyle.marginLeft;
                button.style.marginRight = referenceButton.resolvedStyle.marginRight;
                button.style.marginTop = referenceButton.resolvedStyle.marginTop;
                button.style.marginBottom = referenceButton.resolvedStyle.marginBottom;
                button.style.paddingLeft = referenceButton.resolvedStyle.paddingLeft;
                button.style.paddingRight = referenceButton.resolvedStyle.paddingRight;
                button.style.paddingTop = referenceButton.resolvedStyle.paddingTop;
                button.style.paddingBottom = referenceButton.resolvedStyle.paddingBottom;
            }

            button.style.fontSize = referenceButton.resolvedStyle.fontSize;
            button.style.unityTextAlign = referenceButton.resolvedStyle.unityTextAlign;
            button.style.unityFontStyleAndWeight = referenceButton.resolvedStyle.unityFontStyleAndWeight;
        }

        private void CreateStatusIndicator()
        {
            this.statusLabel = new Label("IDLE")
            {
                name = "PuckReplayModStatus"
            };
            this.statusLabel.style.position = Position.Absolute;
            this.statusLabel.style.right = 18f;
            this.statusLabel.style.top = 18f;
            this.statusLabel.style.paddingLeft = 8f;
            this.statusLabel.style.paddingRight = 8f;
            this.statusLabel.style.paddingTop = 4f;
            this.statusLabel.style.paddingBottom = 4f;
            this.statusLabel.style.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.72f);
            this.statusLabel.style.color = Color.white;
            this.statusLabel.style.fontSize = 13f;
            this.statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            this.statusLabel.pickingMode = PickingMode.Ignore;
            this.root.Add(this.statusLabel);
        }

        private void CreateTimelineIndicator()
        {
            this.timelineLabel = new Label
            {
                name = "PuckReplayModTimeline"
            };
            this.timelineLabel.style.position = Position.Absolute;
            this.timelineLabel.style.right = 18f;
            this.timelineLabel.style.top = 52f;
            this.timelineLabel.style.paddingLeft = 8f;
            this.timelineLabel.style.paddingRight = 8f;
            this.timelineLabel.style.paddingTop = 4f;
            this.timelineLabel.style.paddingBottom = 4f;
            this.timelineLabel.style.backgroundColor = new Color(0.04f, 0.06f, 0.08f, 0.78f);
            this.timelineLabel.style.color = Color.white;
            this.timelineLabel.style.fontSize = 12f;
            this.timelineLabel.style.display = DisplayStyle.None;
            this.timelineLabel.pickingMode = PickingMode.Ignore;
            this.root.Add(this.timelineLabel);
        }

        private void CreateLibraryPanel()
        {
            this.libraryPanel = new VisualElement
            {
                name = "PuckReplayModLibrary"
            };
            this.libraryPanel.style.position = Position.Absolute;
            this.libraryPanel.style.left = 80f;
            this.libraryPanel.style.top = 80f;
            this.libraryPanel.style.width = 520f;
            this.libraryPanel.style.minHeight = 260f;
            this.libraryPanel.style.paddingLeft = 16f;
            this.libraryPanel.style.paddingRight = 16f;
            this.libraryPanel.style.paddingTop = 14f;
            this.libraryPanel.style.paddingBottom = 14f;
            this.libraryPanel.style.backgroundColor = new Color(0.08f, 0.09f, 0.1f, 0.94f);
            this.libraryPanel.style.display = DisplayStyle.None;
            this.libraryPanel.pickingMode = PickingMode.Ignore;

            Label title = new Label("REPLAYS");
            title.style.fontSize = 18f;
            title.style.color = Color.white;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            this.libraryPanel.Add(title);

            this.storageLabel = new Label();
            this.storageLabel.style.marginTop = 8f;
            this.storageLabel.style.color = new Color(0.82f, 0.86f, 0.9f, 1f);
            this.storageLabel.style.whiteSpace = WhiteSpace.Normal;
            this.libraryPanel.Add(this.storageLabel);

            this.playbackLabel = new Label();
            this.playbackLabel.style.marginTop = 8f;
            this.playbackLabel.style.color = new Color(0.76f, 0.82f, 0.88f, 1f);
            this.playbackLabel.style.whiteSpace = WhiteSpace.Normal;
            this.libraryPanel.Add(this.playbackLabel);

            this.replayList = new VisualElement
            {
                name = "PuckReplayModReplayList"
            };
            this.replayList.style.marginTop = 10f;
            this.libraryPanel.Add(this.replayList);

            Button markerButton = new Button(delegate
            {
                this.recorder.AddMarker();
            })
            {
                text = "ADD MARKER"
            };
            markerButton.style.marginTop = 12f;
            markerButton.style.height = 34f;
            this.libraryPanel.Add(markerButton);

            Button stopPlaybackButton = new Button(delegate
            {
                this.playback.Close();
                this.RefreshPlaybackStatus();
            })
            {
                text = "CLOSE PLAYBACK"
            };
            stopPlaybackButton.style.marginTop = 8f;
            stopPlaybackButton.style.height = 34f;
            this.libraryPanel.Add(stopPlaybackButton);

            Button closeButton = new Button(this.HideLibrary)
            {
                text = "CLOSE"
            };
            closeButton.style.marginTop = 8f;
            closeButton.style.height = 34f;
            this.libraryPanel.Add(closeButton);

            this.root.Add(this.libraryPanel);
        }

        private void ToggleLibrary()
        {
            if (this.isLibraryVisible)
            {
                this.HideLibrary();
                return;
            }

            this.ShowLibrary();
        }

        private void ShowLibrary()
        {
            if (this.libraryPanel == null)
            {
                return;
            }

            this.isLibraryVisible = true;
            this.libraryPanel.style.display = DisplayStyle.Flex;
            this.root.pickingMode = PickingMode.Ignore;
            this.libraryPanel.pickingMode = PickingMode.Position;
            this.RefreshLibraryText();
            this.RefreshReplayList();
            this.RefreshPlaybackStatus();
        }

        private void HideLibrary()
        {
            if (this.libraryPanel == null)
            {
                return;
            }

            this.isLibraryVisible = false;
            this.libraryPanel.style.display = DisplayStyle.None;
            this.root.pickingMode = PickingMode.Ignore;
            this.libraryPanel.pickingMode = PickingMode.Ignore;
        }

        private void Event_HideLibrary(System.Collections.Generic.Dictionary<string, object> message)
        {
            this.HideLibrary();
        }

        private void RefreshLibraryText()
        {
            if (this.storageLabel == null)
            {
                return;
            }

            int replayCount = 0;
            if (Directory.Exists(this.storage.ReplaysDirectory))
            {
                replayCount = Directory.GetFiles(this.storage.ReplaysDirectory, "*" + ReplayModConstants.ReplayFileExtension).Length;
            }

            this.storageLabel.text = "Stored replays: " + replayCount + "\nFolder: " + this.storage.ReplaysDirectory + "\nCapture: " + this.settings.CaptureTickRate + " ticks/sec";
        }

        private void RefreshReplayList()
        {
            if (this.replayList == null)
            {
                return;
            }

            this.replayList.Clear();
            List<ReplayFileSummary> replays = this.reader.GetRecentReplays(this.storage.ReplaysDirectory, 6);
            if (replays.Count == 0)
            {
                Label emptyLabel = new Label("No replays found.");
                emptyLabel.style.color = new Color(0.76f, 0.82f, 0.88f, 1f);
                this.replayList.Add(emptyLabel);
                return;
            }

            foreach (ReplayFileSummary replay in replays)
            {
                ReplayFileSummary selectedReplay = replay;
                Button replayButton = new Button(delegate
                {
                    this.PlayReplay(selectedReplay.FilePath);
                })
                {
                    text = this.FormatReplayButtonText(selectedReplay)
                };
                replayButton.style.height = 34f;
                replayButton.style.marginTop = 4f;
                this.replayList.Add(replayButton);
            }
        }

        private string FormatReplayButtonText(ReplayFileSummary replay)
        {
            return "PLAY  " + replay.LastWriteUtc.ToLocalTime().ToString("MM/dd HH:mm") +
                "  " + replay.DurationSeconds.ToString("0") + "s  " +
                replay.ServerName;
        }

        private void PlayReplay(string filePath)
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
                this.HideLibrary();
            }
            catch (Exception exception)
            {
                ReplayModLog.Warning("Failed to play replay " + filePath + ": " + exception.Message);
            }
        }

        private void RefreshPlaybackStatus()
        {
            this.RefreshTimelineIndicator();
            if (this.playbackLabel == null)
            {
                return;
            }

            if (this.playback.IsPlaying)
            {
                this.playbackLabel.text = "Playback: " + this.playback.PlaybackMode + "  " + this.playback.CurrentTick + " / " + this.playback.TotalTicks;
                return;
            }

            this.playbackLabel.text = "Playback: idle";
        }

        private void RefreshTimelineIndicator()
        {
            if (this.timelineLabel == null)
            {
                return;
            }

            if (!this.playback.IsPlaying)
            {
                this.timelineLabel.style.display = DisplayStyle.None;
                return;
            }

            int currentTick = Math.Max(0, this.playback.CurrentTick);
            int totalTicks = Math.Max(currentTick, this.playback.TotalTicks);
            this.timelineLabel.text = "REPLAY  " + this.FormatPlaybackTime(currentTick) + " / " + this.FormatPlaybackTime(totalTicks);
            this.timelineLabel.style.display = DisplayStyle.Flex;
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

        private void RefreshStatusIndicator()
        {
            if (this.statusLabel == null)
            {
                return;
            }

            if (this.recorder.IsRecording)
            {
                this.statusLabel.text = "REPLAY MOD  REC  " + this.recorder.CurrentTick;
                this.statusLabel.style.backgroundColor = new Color(0.55f, 0.05f, 0.06f, 0.9f);
                return;
            }

            if (this.recorder.IsRecordingSuppressed)
            {
                this.statusLabel.text = "REPLAY MOD  PLAYBACK";
                this.statusLabel.style.backgroundColor = new Color(0.1f, 0.18f, 0.32f, 0.9f);
                return;
            }

            this.statusLabel.text = "REPLAY MOD  READY";
            this.statusLabel.style.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.72f);
        }
    }
}
