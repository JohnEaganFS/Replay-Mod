using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuckReplayMod
{
    public class ReplayGameStatePlaybackService
    {
        private static readonly FieldInfo ScoreboardPlayerMapField = typeof(UIScoreboard).GetField("playerVisualElementMap", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo MinimapPlayerBodyMapField = typeof(UIMinimap).GetField("playerBodyVisualElementMap", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo MinimapPuckMapField = typeof(UIMinimap).GetField("puckVisualElementMap", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo MinimapUpdateMethod = typeof(UIMinimap).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo UsernamesPlayerBodyMapField = typeof(UIUsernames).GetField("playerBodyVisualElementMap", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly ReplayModSettings settings;
        private readonly List<ReplayEventDto> events = new List<ReplayEventDto>();
        private readonly Dictionary<ulong, PlayerSnapshotPayload> playerStates = new Dictionary<ulong, PlayerSnapshotPayload>();
        private readonly Dictionary<ulong, Player> scoreboardPlayers = new Dictionary<ulong, Player>();
        private readonly List<ChatMessage> replayChatMessages = new List<ChatMessage>();

        private int nextEventIndex;
        private int lastAppliedTick = -1;
        private GameStatePayload gameState;

        public static bool IsApplyingReplayGameState { get; private set; }

        public ReplayGameStatePlaybackService(ReplayModSettings settings)
        {
            this.settings = settings;
        }

        public void Start(ReplaySessionData session)
        {
            this.Close();
            if (this.settings == null || this.settings.ClearChatOnPlaybackStart)
            {
                this.ClearChatMessages();
            }
            if (session == null || session.Events == null)
            {
                return;
            }

            this.events.AddRange(session.Events);
            this.events.Sort((left, right) => left.Tick.CompareTo(right.Tick));
        }

        public void ApplyThrough(int replayTick)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            RemoveLocalNonReplayPlayerFromScoreboard();

            if (replayTick < this.lastAppliedTick)
            {
                this.Rewind();
            }

            while (this.nextEventIndex < this.events.Count && this.events[this.nextEventIndex].Tick <= replayTick)
            {
                this.ApplyEvent(this.events[this.nextEventIndex]);
                this.nextEventIndex++;
            }

            this.lastAppliedTick = replayTick;
            this.ApplyGameState();
            this.ApplyPlayerStates();
            RemoveLocalNonReplayPlayerFromScoreboard();
        }

        public void Close()
        {
            this.RemoveScoreboardPlayers();
            RemoveAllReplayPlayersFromScoreboard();
            RemoveAllReplayObjectsFromMinimap();
            RemoveAllReplayObjectsFromPlayerUsernames();
            this.ClearChatMessages();
            this.events.Clear();
            this.playerStates.Clear();
            this.scoreboardPlayers.Clear();
            this.replayChatMessages.Clear();
            this.nextEventIndex = 0;
            this.lastAppliedTick = -1;
            this.gameState = null;
        }

        private void Rewind()
        {
            this.RemoveScoreboardPlayers();
            RemoveAllReplayObjectsFromMinimap();
            RemoveAllReplayObjectsFromPlayerUsernames();
            this.playerStates.Clear();
            this.scoreboardPlayers.Clear();
            this.ClearChatMessages();
            this.replayChatMessages.Clear();
            this.nextEventIndex = 0;
            this.lastAppliedTick = -1;
            this.gameState = null;
        }

        private void ApplyEvent(ReplayEventDto replayEvent)
        {
            if (replayEvent == null)
            {
                return;
            }

            switch (replayEvent.Type)
            {
                case "InitialSnapshot":
                    this.ApplyInitialSnapshot(replayEvent.Payload as InitialSnapshotPayload);
                    break;
                case "GameState":
                    this.gameState = replayEvent.Payload as GameStatePayload;
                    break;
                case "ScoreboardSnapshot":
                    this.ApplyScoreboardSnapshot(replayEvent.Payload as ScoreboardSnapshotPayload);
                    break;
                case "PlayerState":
                    this.TrackPlayer(replayEvent.Payload as PlayerSnapshotPayload);
                    break;
                case "PlayerSpawned":
                    this.TrackPlayer((replayEvent.Payload as PlayerLifecyclePayload)?.Player);
                    break;
                case "PlayerDespawned":
                    this.RemovePlayer((replayEvent.Payload as PlayerLifecyclePayload)?.Player);
                    break;
                case "ChatMessage":
                    this.ApplyChatMessage(replayEvent.Payload as ChatMessagePayload);
                    break;
            }
        }

        private void ApplyInitialSnapshot(InitialSnapshotPayload snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            if (snapshot.GameState != null)
            {
                this.gameState = snapshot.GameState;
            }

            foreach (PlayerSnapshotPayload player in snapshot.Players)
            {
                this.TrackPlayer(player);
            }
        }

        private void ApplyScoreboardSnapshot(ScoreboardSnapshotPayload snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            foreach (PlayerSnapshotPayload player in snapshot.Players)
            {
                this.TrackPlayer(player);
            }
        }

        private void TrackPlayer(PlayerSnapshotPayload player)
        {
            if (player != null)
            {
                this.playerStates[player.OwnerClientId] = player;
            }
        }

        private void RemovePlayer(PlayerSnapshotPayload player)
        {
            if (player == null)
            {
                return;
            }

            this.playerStates.Remove(player.OwnerClientId);
            this.RemoveScoreboardPlayer(player.OwnerClientId);
        }

        private void ApplyChatMessage(ChatMessagePayload payload)
        {
            if (this.settings != null && !this.settings.ShowReplayChat)
            {
                return;
            }

            if (payload == null)
            {
                return;
            }

            ChatMessage chatMessage = new ChatMessage
            {
                SteamID = string.IsNullOrEmpty(payload.SteamId) ? (FixedString32Bytes?)null : new FixedString32Bytes(payload.SteamId),
                Username = string.IsNullOrEmpty(payload.Username) ? (FixedString32Bytes?)null : new FixedString32Bytes(payload.Username),
                Team = ParseNullableEnum<PlayerTeam>(payload.Team),
                Content = new FixedString512Bytes(payload.Message ?? string.Empty),
                Timestamp = Utils.GetTimestamp(),
                IsQuickChat = payload.IsQuickChat,
                IsTeamChat = payload.IsTeamChat,
                IsSystem = payload.IsSystem
            };

            ChatManager chatManager = NetworkBehaviourSingleton<ChatManager>.Instance;
            if (chatManager != null)
            {
                chatManager.AddChatMessage(chatMessage);
                this.replayChatMessages.Add(chatMessage);
                return;
            }

            EventManager.TriggerEvent("Event_OnChatMessageAdded", new Dictionary<string, object>
            {
                {
                    "chatMessage",
                    chatMessage
                }
            });
            this.replayChatMessages.Add(chatMessage);
        }

        private void ApplyGameState()
        {
            if (this.gameState == null)
            {
                return;
            }

            GameManager gameManager = NetworkBehaviourSingleton<GameManager>.Instance;
            if (gameManager == null)
            {
                return;
            }

            try
            {
                IsApplyingReplayGameState = true;
                gameManager.Server_SetGameState(
                    ParseEnum(this.gameState.Phase, GamePhase.None),
                    Math.Max(0, this.gameState.Tick),
                    Math.Max(0, this.gameState.Period),
                    Math.Max(0, this.gameState.BlueScore),
                    Math.Max(0, this.gameState.RedScore),
                    this.gameState.IsOvertime);
                gameManager.Server_StopTicking();
            }
            finally
            {
                IsApplyingReplayGameState = false;
            }
        }

        private void ApplyPlayerStates()
        {
            PlayerManager playerManager = MonoBehaviourSingleton<PlayerManager>.Instance;
            if (playerManager == null)
            {
                return;
            }

            foreach (PlayerSnapshotPayload playerState in this.playerStates.Values)
            {
                Player replayPlayer = playerManager.GetReplayPlayerByClientId(playerState.OwnerClientId);
                if (replayPlayer == null)
                {
                    continue;
                }

                this.ApplyPlayerState(replayPlayer, playerState);
                this.EnsureScoreboardPlayer(playerState.OwnerClientId, replayPlayer);
            }
        }

        private void ApplyPlayerState(Player replayPlayer, PlayerSnapshotPayload playerState)
        {
            replayPlayer.GameState.Value = BuildPlayerGameState(playerState);
            replayPlayer.Handedness.Value = ParseEnum(playerState.Handedness, PlayerHandedness.Right);
            replayPlayer.SteamId.Value = ToFixedString(playerState.SteamId);
            replayPlayer.Username.Value = ToFixedString(playerState.Username);
            replayPlayer.Number.Value = playerState.Number;
            replayPlayer.PatreonLevel.Value = playerState.PatreonLevel;
            replayPlayer.AdminLevel.Value = playerState.AdminLevel;
            replayPlayer.Goals.Value = Math.Max(0, playerState.Goals);
            replayPlayer.Assists.Value = Math.Max(0, playerState.Assists);
            replayPlayer.IsMuted.Value = playerState.IsMuted;
            if (playerState.Customization != null)
            {
                replayPlayer.CustomizationState.Value = BuildCustomizationState(playerState.Customization);
            }
        }

        private void EnsureScoreboardPlayer(ulong originalOwnerClientId, Player replayPlayer)
        {
            UIManager uiManager = MonoBehaviourSingleton<UIManager>.Instance;
            if (uiManager == null || uiManager.Scoreboard == null || this.scoreboardPlayers.ContainsKey(originalOwnerClientId))
            {
                return;
            }

            uiManager.Scoreboard.AddPlayer(replayPlayer);
            uiManager.Scoreboard.StylePlayer(replayPlayer);
            this.scoreboardPlayers[originalOwnerClientId] = replayPlayer;
        }

        private void RemoveScoreboardPlayers()
        {
            List<ulong> ownerClientIds = new List<ulong>(this.scoreboardPlayers.Keys);
            foreach (ulong ownerClientId in ownerClientIds)
            {
                this.RemoveScoreboardPlayer(ownerClientId);
            }
        }

        private void RemoveScoreboardPlayer(ulong originalOwnerClientId)
        {
            UIManager uiManager = MonoBehaviourSingleton<UIManager>.Instance;
            Player replayPlayer;
            this.scoreboardPlayers.TryGetValue(originalOwnerClientId, out replayPlayer);
            if (replayPlayer == null)
            {
                PlayerManager playerManager = MonoBehaviourSingleton<PlayerManager>.Instance;
                replayPlayer = playerManager != null ? playerManager.GetReplayPlayerByClientId(originalOwnerClientId) : null;
            }

            if (uiManager != null && uiManager.Scoreboard != null && replayPlayer != null)
            {
                uiManager.Scoreboard.RemovePlayer(replayPlayer);
            }

            this.scoreboardPlayers.Remove(originalOwnerClientId);
        }

        public static void RemoveAllReplayPlayersFromScoreboard()
        {
            UIManager uiManager = MonoBehaviourSingleton<UIManager>.Instance;
            if (uiManager == null || uiManager.Scoreboard == null || ScoreboardPlayerMapField == null)
            {
                return;
            }

            Dictionary<Player, VisualElement> playerRows = ScoreboardPlayerMapField.GetValue(uiManager.Scoreboard) as Dictionary<Player, VisualElement>;
            if (playerRows == null || playerRows.Count == 0)
            {
                return;
            }

            List<Player> playersToRemove = new List<Player>();
            foreach (KeyValuePair<Player, VisualElement> entry in playerRows)
            {
                if (IsReplayOrDestroyedPlayer(entry.Key))
                {
                    playersToRemove.Add(entry.Key);
                }
            }

            foreach (Player player in playersToRemove)
            {
                VisualElement row;
                if (playerRows.TryGetValue(player, out row) && row != null && row.parent != null)
                {
                    row.parent.Remove(row);
                }

                playerRows.Remove(player);
            }
        }

        public static void RemoveLocalNonReplayPlayerFromScoreboard()
        {
            UIManager uiManager = MonoBehaviourSingleton<UIManager>.Instance;
            if (uiManager == null || uiManager.Scoreboard == null || ScoreboardPlayerMapField == null)
            {
                return;
            }

            Dictionary<Player, VisualElement> playerRows = ScoreboardPlayerMapField.GetValue(uiManager.Scoreboard) as Dictionary<Player, VisualElement>;
            if (playerRows == null || playerRows.Count == 0)
            {
                return;
            }

            List<Player> playersToRemove = new List<Player>();
            foreach (KeyValuePair<Player, VisualElement> entry in playerRows)
            {
                if (IsLocalNonReplayPlayer(entry.Key))
                {
                    playersToRemove.Add(entry.Key);
                }
            }

            foreach (Player player in playersToRemove)
            {
                VisualElement row;
                if (playerRows.TryGetValue(player, out row) && row != null && row.parent != null)
                {
                    row.parent.Remove(row);
                }

                playerRows.Remove(player);
            }
        }

        public static void RemoveAllReplayObjectsFromMinimap()
        {
            UIManager uiManager = MonoBehaviourSingleton<UIManager>.Instance;
            if (uiManager == null || uiManager.Minimap == null)
            {
                return;
            }

            RemoveReplayMinimapPlayerBodies(uiManager.Minimap);
            RemoveReplayMinimapPucks(uiManager.Minimap);
        }

        public static void RemoveAllReplayObjectsFromPlayerUsernames()
        {
            UIManager uiManager = MonoBehaviourSingleton<UIManager>.Instance;
            if (uiManager == null || uiManager.Usernames == null || UsernamesPlayerBodyMapField == null)
            {
                return;
            }

            Dictionary<PlayerBody, VisualElement> playerBodyRows = UsernamesPlayerBodyMapField.GetValue(uiManager.Usernames) as Dictionary<PlayerBody, VisualElement>;
            if (playerBodyRows == null || playerBodyRows.Count == 0)
            {
                return;
            }

            List<PlayerBody> bodiesToRemove = new List<PlayerBody>();
            foreach (KeyValuePair<PlayerBody, VisualElement> entry in playerBodyRows)
            {
                if (IsReplayOrDestroyedPlayerBody(entry.Key))
                {
                    bodiesToRemove.Add(entry.Key);
                }
            }

            foreach (PlayerBody body in bodiesToRemove)
            {
                VisualElement row;
                if (playerBodyRows.TryGetValue(body, out row) && row != null && row.parent != null)
                {
                    row.parent.Remove(row);
                }

                playerBodyRows.Remove(body);
            }
        }

        public static void EnsureReplayObjectsOnMinimap()
        {
            UIManager uiManager = MonoBehaviourSingleton<UIManager>.Instance;
            if (uiManager == null || uiManager.Minimap == null)
            {
                return;
            }

            PlayerManager playerManager = MonoBehaviourSingleton<PlayerManager>.Instance;
            if (playerManager != null)
            {
                foreach (Player player in playerManager.GetReplayPlayers())
                {
                    if (player != null && player.PlayerBody != null)
                    {
                        uiManager.Minimap.AddPlayerBody(player.PlayerBody);
                        uiManager.Minimap.StylePlayer(player.PlayerBody);
                    }
                }
            }

            PuckManager puckManager = MonoBehaviourSingleton<PuckManager>.Instance;
            if (puckManager != null)
            {
                foreach (Puck puck in puckManager.GetReplayPucks())
                {
                    if (puck != null)
                    {
                        uiManager.Minimap.AddPuck(puck);
                    }
                }
            }

            ForceMinimapUpdate(uiManager.Minimap);
        }

        private static void ForceMinimapUpdate(UIMinimap minimap)
        {
            if (minimap == null || MinimapUpdateMethod == null)
            {
                return;
            }

            try
            {
                MinimapUpdateMethod.Invoke(minimap, null);
            }
            catch (Exception exception)
            {
                ReplayModLog.Warning("Failed to force replay minimap update: " + exception.Message);
            }
        }

        private static void RemoveReplayMinimapPlayerBodies(UIMinimap minimap)
        {
            if (MinimapPlayerBodyMapField == null)
            {
                return;
            }

            Dictionary<PlayerBody, VisualElement> playerBodyRows = MinimapPlayerBodyMapField.GetValue(minimap) as Dictionary<PlayerBody, VisualElement>;
            if (playerBodyRows == null || playerBodyRows.Count == 0)
            {
                return;
            }

            List<PlayerBody> bodiesToRemove = new List<PlayerBody>();
            foreach (KeyValuePair<PlayerBody, VisualElement> entry in playerBodyRows)
            {
                if (IsReplayOrDestroyedPlayerBody(entry.Key))
                {
                    bodiesToRemove.Add(entry.Key);
                }
            }

            foreach (PlayerBody body in bodiesToRemove)
            {
                VisualElement row;
                if (playerBodyRows.TryGetValue(body, out row) && row != null && row.parent != null)
                {
                    row.parent.Remove(row);
                }

                playerBodyRows.Remove(body);
            }
        }

        private static void RemoveReplayMinimapPucks(UIMinimap minimap)
        {
            if (MinimapPuckMapField == null)
            {
                return;
            }

            Dictionary<Puck, VisualElement> puckRows = MinimapPuckMapField.GetValue(minimap) as Dictionary<Puck, VisualElement>;
            if (puckRows == null || puckRows.Count == 0)
            {
                return;
            }

            List<Puck> pucksToRemove = new List<Puck>();
            foreach (KeyValuePair<Puck, VisualElement> entry in puckRows)
            {
                if (IsReplayOrDestroyedPuck(entry.Key))
                {
                    pucksToRemove.Add(entry.Key);
                }
            }

            foreach (Puck puck in pucksToRemove)
            {
                VisualElement row;
                if (puckRows.TryGetValue(puck, out row) && row != null && row.parent != null)
                {
                    row.parent.Remove(row);
                }

                puckRows.Remove(puck);
            }
        }

        private static bool IsReplayOrDestroyedPlayer(Player player)
        {
            if (player == null)
            {
                return true;
            }

            try
            {
                return player.IsReplay != null && player.IsReplay.Value;
            }
            catch
            {
                return true;
            }
        }

        private static bool IsLocalNonReplayPlayer(Player player)
        {
            if (player == null)
            {
                return false;
            }

            try
            {
                return player.IsLocalPlayer && (player.IsReplay == null || !player.IsReplay.Value);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsReplayOrDestroyedPlayerBody(PlayerBody body)
        {
            if (body == null || body.Player == null)
            {
                return true;
            }

            try
            {
                return body.Player.IsReplay != null && body.Player.IsReplay.Value;
            }
            catch
            {
                return true;
            }
        }

        private static bool IsReplayOrDestroyedPuck(Puck puck)
        {
            if (puck == null)
            {
                return true;
            }

            try
            {
                return puck.IsReplay != null && puck.IsReplay.Value;
            }
            catch
            {
                return true;
            }
        }

        private void ClearChatMessages()
        {
            ChatManager chatManager = NetworkBehaviourSingleton<ChatManager>.Instance;
            if (chatManager != null)
            {
                chatManager.ClearChatMessages();
                return;
            }

            if (this.replayChatMessages.Count > 0)
            {
                EventManager.TriggerEvent("Event_OnChatMessagesCleared", null);
            }
        }

        private static PlayerGameState BuildPlayerGameState(PlayerSnapshotPayload player)
        {
            PlayerPhase phase = ParseEnum(player != null ? player.Phase : null, PlayerPhase.Play);
            PlayerTeam team = ParseEnum(player != null ? player.Team : null, PlayerTeam.Spectator);
            PlayerRole role = ParseEnum(player != null ? player.Role : null, PlayerRole.Attacker);
            if (phase == PlayerPhase.None || phase == PlayerPhase.TeamSelect || phase == PlayerPhase.PositionSelect)
            {
                phase = PlayerPhase.Play;
            }

            if (role == PlayerRole.None)
            {
                role = PlayerRole.Attacker;
            }

            return new PlayerGameState
            {
                Phase = phase,
                Team = team,
                Role = role
            };
        }

        private static PlayerCustomizationState BuildCustomizationState(PlayerCustomizationPayload customization)
        {
            return new PlayerCustomizationState
            {
                FlagID = customization.FlagID,
                HeadgearIDBlueAttacker = customization.HeadgearIDBlueAttacker,
                HeadgearIDRedAttacker = customization.HeadgearIDRedAttacker,
                HeadgearIDBlueGoalie = customization.HeadgearIDBlueGoalie,
                HeadgearIDRedGoalie = customization.HeadgearIDRedGoalie,
                MustacheID = customization.MustacheID,
                BeardID = customization.BeardID,
                JerseyIDBlueAttacker = customization.JerseyIDBlueAttacker,
                JerseyIDRedAttacker = customization.JerseyIDRedAttacker,
                JerseyIDBlueGoalie = customization.JerseyIDBlueGoalie,
                JerseyIDRedGoalie = customization.JerseyIDRedGoalie,
                StickSkinIDBlueAttacker = customization.StickSkinIDBlueAttacker,
                StickSkinIDRedAttacker = customization.StickSkinIDRedAttacker,
                StickSkinIDBlueGoalie = customization.StickSkinIDBlueGoalie,
                StickSkinIDRedGoalie = customization.StickSkinIDRedGoalie,
                StickShaftTapeIDBlueAttacker = customization.StickShaftTapeIDBlueAttacker,
                StickShaftTapeIDRedAttacker = customization.StickShaftTapeIDRedAttacker,
                StickShaftTapeIDBlueGoalie = customization.StickShaftTapeIDBlueGoalie,
                StickShaftTapeIDRedGoalie = customization.StickShaftTapeIDRedGoalie,
                StickBladeTapeIDBlueAttacker = customization.StickBladeTapeIDBlueAttacker,
                StickBladeTapeIDRedAttacker = customization.StickBladeTapeIDRedAttacker,
                StickBladeTapeIDBlueGoalie = customization.StickBladeTapeIDBlueGoalie,
                StickBladeTapeIDRedGoalie = customization.StickBladeTapeIDRedGoalie
            };
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct
        {
            T parsed;
            if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, true, out parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static T? ParseNullableEnum<T>(string value) where T : struct
        {
            T parsed;
            if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, true, out parsed))
            {
                return parsed;
            }

            return null;
        }

        private static FixedString32Bytes ToFixedString(string value)
        {
            return new FixedString32Bytes(value ?? string.Empty);
        }
    }
}
