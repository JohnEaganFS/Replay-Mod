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
        internal static readonly Vector3 ReplaySpectatorSpawnPosition = new Vector3(0f, 6f, 0f);
        internal static readonly Quaternion ReplaySpectatorSpawnRotation = Quaternion.Euler(22f, 0f, 0f);

        private readonly NativeReplayEventConverter converter = new NativeReplayEventConverter();
        private readonly ReplayModSettings settings;
        private readonly ReplayGameStatePlaybackService gameStatePlayback;

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
            List<ReplayPlaybackPlayerTarget> targets = new List<ReplayPlaybackPlayerTarget>();
            PlayerManager playerManager = MonoBehaviourSingleton<PlayerManager>.Instance;
            if (playerManager == null)
            {
                return targets;
            }

            foreach (Player player in playerManager.GetReplayPlayers())
            {
                if (player == null)
                {
                    continue;
                }

                string username = player.Username.Value.ToString();
                string label = string.IsNullOrEmpty(username) ? "Replay Player " + player.OwnerClientId : username;
                if (player.Number.Value > 0)
                {
                    label = "#" + player.Number.Value + " " + label;
                }

                targets.Add(new ReplayPlaybackPlayerTarget(player.OwnerClientId, label));
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

            if (this.GetCameraTargetPlayer() != null)
            {
                return;
            }

            List<ReplayPlaybackPlayerTarget> targets = this.GetCameraTargets();
            this.cameraTargetClientId = targets.Count > 0 ? (ulong?)targets[0].OwnerClientId : null;
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
                Player selected = playerManager.GetReplayPlayerByClientId(this.cameraTargetClientId.Value);
                if (selected != null)
                {
                    return selected;
                }
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
