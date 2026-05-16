using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using Unity.Netcode;
using UnityEngine;

namespace PuckReplayMod
{
    public class NativeReplayPlaybackService
    {
        private static readonly FieldInfo ReplayPuckNetworkObjectIdMapField = typeof(ReplayPlayer).GetField("replayPuckNetworkObjectIdMap", BindingFlags.Instance | BindingFlags.NonPublic);
        private const ulong ReplayOwnerClientIdOffset = 1337UL;
        internal static readonly Vector3 ReplaySpectatorSpawnPosition = new Vector3(0f, 6f, 0f);
        internal static readonly Quaternion ReplaySpectatorSpawnRotation = Quaternion.Euler(22f, 0f, 0f);

        private readonly NativeReplayEventConverter converter = new NativeReplayEventConverter();
        private readonly ReplayModSettings settings;
        private readonly ReplayGameStatePlaybackService gameStatePlayback;
        private readonly List<CameraTargetTimelineEvent> cameraTargetTimeline = new List<CameraTargetTimelineEvent>();

        private ReplayPlayer replayPlayer;
        private ReplaySessionData currentSession;
        private string currentFilePath;
        private SortedList<int, List<ValueTuple<string, object>>> currentEventMap;
        private int currentTickRate = 30;
        private bool replaySpectatorCameraAdjusted;
        private ReplayPlaybackCameraMode cameraMode = ReplayPlaybackCameraMode.Free;
        private ulong? cameraTargetClientId;
        private ulong? hiddenFirstPersonTargetClientId;
        private PlayerBody hiddenFirstPersonTargetBody;
        private readonly List<RendererVisibilityState> hiddenFirstPersonRenderers = new List<RendererVisibilityState>();
        private readonly Dictionary<ulong, ReplayPlayerBodyMove> pausedBodySnapshots = new Dictionary<ulong, ReplayPlayerBodyMove>();
        private readonly Dictionary<ulong, ReplayStickMove> pausedStickSnapshots = new Dictionary<ulong, ReplayStickMove>();
        private readonly Dictionary<ulong, ReplayPuckMove> pausedPuckSnapshots = new Dictionary<ulong, ReplayPuckMove>();
        private bool firstPersonCameraSmoothingInitialized;
        private ulong? firstPersonCameraSmoothingTargetClientId;
        private Quaternion firstPersonCameraSmoothedRotation = Quaternion.identity;
        private float nextSlowMotionInterpolationDebugRealtime;

        public NativeReplayPlaybackService(ReplayModSettings settings)
        {
            this.settings = settings;
            this.gameStatePlayback = new ReplayGameStatePlaybackService(settings);
        }

        public bool IsPlaying { get; private set; }

        public string CurrentFilePath
        {
            get { return this.currentFilePath; }
        }

        public int CurrentTick
        {
            get
            {
                return ReplayPlaybackRuntime.GetVisibleTick(this.replayPlayer);
            }
        }

        public bool IsPaused
        {
            get { return this.IsPlaying && ReplayPlaybackRuntime.IsPaused; }
        }

        public float PlaybackSpeed
        {
            get { return ReplayPlaybackRuntime.PlaybackSpeed; }
        }

        public ReplayPlaybackCameraMode CameraMode
        {
            get { return this.cameraMode; }
        }

        public ulong? CameraTargetClientId
        {
            get { return this.cameraTargetClientId; }
        }

        public bool CanPlay
        {
            get
            {
                return NetworkManager.Singleton != null &&
                    NetworkManager.Singleton.IsHost &&
                    NetworkManager.Singleton.ConnectedClientsList != null &&
                    NetworkManager.Singleton.ConnectedClientsList.Count <= 1 &&
                    MonoBehaviourSingleton<ReplayManager>.Instance != null &&
                    MonoBehaviourSingleton<ReplayManager>.Instance.ReplayPlayer != null;
            }
        }

        public bool CanStartLocalReplaySession
        {
            get
            {
                return NetworkManager.Singleton != null &&
                    !NetworkManager.Singleton.IsClient &&
                    !NetworkManager.Singleton.IsServer &&
                    NetworkBehaviourSingleton<ServerManager>.Instance != null;
            }
        }

        public bool TryStartLocalReplaySession()
        {
            if (!this.CanStartLocalReplaySession)
            {
                return false;
            }

            this.replaySpectatorCameraAdjusted = false;
            ReplayModLog.Info("Starting local practice session for native replay playback.");
            EventManager.TriggerEvent("Event_OnPlayClickPractice", null);
            return true;
        }

        public string GetUnavailableReason()
        {
            if (NetworkManager.Singleton == null)
            {
                return "network manager is unavailable";
            }

            if (!NetworkManager.Singleton.IsHost)
            {
                return "not in a local host/practice session";
            }

            if (NetworkManager.Singleton.ConnectedClientsList != null && NetworkManager.Singleton.ConnectedClientsList.Count > 1)
            {
                return "local host has connected clients";
            }

            if (MonoBehaviourSingleton<ReplayManager>.Instance == null || MonoBehaviourSingleton<ReplayManager>.Instance.ReplayPlayer == null)
            {
                return "native ReplayPlayer is unavailable";
            }

            return "unknown";
        }

        public bool TryPlay(ReplaySessionData session, string filePath)
        {
            if (!this.CanPlay)
            {
                return false;
            }

            ReplayManager replayManager = MonoBehaviourSingleton<ReplayManager>.Instance;
            this.replayPlayer = replayManager.ReplayPlayer;
            SortedList<int, List<ValueTuple<string, object>>> eventMap = this.converter.Convert(session);
            if (eventMap.Count == 0)
            {
                throw new InvalidOperationException("Replay has no native events to play.");
            }

            this.currentSession = session;
            this.currentEventMap = eventMap;
            this.currentTickRate = session != null && session.Header != null ? Math.Max(1, session.Header.TickRate) : 30;
            this.BuildCameraTargetRoster(session);
            this.EnsureCameraTarget();

            if (this.replayPlayer.IsReplaying)
            {
                this.replayPlayer.Server_StopReplay();
            }

            this.replaySpectatorCameraAdjusted = false;
            this.PrepareSceneForReplay();
            this.replayPlayer.Server_StartReplay(this.CloneEventMap(), this.currentTickRate, 0);
            this.currentFilePath = filePath;
            this.IsPlaying = this.replayPlayer.IsReplaying;
            if (this.IsPlaying)
            {
                ReplayPlaybackRuntime.Attach(this.replayPlayer);
                this.gameStatePlayback.Start(session);
                this.gameStatePlayback.ApplyThrough(this.CurrentTick);
                ReplayGameStatePlaybackService.EnsureReplayObjectsOnMinimap();
            }

            return this.IsPlaying;
        }

        public void Tick()
        {
            if (!this.IsPlaying)
            {
                return;
            }

            if (this.replayPlayer == null || !this.replayPlayer.IsReplaying)
            {
                this.IsPlaying = false;
                this.gameStatePlayback.Close();
                return;
            }

            this.EnforcePausedReplayObjectSnapshots();
            this.gameStatePlayback.ApplyThrough(this.CurrentTick);
            this.ApplySlowMotionInterpolation();
        }

        public void SetPaused(bool paused)
        {
            if (!this.IsPlaying)
            {
                return;
            }

            ReplayPlaybackRuntime.SetPaused(paused);
            if (paused)
            {
                this.ApplyLatestTransformsThrough(this.CurrentTick);
                this.EnforcePausedReplayObjectSnapshots();
            }
        }

        public void SetPlaybackSpeed(float speed)
        {
            ReplayPlaybackRuntime.SetPlaybackSpeed(speed);
        }

        public void SetCameraMode(ReplayPlaybackCameraMode mode)
        {
            if (this.cameraMode == ReplayPlaybackCameraMode.FirstPerson && mode != ReplayPlaybackCameraMode.FirstPerson)
            {
                this.RestoreFirstPersonTargetRenderers();
            }

            this.cameraMode = mode;
            this.ResetFirstPersonCameraSmoothing();
            this.EnsureCameraTarget();
            if (this.cameraMode == ReplayPlaybackCameraMode.Free)
            {
                this.RestoreLocalSpectatorFieldOfView();
            }
        }

        public void SetCameraTarget(ulong? ownerClientId)
        {
            if (this.cameraMode == ReplayPlaybackCameraMode.FirstPerson && this.cameraTargetClientId != ownerClientId)
            {
                this.RestoreFirstPersonTargetRenderers();
            }

            this.cameraTargetClientId = ownerClientId;
            this.ResetFirstPersonCameraSmoothing();
            this.EnsureCameraTarget();
        }

        public List<ReplayPlaybackPlayerTarget> GetCameraTargets()
        {
            int currentTick = this.CurrentTick;
            Dictionary<ulong, string> labelsByClientId = new Dictionary<ulong, string>();
            Dictionary<ulong, PlayerSnapshotPayload> availableTargets = this.GetAvailableCameraTargetSnapshots(currentTick);
            foreach (PlayerSnapshotPayload playerSnapshot in availableTargets.Values)
            {
                if (playerSnapshot == null)
                {
                    continue;
                }

                labelsByClientId[playerSnapshot.OwnerClientId] = FormatCameraTargetLabel(playerSnapshot);
            }

            PlayerManager playerManager = MonoBehaviourSingleton<PlayerManager>.Instance;
            if (playerManager != null)
            {
                foreach (Player player in playerManager.GetReplayPlayers())
                {
                    if (player == null)
                    {
                        continue;
                    }

                    ulong originalOwnerClientId = ToOriginalReplayOwnerClientId(player.OwnerClientId);
                    if (availableTargets.ContainsKey(originalOwnerClientId))
                    {
                        labelsByClientId[originalOwnerClientId] = FormatCameraTargetLabel(player, originalOwnerClientId);
                    }
                }
            }

            List<ReplayPlaybackPlayerTarget> targets = new List<ReplayPlaybackPlayerTarget>(labelsByClientId.Count);
            foreach (KeyValuePair<ulong, string> entry in labelsByClientId)
            {
                targets.Add(new ReplayPlaybackPlayerTarget(entry.Key, entry.Value));
            }

            targets.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));
            return targets;
        }

        public bool TryApplyPovCamera(SpectatorCamera spectatorCamera, float deltaTime)
        {
            if (!this.IsPlaying || this.cameraMode == ReplayPlaybackCameraMode.Free || spectatorCamera == null || !spectatorCamera.IsOwner)
            {
                if (this.cameraMode != ReplayPlaybackCameraMode.FirstPerson)
                {
                    this.RestoreFirstPersonTargetRenderers();
                }

                return false;
            }

            this.EnforcePausedReplayObjectSnapshots();
            this.EnsureCameraTarget();
            Player target = this.GetCameraTargetPlayer();
            if (target == null || target.PlayerBody == null)
            {
                this.RestoreFirstPersonTargetRenderers();
                this.ApplyDefaultFieldOfView(spectatorCamera);
                return false;
            }

            if (this.cameraMode == ReplayPlaybackCameraMode.FirstPerson)
            {
                if (target.PlayerCamera == null)
                {
                    this.RestoreFirstPersonTargetRenderers();
                    this.ApplyDefaultFieldOfView(spectatorCamera);
                    return false;
                }

                this.ApplyFirstPersonFieldOfView(spectatorCamera);
                this.HideFirstPersonTargetRenderers(target);
                this.ApplyFirstPersonPovCamera(spectatorCamera, target, deltaTime);
                return true;
            }

            this.RestoreFirstPersonTargetRenderers();
            this.ApplyDefaultFieldOfView(spectatorCamera);
            Vector3 forward = target.PlayerBody.transform.forward;
            Vector3 position = target.PlayerBody.transform.position - forward * this.GetThirdPersonCameraDistance() + Vector3.up * 2.35f;
            Vector3 lookTarget = target.PlayerBody.transform.position + Vector3.up * 1.25f + forward * 1.5f;
            Quaternion rotation = Quaternion.LookRotation(lookTarget - position);
            if (ReplayPlaybackRuntime.IsPaused)
            {
                spectatorCamera.transform.position = position;
                spectatorCamera.transform.rotation = rotation;
                return true;
            }

            float lerp = Mathf.Clamp01(deltaTime * 18f);
            spectatorCamera.transform.position = Vector3.Lerp(spectatorCamera.transform.position, position, lerp);
            spectatorCamera.transform.rotation = Quaternion.Slerp(spectatorCamera.transform.rotation, rotation, lerp);
            return true;
        }

        public bool SeekToTick(int targetTick)
        {
            if (!this.IsPlaying || this.replayPlayer == null || this.currentEventMap == null || this.currentEventMap.Count == 0)
            {
                return false;
            }

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return false;
            }

            bool wasPaused = ReplayPlaybackRuntime.IsPaused;
            float speed = ReplayPlaybackRuntime.PlaybackSpeed;
            int minTick = this.currentEventMap.Keys[0];
            int maxTick = this.currentEventMap.Keys[this.currentEventMap.Keys.Count - 1];
            targetTick = Mathf.Clamp(targetTick, minTick, maxTick);

            this.RestoreFirstPersonTargetRenderers();
            if (this.replayPlayer.IsReplaying)
            {
                this.replayPlayer.Server_StopReplay();
            }

            this.PrepareSceneForReplay();
            this.replayPlayer.Server_StartReplay(this.CloneEventMap(), this.currentTickRate, targetTick);
            this.IsPlaying = this.replayPlayer.IsReplaying;
            if (!this.IsPlaying)
            {
                return false;
            }

            ReplayPlaybackRuntime.Attach(this.replayPlayer);
            ReplayPlaybackRuntime.SetPlaybackSpeed(speed);
            ReplayPlaybackRuntime.SetPaused(wasPaused);
            ReplayPlaybackRuntime.RunImmediateThrough(this.replayPlayer, targetTick);
            this.ApplyLatestTransformsThrough(targetTick);
            ReplayPlaybackRuntime.SetPaused(wasPaused);
            this.gameStatePlayback.ApplyThrough(this.CurrentTick);
            ReplayGameStatePlaybackService.EnsureReplayObjectsOnMinimap();
            this.ResetFirstPersonCameraSmoothing();
            if (this.cameraMode == ReplayPlaybackCameraMode.FirstPerson)
            {
                this.HideFirstPersonTargetRenderers(this.GetCameraTargetPlayer());
            }

            return true;
        }

        public bool TryStepRelativeTicks(int deltaTicks)
        {
            if (!this.IsPlaying || this.replayPlayer == null || this.currentEventMap == null || this.currentEventMap.Count == 0)
            {
                return false;
            }

            if (deltaTicks != 1 && deltaTicks != -1)
            {
                return false;
            }

            int minTick = this.currentEventMap.Keys[0];
            int maxTick = this.currentEventMap.Keys[this.currentEventMap.Keys.Count - 1];
            int currentTick = this.CurrentTick;
            int targetTick = Mathf.Clamp(currentTick + deltaTicks, minTick, maxTick);
            if (targetTick == currentTick)
            {
                return true;
            }

            if (deltaTicks < 0 && this.HasLifecycleEventsBetween(targetTick, currentTick))
            {
                return false;
            }

            ReplayPlaybackRuntime.SetPaused(true);
            if (deltaTicks > 0)
            {
                ReplayPlaybackRuntime.RunImmediateThrough(this.replayPlayer, targetTick);
            }

            this.ApplyLatestTransformsThrough(targetTick);
            ReplayPlaybackRuntime.SetVisibleTick(this.replayPlayer, targetTick);
            this.gameStatePlayback.ApplyThrough(this.CurrentTick);
            ReplayGameStatePlaybackService.EnsureReplayObjectsOnMinimap();
            this.ResetFirstPersonCameraSmoothing();
            if (this.cameraMode == ReplayPlaybackCameraMode.FirstPerson)
            {
                this.HideFirstPersonTargetRenderers(this.GetCameraTargetPlayer());
            }

            return true;
        }

        public void Close()
        {
            this.gameStatePlayback.Close();
            if (this.replayPlayer != null && this.replayPlayer.IsReplaying && NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
            {
                this.replayPlayer.Server_StopReplay();
            }

            ReplayPlaybackRuntime.Detach(this.replayPlayer);
            this.IsPlaying = false;
            this.currentSession = null;
            this.currentFilePath = null;
            this.currentEventMap = null;
            this.pausedBodySnapshots.Clear();
            this.pausedStickSnapshots.Clear();
            this.pausedPuckSnapshots.Clear();
            this.cameraTargetTimeline.Clear();
            this.currentTickRate = 30;
            this.replayPlayer = null;
            this.replaySpectatorCameraAdjusted = false;
            this.cameraMode = ReplayPlaybackCameraMode.Free;
            this.cameraTargetClientId = null;
            this.ResetFirstPersonCameraSmoothing();
            this.RestoreFirstPersonTargetRenderers();
            this.RestoreLocalSpectatorFieldOfView();
        }

        private void EnsureCameraTarget()
        {
            if (this.cameraMode == ReplayPlaybackCameraMode.Free)
            {
                return;
            }

            int currentTick = this.CurrentTick;
            if (this.cameraTargetClientId.HasValue && this.IsAvailableCameraTarget(this.cameraTargetClientId.Value, currentTick))
            {
                return;
            }

            ulong activeClientId;
            if (this.TryGetFirstAvailableCameraTarget(currentTick, out activeClientId))
            {
                if (this.cameraMode == ReplayPlaybackCameraMode.FirstPerson && this.cameraTargetClientId != activeClientId)
                {
                    this.RestoreFirstPersonTargetRenderers();
                }

                this.cameraTargetClientId = activeClientId;
                this.ResetFirstPersonCameraSmoothing();
                return;
            }

            this.cameraTargetClientId = null;
            this.cameraMode = ReplayPlaybackCameraMode.Free;
            this.ResetFirstPersonCameraSmoothing();
            this.RestoreFirstPersonTargetRenderers();
            this.RestoreLocalSpectatorFieldOfView();
        }

        private Player GetCameraTargetPlayer()
        {
            PlayerManager playerManager = MonoBehaviourSingleton<PlayerManager>.Instance;
            if (playerManager == null)
            {
                return null;
            }

            if (this.cameraTargetClientId.HasValue)
            {
                return playerManager.GetReplayPlayerByClientId(this.cameraTargetClientId.Value);
            }

            foreach (Player player in playerManager.GetReplayPlayers())
            {
                if (player != null)
                {
                    return player;
                }
            }

            return null;
        }

        private void BuildCameraTargetRoster(ReplaySessionData session)
        {
            this.cameraTargetTimeline.Clear();
            if (session == null || session.Events == null)
            {
                return;
            }

            for (int i = 0; i < session.Events.Count; i++)
            {
                ReplayEventDto replayEvent = session.Events[i];
                if (replayEvent == null)
                {
                    continue;
                }

                this.TrackCameraTargetPayload(replayEvent.Type, replayEvent.Tick, i, replayEvent.Payload);
            }

            this.cameraTargetTimeline.Sort(delegate(CameraTargetTimelineEvent left, CameraTargetTimelineEvent right)
            {
                int tickCompare = left.Tick.CompareTo(right.Tick);
                return tickCompare != 0 ? tickCompare : left.Sequence.CompareTo(right.Sequence);
            });
        }

        private void TrackCameraTargetPayload(string eventName, int tick, int sequence, object payload)
        {
            InitialSnapshotPayload initialSnapshot = payload as InitialSnapshotPayload;
            if (initialSnapshot != null)
            {
                this.TrackCameraTargetPlayers(initialSnapshot.Players, tick, sequence, true);
                if (initialSnapshot.PlayerBodies != null)
                {
                    for (int i = 0; i < initialSnapshot.PlayerBodies.Count; i++)
                    {
                        BodyLifecyclePayload body = initialSnapshot.PlayerBodies[i];
                        if (body != null)
                        {
                            this.AddCameraTargetTimelineEvent(tick, sequence, body.Player, null);
                        }
                    }
                }

                return;
            }

            PlayerSnapshotPayload playerSnapshot = payload as PlayerSnapshotPayload;
            if (playerSnapshot != null)
            {
                this.AddCameraTargetTimelineEvent(tick, sequence, playerSnapshot, null);
                return;
            }

            PlayerLifecyclePayload playerLifecycle = payload as PlayerLifecyclePayload;
            if (playerLifecycle != null)
            {
                bool? isAvailable = null;
                if (string.Equals(eventName, "PlayerSpawned", StringComparison.Ordinal))
                {
                    isAvailable = true;
                }
                else if (string.Equals(eventName, "PlayerDespawned", StringComparison.Ordinal))
                {
                    isAvailable = false;
                }

                this.AddCameraTargetTimelineEvent(tick, sequence, playerLifecycle.Player, isAvailable);
                return;
            }

            BodyLifecyclePayload bodyLifecycle = payload as BodyLifecyclePayload;
            if (bodyLifecycle != null)
            {
                this.AddCameraTargetTimelineEvent(tick, sequence, bodyLifecycle.Player, null);
                return;
            }

            ScoreboardSnapshotPayload scoreboard = payload as ScoreboardSnapshotPayload;
            if (scoreboard != null)
            {
                this.TrackCameraTargetPlayers(scoreboard.Players, tick, sequence, null);
            }
        }

        private void TrackCameraTargetPlayers(List<PlayerSnapshotPayload> players, int tick, int sequence, bool? isAvailable)
        {
            if (players == null)
            {
                return;
            }

            for (int i = 0; i < players.Count; i++)
            {
                this.AddCameraTargetTimelineEvent(tick, sequence, players[i], isAvailable);
            }
        }

        private void AddCameraTargetTimelineEvent(int tick, int sequence, PlayerSnapshotPayload player, bool? isAvailable)
        {
            if (player == null)
            {
                return;
            }

            this.cameraTargetTimeline.Add(new CameraTargetTimelineEvent(tick, sequence, player.OwnerClientId, isAvailable, player));
        }

        private Dictionary<ulong, PlayerSnapshotPayload> GetAvailableCameraTargetSnapshots(int currentTick)
        {
            Dictionary<ulong, PlayerSnapshotPayload> availableTargets = new Dictionary<ulong, PlayerSnapshotPayload>();
            for (int i = 0; i < this.cameraTargetTimeline.Count; i++)
            {
                CameraTargetTimelineEvent timelineEvent = this.cameraTargetTimeline[i];
                if (timelineEvent.Tick > currentTick)
                {
                    break;
                }

                if (timelineEvent.IsAvailable.HasValue)
                {
                    if (timelineEvent.IsAvailable.Value)
                    {
                        availableTargets[timelineEvent.OwnerClientId] = timelineEvent.Player;
                    }
                    else
                    {
                        availableTargets.Remove(timelineEvent.OwnerClientId);
                    }

                    continue;
                }

                if (availableTargets.ContainsKey(timelineEvent.OwnerClientId))
                {
                    availableTargets[timelineEvent.OwnerClientId] = timelineEvent.Player;
                }
            }

            return availableTargets;
        }

        private bool IsAvailableCameraTarget(ulong ownerClientId, int currentTick)
        {
            return this.GetAvailableCameraTargetSnapshots(currentTick).ContainsKey(ownerClientId);
        }

        private bool TryGetFirstAvailableCameraTarget(int currentTick, out ulong ownerClientId)
        {
            ownerClientId = 0UL;
            Dictionary<ulong, PlayerSnapshotPayload> availableTargets = this.GetAvailableCameraTargetSnapshots(currentTick);
            List<ReplayPlaybackPlayerTarget> targets = new List<ReplayPlaybackPlayerTarget>(availableTargets.Count);
            foreach (PlayerSnapshotPayload playerSnapshot in availableTargets.Values)
            {
                targets.Add(new ReplayPlaybackPlayerTarget(playerSnapshot.OwnerClientId, FormatCameraTargetLabel(playerSnapshot)));
            }

            targets.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));
            if (targets.Count == 0)
            {
                return false;
            }

            ownerClientId = targets[0].OwnerClientId;
            return true;
        }

        private static ulong ToOriginalReplayOwnerClientId(ulong ownerClientId)
        {
            return ownerClientId >= ReplayOwnerClientIdOffset ? ownerClientId - ReplayOwnerClientIdOffset : ownerClientId;
        }

        private static string FormatCameraTargetLabel(PlayerSnapshotPayload player)
        {
            string username = player != null ? player.Username : null;
            string label = string.IsNullOrEmpty(username) ? "Replay Player " + (player != null ? player.OwnerClientId.ToString() : "Unknown") : username;
            if (player != null && player.Number > 0)
            {
                label = "#" + player.Number + " " + label;
            }

            return label;
        }

        private static string FormatCameraTargetLabel(Player player, ulong originalOwnerClientId)
        {
            string username = player.Username.Value.ToString();
            string label = string.IsNullOrEmpty(username) ? "Replay Player " + originalOwnerClientId : username;
            if (player.Number.Value > 0)
            {
                label = "#" + player.Number.Value + " " + label;
            }

            return label;
        }

        private float GetThirdPersonCameraDistance()
        {
            float distance = this.settings != null ? this.settings.PlaybackThirdPersonCameraDistance : 4.25f;
            return Mathf.Clamp(distance, 1.5f, 12f);
        }

        private void ApplyFirstPersonPovCamera(SpectatorCamera spectatorCamera, Player target, float deltaTime)
        {
            Vector3 cameraOrigin = target.PlayerCamera.transform.position;
            Quaternion targetRotation = target.PlayerCamera.transform.rotation;
            if (!this.ShouldSmoothFirstPersonCamera(target, deltaTime))
            {
                spectatorCamera.transform.rotation = targetRotation;
                spectatorCamera.transform.position = cameraOrigin + targetRotation * Vector3.forward * 0.06f;
                this.firstPersonCameraSmoothedRotation = targetRotation;
                this.firstPersonCameraSmoothingInitialized = true;
                this.firstPersonCameraSmoothingTargetClientId = target.OwnerClientId;
                return;
            }

            if (!this.firstPersonCameraSmoothingInitialized ||
                !this.firstPersonCameraSmoothingTargetClientId.HasValue ||
                this.firstPersonCameraSmoothingTargetClientId.Value != target.OwnerClientId)
            {
                this.firstPersonCameraSmoothedRotation = targetRotation;
                this.firstPersonCameraSmoothingInitialized = true;
                this.firstPersonCameraSmoothingTargetClientId = target.OwnerClientId;
            }

            float speed = this.settings != null ? this.settings.FirstPersonCameraSmoothingSpeed : 18f;
            float lerp = 1f - Mathf.Exp(-Mathf.Clamp(speed, 1f, 60f) * Mathf.Max(0f, deltaTime));
            this.firstPersonCameraSmoothedRotation = Quaternion.Slerp(this.firstPersonCameraSmoothedRotation, targetRotation, lerp);
            spectatorCamera.transform.rotation = this.firstPersonCameraSmoothedRotation;
            spectatorCamera.transform.position = cameraOrigin + this.firstPersonCameraSmoothedRotation * Vector3.forward * 0.06f;
        }

        private bool ShouldSmoothFirstPersonCamera(Player target, float deltaTime)
        {
            return this.settings != null &&
                this.settings.EnableFirstPersonCameraSmoothing &&
                !ReplayPlaybackRuntime.IsPaused &&
                deltaTime > 0f &&
                target != null &&
                target.PlayerCamera != null;
        }

        private void ResetFirstPersonCameraSmoothing()
        {
            this.firstPersonCameraSmoothingInitialized = false;
            this.firstPersonCameraSmoothingTargetClientId = null;
            this.firstPersonCameraSmoothedRotation = Quaternion.identity;
        }

        private void ApplyFirstPersonFieldOfView(SpectatorCamera spectatorCamera)
        {
            if (spectatorCamera == null || spectatorCamera.UnityCamera == null)
            {
                return;
            }

            float fov = this.settings != null ? this.settings.PlaybackFirstPersonFov : SettingsManager.Fov;
            spectatorCamera.UnityCamera.fieldOfView = Mathf.Clamp(fov, 60f, 120f);
        }

        private void ApplyDefaultFieldOfView(SpectatorCamera spectatorCamera)
        {
            if (spectatorCamera != null && spectatorCamera.UnityCamera != null)
            {
                spectatorCamera.UnityCamera.fieldOfView = SettingsManager.Fov;
            }
        }

        private void HideFirstPersonTargetRenderers(Player target)
        {
            if (target == null || target.PlayerBody == null)
            {
                return;
            }

            if (this.hiddenFirstPersonTargetClientId.HasValue &&
                this.hiddenFirstPersonTargetClientId.Value == target.OwnerClientId &&
                this.hiddenFirstPersonTargetBody == target.PlayerBody &&
                this.AreFirstPersonRenderersStillHidden())
            {
                return;
            }

            this.RestoreFirstPersonTargetRenderers();
            Renderer[] renderers = target.PlayerBody.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || this.ShouldKeepRendererVisibleInFirstPerson(target.PlayerBody, renderer))
                {
                    continue;
                }

                this.hiddenFirstPersonRenderers.Add(new RendererVisibilityState(renderer, renderer.enabled));
                renderer.enabled = false;
            }

            this.hiddenFirstPersonTargetClientId = target.OwnerClientId;
            this.hiddenFirstPersonTargetBody = target.PlayerBody;
        }

        private bool ShouldKeepRendererVisibleInFirstPerson(PlayerBody playerBody, Renderer renderer)
        {
            if (playerBody == null || renderer == null || playerBody.Player == null || playerBody.Player.Role != PlayerRole.Goalie)
            {
                return false;
            }

            PlayerMesh mesh = playerBody.PlayerMesh;
            if (mesh == null)
            {
                return false;
            }

            return IsRendererUnder(mesh.PlayerLegPadLeft, renderer) || IsRendererUnder(mesh.PlayerLegPadRight, renderer);
        }

        private static bool IsRendererUnder(Component parent, Renderer renderer)
        {
            return parent != null && renderer != null && renderer.transform.IsChildOf(parent.transform);
        }

        private void RestoreFirstPersonTargetRenderers()
        {
            if (!this.hiddenFirstPersonTargetClientId.HasValue && this.hiddenFirstPersonRenderers.Count == 0)
            {
                return;
            }

            for (int i = 0; i < this.hiddenFirstPersonRenderers.Count; i++)
            {
                RendererVisibilityState state = this.hiddenFirstPersonRenderers[i];
                if (state.Renderer != null)
                {
                    state.Renderer.enabled = state.WasEnabled;
                }
            }

            this.hiddenFirstPersonRenderers.Clear();
            this.hiddenFirstPersonTargetClientId = null;
            this.hiddenFirstPersonTargetBody = null;
        }

        private bool AreFirstPersonRenderersStillHidden()
        {
            if (this.hiddenFirstPersonRenderers.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < this.hiddenFirstPersonRenderers.Count; i++)
            {
                Renderer renderer = this.hiddenFirstPersonRenderers[i].Renderer;
                if (renderer != null && renderer.enabled)
                {
                    return false;
                }
            }

            return true;
        }

        private struct RendererVisibilityState
        {
            public RendererVisibilityState(Renderer renderer, bool wasEnabled)
            {
                this.Renderer = renderer;
                this.WasEnabled = wasEnabled;
            }

            public Renderer Renderer;
            public bool WasEnabled;
        }

        private sealed class CameraTargetTimelineEvent
        {
            public CameraTargetTimelineEvent(int tick, int sequence, ulong ownerClientId, bool? isAvailable, PlayerSnapshotPayload player)
            {
                this.Tick = tick;
                this.Sequence = sequence;
                this.OwnerClientId = ownerClientId;
                this.IsAvailable = isAvailable;
                this.Player = player;
            }

            public int Tick;
            public int Sequence;
            public ulong OwnerClientId;
            public bool? IsAvailable;
            public PlayerSnapshotPayload Player;
        }

        private void RestoreLocalSpectatorFieldOfView()
        {
            PlayerManager playerManager = MonoBehaviourSingleton<PlayerManager>.Instance;
            Player localPlayer = playerManager != null ? playerManager.GetLocalPlayer() : null;
            SpectatorCamera spectatorCamera = localPlayer != null ? localPlayer.SpectatorCamera : null;
            if (spectatorCamera != null && spectatorCamera.UnityCamera != null)
            {
                spectatorCamera.UnityCamera.fieldOfView = SettingsManager.Fov;
            }
        }

        public void EnforceSpectatorMode()
        {
            try
            {
                HideReplaySelectionViews();

                PlayerManager playerManager = MonoBehaviourSingleton<PlayerManager>.Instance;
                Player localPlayer = playerManager != null ? playerManager.GetLocalPlayer() : null;
                if (localPlayer == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                {
                    return;
                }

                if (localPlayer.IsCharacterSpawned)
                {
                    localPlayer.Server_DespawnCharacter();
                }

                localPlayer.Server_SetGameState(PlayerPhase.Spectate, PlayerTeam.Spectator, PlayerRole.None, null);
                this.EnsureReplaySpectatorCameraRaised(localPlayer);
            }
            catch (Exception exception)
            {
                ReplayModLog.Warning("Failed to enforce replay spectator mode: " + exception.Message);
            }
        }

        private void EnsureReplaySpectatorCameraRaised(Player localPlayer)
        {
            if (this.replaySpectatorCameraAdjusted || localPlayer == null || !localPlayer.IsSpectatorCameraSpawned || localPlayer.SpectatorCamera == null)
            {
                return;
            }

            if (localPlayer.SpectatorCamera.transform.position.y < 2f)
            {
                localPlayer.Server_DespawnSpectatorCamera();
                localPlayer.Server_SpawnSpectatorCamera(ReplaySpectatorSpawnPosition, ReplaySpectatorSpawnRotation);
            }

            this.replaySpectatorCameraAdjusted = true;
        }

        private static void HideReplaySelectionViews()
        {
            UIManager uiManager = MonoBehaviourSingleton<UIManager>.Instance;
            if (uiManager == null)
            {
                return;
            }

            if (uiManager.TeamSelect != null && uiManager.TeamSelect.IsVisible)
            {
                uiManager.TeamSelect.Hide();
            }

            if (uiManager.PositionSelect != null && uiManager.PositionSelect.IsVisible)
            {
                uiManager.PositionSelect.Hide();
            }
        }

        private void PrepareSceneForReplay()
        {
            try
            {
                this.EnforceSpectatorMode();

                if (MonoBehaviourSingleton<PuckManager>.Instance != null)
                {
                    MonoBehaviourSingleton<PuckManager>.Instance.Server_DespawnPucks(false);
                }

                if (NetworkBehaviourSingleton<GameManager>.Instance != null)
                {
                    NetworkBehaviourSingleton<GameManager>.Instance.Server_StopTicking();
                }

                if (MonoBehaviourSingleton<ReplayManager>.Instance != null)
                {
                    MonoBehaviourSingleton<ReplayManager>.Instance.Server_StopRecording();
                }
            }
            catch (Exception exception)
            {
                ReplayModLog.Warning("Native replay scene preparation failed: " + exception.Message);
            }
        }

        private SortedList<int, List<ValueTuple<string, object>>> CloneEventMap()
        {
            SortedList<int, List<ValueTuple<string, object>>> clone = new SortedList<int, List<ValueTuple<string, object>>>();
            if (this.currentEventMap == null)
            {
                return clone;
            }

            foreach (KeyValuePair<int, List<ValueTuple<string, object>>> entry in this.currentEventMap)
            {
                clone.Add(entry.Key, new List<ValueTuple<string, object>>(entry.Value));
            }

            return clone;
        }

        private void ApplyLatestTransformsThrough(int targetTick)
        {
            if (this.currentEventMap == null || this.currentEventMap.Count == 0)
            {
                return;
            }

            Dictionary<ulong, ReplayPlayerBodyMove> bodyMoves = new Dictionary<ulong, ReplayPlayerBodyMove>();
            Dictionary<ulong, ReplayStickMove> stickMoves = new Dictionary<ulong, ReplayStickMove>();
            Dictionary<ulong, ReplayPuckMove> puckMoves = new Dictionary<ulong, ReplayPuckMove>();
            Dictionary<ulong, ReplayPlayerInput> playerInputs = new Dictionary<ulong, ReplayPlayerInput>();

            foreach (KeyValuePair<int, List<ValueTuple<string, object>>> entry in this.currentEventMap)
            {
                if (entry.Key > targetTick)
                {
                    break;
                }

                foreach (ValueTuple<string, object> replayEvent in entry.Value)
                {
                    if (replayEvent.Item1 == "PlayerBodyMove" && replayEvent.Item2 is ReplayPlayerBodyMove)
                    {
                        ReplayPlayerBodyMove move = (ReplayPlayerBodyMove)replayEvent.Item2;
                        bodyMoves[move.OwnerClientId] = move;
                    }
                    else if (replayEvent.Item1 == "StickMove" && replayEvent.Item2 is ReplayStickMove)
                    {
                        ReplayStickMove move = (ReplayStickMove)replayEvent.Item2;
                        stickMoves[move.OwnerClientId] = move;
                    }
                    else if (replayEvent.Item1 == "PuckMove" && replayEvent.Item2 is ReplayPuckMove)
                    {
                        ReplayPuckMove move = (ReplayPuckMove)replayEvent.Item2;
                        puckMoves[move.NetworkObjectId] = move;
                    }
                    else if (replayEvent.Item1 == "PlayerInput" && replayEvent.Item2 is ReplayPlayerInput)
                    {
                        ReplayPlayerInput input = (ReplayPlayerInput)replayEvent.Item2;
                        playerInputs[input.OwnerClientId] = input;
                    }
                }
            }

            this.ApplyPlayerInputs(playerInputs);
            this.ApplyBodyMoves(bodyMoves);
            this.ApplyStickMoves(stickMoves);
            this.ApplyPuckMoves(puckMoves);
            this.StorePausedReplayObjectSnapshots(bodyMoves, stickMoves, puckMoves);
            this.ForceReplayPlayerMeshPoses();
        }

        private void ApplyPlayerInputs(Dictionary<ulong, ReplayPlayerInput> inputs)
        {
            PlayerManager playerManager = MonoBehaviourSingleton<PlayerManager>.Instance;
            if (playerManager == null)
            {
                return;
            }

            foreach (ReplayPlayerInput input in inputs.Values)
            {
                Player player = playerManager.GetReplayPlayerByClientId(input.OwnerClientId);
                if (player == null || player.PlayerInput == null)
                {
                    continue;
                }

                ApplyPlayerInput(player.PlayerInput, input);
            }
        }

        internal static void ApplyPlayerInput(PlayerInput playerInput, ReplayPlayerInput input)
        {
            if (playerInput == null)
            {
                return;
            }

            playerInput.LookAngleInput.ClientValue = input.LookAngleInput;
            playerInput.LookAngleInput.ServerValue = input.LookAngleInput;
            playerInput.BladeAngleInput.ClientValue = input.BladeAngleInput;
            playerInput.BladeAngleInput.ServerValue = input.BladeAngleInput;
            playerInput.TrackInput.ClientValue = input.TrackInput;
            playerInput.TrackInput.ServerValue = input.TrackInput;
            playerInput.LookInput.ClientValue = input.LookInput;
            playerInput.LookInput.ServerValue = input.LookInput;
        }

        private void ApplyBodyMoves(Dictionary<ulong, ReplayPlayerBodyMove> moves)
        {
            PlayerManager playerManager = MonoBehaviourSingleton<PlayerManager>.Instance;
            if (playerManager == null)
            {
                return;
            }

            foreach (ReplayPlayerBodyMove move in moves.Values)
            {
                Player player = playerManager.GetReplayPlayerByClientId(move.OwnerClientId);
                if (player == null || player.PlayerBody == null)
                {
                    continue;
                }

                player.PlayerBody.transform.DOKill(false);
                ApplyTransformAndRigidbody(player.PlayerBody.transform, player.PlayerBody.Rigidbody, move.Position, move.Rotation);
                player.PlayerBody.Stamina.Value = move.Stamina;
                player.PlayerBody.Speed.Value = move.Speed;
                player.PlayerBody.IsSprinting.Value = move.IsSprinting;
                player.PlayerBody.IsSliding.Value = move.IsSliding;
                player.PlayerBody.IsStopping.Value = move.IsStopping;
                player.PlayerBody.IsExtendedLeft.Value = move.IsExtendedLeft;
                player.PlayerBody.IsExtendedRight.Value = move.IsExtendedRight;
            }
        }

        private void ApplyStickMoves(Dictionary<ulong, ReplayStickMove> moves)
        {
            PlayerManager playerManager = MonoBehaviourSingleton<PlayerManager>.Instance;
            if (playerManager == null)
            {
                return;
            }

            foreach (ReplayStickMove move in moves.Values)
            {
                Player player = playerManager.GetReplayPlayerByClientId(move.OwnerClientId);
                if (player == null || player.Stick == null)
                {
                    continue;
                }

                player.Stick.transform.DOKill(false);
                ApplyTransformAndRigidbody(player.Stick.transform, player.Stick.Rigidbody, move.Position, move.Rotation);
            }
        }

        private void ApplyPuckMoves(Dictionary<ulong, ReplayPuckMove> moves)
        {
            PuckManager puckManager = MonoBehaviourSingleton<PuckManager>.Instance;
            if (puckManager == null || ReplayPuckNetworkObjectIdMapField == null || this.replayPlayer == null)
            {
                return;
            }

            Dictionary<ulong, ulong> puckIdMap = ReplayPuckNetworkObjectIdMapField.GetValue(this.replayPlayer) as Dictionary<ulong, ulong>;
            if (puckIdMap == null)
            {
                return;
            }

            foreach (ReplayPuckMove move in moves.Values)
            {
                ulong replayNetworkObjectId;
                if (!puckIdMap.TryGetValue(move.NetworkObjectId, out replayNetworkObjectId))
                {
                    continue;
                }

                Puck puck = puckManager.GetReplayPuckByNetworkObjectId(replayNetworkObjectId);
                if (puck == null)
                {
                    continue;
                }

                puck.transform.DOKill(false);
                ApplyTransformAndRigidbody(puck.transform, puck.Rigidbody, move.Position, move.Rotation);
            }
        }

        private void ApplySlowMotionInterpolation()
        {
            if (this.settings == null ||
                !this.settings.EnableSlowMotionInterpolation ||
                !this.IsPlaying ||
                ReplayPlaybackRuntime.IsPaused ||
                this.replayPlayer == null ||
                this.currentEventMap == null ||
                ReplayPlaybackRuntime.PlaybackSpeed >= 0.999f)
            {
                return;
            }

            float fraction = ReplayPlaybackRuntime.GetInterpolationFraction(this.replayPlayer);
            if (fraction <= 0.001f)
            {
                return;
            }

            int currentTick = this.CurrentTick;
            int nextTick = currentTick + 1;
            if (this.HasLifecycleEventsBetween(currentTick, nextTick))
            {
                this.LogSlowMotionInterpolationDebug(currentTick, nextTick, fraction, "skipped lifecycle event", 0, 0, 0, 0, 0, 0, 0, 0, 0);
                return;
            }

            Dictionary<ulong, ReplayPlayerBodyMove> currentBodyMoves;
            Dictionary<ulong, ReplayStickMove> currentStickMoves;
            Dictionary<ulong, ReplayPuckMove> currentPuckMoves;
            Dictionary<ulong, ReplayPlayerBodyMove> nextBodyMoves;
            Dictionary<ulong, ReplayStickMove> nextStickMoves;
            Dictionary<ulong, ReplayPuckMove> nextPuckMoves;
            this.GetTransformMovesAtTick(currentTick, out currentBodyMoves, out currentStickMoves, out currentPuckMoves);
            this.GetTransformMovesAtTick(nextTick, out nextBodyMoves, out nextStickMoves, out nextPuckMoves);

            int matchedBodies = CountMatchingKeys(currentBodyMoves, nextBodyMoves);
            int matchedSticks = CountMatchingKeys(currentStickMoves, nextStickMoves);
            int matchedPucks = CountMatchingKeys(currentPuckMoves, nextPuckMoves);
            int appliedBodies = this.ApplyInterpolatedBodyMoves(currentBodyMoves, nextBodyMoves, fraction);
            int appliedSticks = this.ApplyInterpolatedStickMoves(currentStickMoves, nextStickMoves, fraction);
            int appliedPucks = this.ApplyInterpolatedPuckMoves(currentPuckMoves, nextPuckMoves, fraction);
            this.ForceReplayPlayerMeshPoses();
            this.LogSlowMotionInterpolationDebug(
                currentTick,
                nextTick,
                fraction,
                "applied",
                currentBodyMoves.Count,
                matchedBodies,
                appliedBodies,
                currentStickMoves.Count,
                matchedSticks,
                appliedSticks,
                currentPuckMoves.Count,
                matchedPucks,
                appliedPucks);
        }

        private void GetTransformMovesAtTick(
            int tick,
            out Dictionary<ulong, ReplayPlayerBodyMove> bodyMoves,
            out Dictionary<ulong, ReplayStickMove> stickMoves,
            out Dictionary<ulong, ReplayPuckMove> puckMoves)
        {
            bodyMoves = new Dictionary<ulong, ReplayPlayerBodyMove>();
            stickMoves = new Dictionary<ulong, ReplayStickMove>();
            puckMoves = new Dictionary<ulong, ReplayPuckMove>();
            if (this.currentEventMap == null)
            {
                return;
            }

            List<ValueTuple<string, object>> events;
            if (!this.currentEventMap.TryGetValue(tick, out events) || events == null)
            {
                return;
            }

            for (int i = 0; i < events.Count; i++)
            {
                ValueTuple<string, object> replayEvent = events[i];
                if (replayEvent.Item1 == "PlayerBodyMove" && replayEvent.Item2 is ReplayPlayerBodyMove)
                {
                    ReplayPlayerBodyMove move = (ReplayPlayerBodyMove)replayEvent.Item2;
                    bodyMoves[move.OwnerClientId] = move;
                }
                else if (replayEvent.Item1 == "StickMove" && replayEvent.Item2 is ReplayStickMove)
                {
                    ReplayStickMove move = (ReplayStickMove)replayEvent.Item2;
                    stickMoves[move.OwnerClientId] = move;
                }
                else if (replayEvent.Item1 == "PuckMove" && replayEvent.Item2 is ReplayPuckMove)
                {
                    ReplayPuckMove move = (ReplayPuckMove)replayEvent.Item2;
                    puckMoves[move.NetworkObjectId] = move;
                }
            }
        }

        private int ApplyInterpolatedBodyMoves(
            Dictionary<ulong, ReplayPlayerBodyMove> currentMoves,
            Dictionary<ulong, ReplayPlayerBodyMove> nextMoves,
            float fraction)
        {
            PlayerManager playerManager = MonoBehaviourSingleton<PlayerManager>.Instance;
            if (playerManager == null || currentMoves == null || nextMoves == null)
            {
                return 0;
            }

            int applied = 0;
            foreach (KeyValuePair<ulong, ReplayPlayerBodyMove> entry in currentMoves)
            {
                ReplayPlayerBodyMove nextMove;
                if (!nextMoves.TryGetValue(entry.Key, out nextMove))
                {
                    continue;
                }

                Player player = playerManager.GetReplayPlayerByClientId(entry.Key);
                if (player == null || player.PlayerBody == null)
                {
                    continue;
                }

                ReplayPlayerBodyMove currentMove = entry.Value;
                Vector3 position = Vector3.Lerp(currentMove.Position, nextMove.Position, fraction);
                Quaternion rotation = Quaternion.Slerp(currentMove.Rotation, nextMove.Rotation, fraction);
                player.PlayerBody.transform.DOKill(false);
                ApplyTransformAndRigidbody(player.PlayerBody.transform, player.PlayerBody.Rigidbody, position, rotation);
                player.PlayerBody.Stamina.Value = Mathf.Lerp(currentMove.Stamina, nextMove.Stamina, fraction);
                player.PlayerBody.Speed.Value = Mathf.Lerp(currentMove.Speed, nextMove.Speed, fraction);
                player.PlayerBody.IsSprinting.Value = fraction < 0.5f ? currentMove.IsSprinting : nextMove.IsSprinting;
                player.PlayerBody.IsSliding.Value = fraction < 0.5f ? currentMove.IsSliding : nextMove.IsSliding;
                player.PlayerBody.IsStopping.Value = fraction < 0.5f ? currentMove.IsStopping : nextMove.IsStopping;
                player.PlayerBody.IsExtendedLeft.Value = fraction < 0.5f ? currentMove.IsExtendedLeft : nextMove.IsExtendedLeft;
                player.PlayerBody.IsExtendedRight.Value = fraction < 0.5f ? currentMove.IsExtendedRight : nextMove.IsExtendedRight;
                applied++;
            }

            return applied;
        }

        private int ApplyInterpolatedStickMoves(
            Dictionary<ulong, ReplayStickMove> currentMoves,
            Dictionary<ulong, ReplayStickMove> nextMoves,
            float fraction)
        {
            PlayerManager playerManager = MonoBehaviourSingleton<PlayerManager>.Instance;
            if (playerManager == null || currentMoves == null || nextMoves == null)
            {
                return 0;
            }

            int applied = 0;
            foreach (KeyValuePair<ulong, ReplayStickMove> entry in currentMoves)
            {
                ReplayStickMove nextMove;
                if (!nextMoves.TryGetValue(entry.Key, out nextMove))
                {
                    continue;
                }

                Player player = playerManager.GetReplayPlayerByClientId(entry.Key);
                if (player == null || player.Stick == null)
                {
                    continue;
                }

                ReplayStickMove currentMove = entry.Value;
                Vector3 position = Vector3.Lerp(currentMove.Position, nextMove.Position, fraction);
                Quaternion rotation = Quaternion.Slerp(currentMove.Rotation, nextMove.Rotation, fraction);
                player.Stick.transform.DOKill(false);
                ApplyTransformAndRigidbody(player.Stick.transform, player.Stick.Rigidbody, position, rotation);
                applied++;
            }

            return applied;
        }

        private int ApplyInterpolatedPuckMoves(
            Dictionary<ulong, ReplayPuckMove> currentMoves,
            Dictionary<ulong, ReplayPuckMove> nextMoves,
            float fraction)
        {
            PuckManager puckManager = MonoBehaviourSingleton<PuckManager>.Instance;
            if (puckManager == null || ReplayPuckNetworkObjectIdMapField == null || this.replayPlayer == null || currentMoves == null || nextMoves == null)
            {
                return 0;
            }

            Dictionary<ulong, ulong> puckIdMap = ReplayPuckNetworkObjectIdMapField.GetValue(this.replayPlayer) as Dictionary<ulong, ulong>;
            if (puckIdMap == null)
            {
                return 0;
            }

            int applied = 0;
            foreach (KeyValuePair<ulong, ReplayPuckMove> entry in currentMoves)
            {
                ReplayPuckMove nextMove;
                if (!nextMoves.TryGetValue(entry.Key, out nextMove))
                {
                    continue;
                }

                ulong replayNetworkObjectId;
                if (!puckIdMap.TryGetValue(entry.Key, out replayNetworkObjectId))
                {
                    continue;
                }

                Puck puck = puckManager.GetReplayPuckByNetworkObjectId(replayNetworkObjectId);
                if (puck == null)
                {
                    continue;
                }

                ReplayPuckMove currentMove = entry.Value;
                Vector3 position = Vector3.Lerp(currentMove.Position, nextMove.Position, fraction);
                Quaternion rotation = Quaternion.Slerp(currentMove.Rotation, nextMove.Rotation, fraction);
                puck.transform.DOKill(false);
                ApplyTransformAndRigidbody(puck.transform, puck.Rigidbody, position, rotation);
                applied++;
            }

            return applied;
        }

        private void LogSlowMotionInterpolationDebug(
            int currentTick,
            int nextTick,
            float fraction,
            string reason,
            int currentBodies,
            int matchedBodies,
            int appliedBodies,
            int currentSticks,
            int matchedSticks,
            int appliedSticks,
            int currentPucks,
            int matchedPucks,
            int appliedPucks)
        {
            if (this.settings == null || !this.settings.EnableDebugProfiling)
            {
                return;
            }

            float realtime = Time.realtimeSinceStartup;
            if (realtime < this.nextSlowMotionInterpolationDebugRealtime)
            {
                return;
            }

            this.nextSlowMotionInterpolationDebugRealtime = realtime + 1f;
            ReplayModLog.Info(
                "[Slow Motion Interpolation] " +
                "reason=" + reason +
                ", tick=" + currentTick + "->" + nextTick +
                ", fraction=" + fraction.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) +
                ", speed=" + ReplayPlaybackRuntime.PlaybackSpeed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) +
                ", bodies=" + appliedBodies + "/" + matchedBodies + "/" + currentBodies +
                ", sticks=" + appliedSticks + "/" + matchedSticks + "/" + currentSticks +
                ", pucks=" + appliedPucks + "/" + matchedPucks + "/" + currentPucks);
        }

        private static int CountMatchingKeys<TValue>(Dictionary<ulong, TValue> currentMoves, Dictionary<ulong, TValue> nextMoves)
        {
            if (currentMoves == null || nextMoves == null)
            {
                return 0;
            }

            int count = 0;
            foreach (ulong key in currentMoves.Keys)
            {
                if (nextMoves.ContainsKey(key))
                {
                    count++;
                }
            }

            return count;
        }

        internal static void ApplyTransformAndRigidbody(Transform transform, Rigidbody rigidbody, Vector3 position, Quaternion rotation)
        {
            if (transform != null)
            {
                transform.position = position;
                transform.rotation = rotation;
            }

            if (rigidbody == null)
            {
                return;
            }

            rigidbody.position = position;
            rigidbody.rotation = rotation;
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }

        private void StorePausedReplayObjectSnapshots(
            Dictionary<ulong, ReplayPlayerBodyMove> bodyMoves,
            Dictionary<ulong, ReplayStickMove> stickMoves,
            Dictionary<ulong, ReplayPuckMove> puckMoves)
        {
            this.pausedBodySnapshots.Clear();
            foreach (KeyValuePair<ulong, ReplayPlayerBodyMove> entry in bodyMoves)
            {
                this.pausedBodySnapshots[entry.Key] = entry.Value;
            }

            this.pausedStickSnapshots.Clear();
            foreach (KeyValuePair<ulong, ReplayStickMove> entry in stickMoves)
            {
                this.pausedStickSnapshots[entry.Key] = entry.Value;
            }

            this.pausedPuckSnapshots.Clear();
            foreach (KeyValuePair<ulong, ReplayPuckMove> entry in puckMoves)
            {
                this.pausedPuckSnapshots[entry.Key] = entry.Value;
            }
        }

        private void EnforcePausedReplayObjectSnapshots()
        {
            if (!this.IsPlaying || !ReplayPlaybackRuntime.IsPaused)
            {
                return;
            }

            PlayerManager playerManager = MonoBehaviourSingleton<PlayerManager>.Instance;
            if (playerManager != null)
            {
                foreach (ReplayPlayerBodyMove move in this.pausedBodySnapshots.Values)
                {
                    Player player = playerManager.GetReplayPlayerByClientId(move.OwnerClientId);
                    if (player != null && player.PlayerBody != null)
                    {
                        ApplyTransformAndRigidbody(player.PlayerBody.transform, player.PlayerBody.Rigidbody, move.Position, move.Rotation);
                    }
                }

                foreach (ReplayStickMove move in this.pausedStickSnapshots.Values)
                {
                    Player player = playerManager.GetReplayPlayerByClientId(move.OwnerClientId);
                    if (player != null && player.Stick != null)
                    {
                        ApplyTransformAndRigidbody(player.Stick.transform, player.Stick.Rigidbody, move.Position, move.Rotation);
                    }
                }
            }

            PuckManager puckManager = MonoBehaviourSingleton<PuckManager>.Instance;
            if (puckManager != null && ReplayPuckNetworkObjectIdMapField != null && this.replayPlayer != null)
            {
                Dictionary<ulong, ulong> puckIdMap = ReplayPuckNetworkObjectIdMapField.GetValue(this.replayPlayer) as Dictionary<ulong, ulong>;
                if (puckIdMap != null)
                {
                    foreach (ReplayPuckMove move in this.pausedPuckSnapshots.Values)
                    {
                        ulong replayNetworkObjectId;
                        if (!puckIdMap.TryGetValue(move.NetworkObjectId, out replayNetworkObjectId))
                        {
                            continue;
                        }

                        Puck puck = puckManager.GetReplayPuckByNetworkObjectId(replayNetworkObjectId);
                        if (puck != null)
                        {
                            ApplyTransformAndRigidbody(puck.transform, puck.Rigidbody, move.Position, move.Rotation);
                        }
                    }
                }
            }

            this.ForceReplayPlayerMeshPoses();
        }

        private void ForceReplayPlayerMeshPoses()
        {
            PlayerManager playerManager = MonoBehaviourSingleton<PlayerManager>.Instance;
            if (playerManager == null)
            {
                return;
            }

            foreach (Player player in playerManager.GetReplayPlayers())
            {
                if (player == null || player.PlayerBody == null || player.PlayerBody.PlayerMesh == null)
                {
                    continue;
                }

                PlayerInput input = player.PlayerInput;
                player.PlayerBody.PlayerMesh.SetLegsPadsActive(player.Role == PlayerRole.Goalie);
                if (input != null && (input.LookInput.ServerValue || input.TrackInput.ServerValue) && player.PlayerCamera != null)
                {
                    player.PlayerCamera.transform.localRotation = Quaternion.Euler(input.LookAngleInput.ServerValue);
                    player.PlayerBody.PlayerMesh.LookAt(player.PlayerCamera.transform.position + player.PlayerCamera.transform.forward * 10f, 1f, true, true);
                }
                else if (player.Stick != null)
                {
                    player.PlayerBody.PlayerMesh.LookAt(player.Stick.BladeHandlePosition, 1f, true, true);
                }
            }
        }

        private bool HasLifecycleEventsBetween(int lowerExclusiveTick, int upperInclusiveTick)
        {
            if (this.currentEventMap == null)
            {
                return false;
            }

            foreach (KeyValuePair<int, List<ValueTuple<string, object>>> entry in this.currentEventMap)
            {
                if (entry.Key <= lowerExclusiveTick)
                {
                    continue;
                }

                if (entry.Key > upperInclusiveTick)
                {
                    break;
                }

                foreach (ValueTuple<string, object> replayEvent in entry.Value)
                {
                    if (IsLifecycleEvent(replayEvent.Item1))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsLifecycleEvent(string eventName)
        {
            return eventName == "PlayerSpawned" ||
                eventName == "PlayerDespawned" ||
                eventName == "PlayerBodySpawned" ||
                eventName == "PlayerBodyDespawned" ||
                eventName == "StickSpawned" ||
                eventName == "StickDespawned" ||
                eventName == "PuckSpawned" ||
                eventName == "PuckDespawned";
        }
    }
}
