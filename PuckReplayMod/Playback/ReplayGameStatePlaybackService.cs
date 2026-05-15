using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;

namespace PuckReplayMod
{
    public class ReplayGameStatePlaybackService
    {
        private readonly List<ReplayEventDto> events = new List<ReplayEventDto>();
        private readonly Dictionary<ulong, PlayerSnapshotPayload> playerStates = new Dictionary<ulong, PlayerSnapshotPayload>();
        private readonly HashSet<ulong> scoreboardPlayers = new HashSet<ulong>();
        private readonly List<ChatMessage> replayChatMessages = new List<ChatMessage>();

        private int nextEventIndex;
        private int lastAppliedTick = -1;
        private GameStatePayload gameState;

        public void Start(ReplaySessionData session)
        {
            this.Close();
            this.ClearChatMessages();
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
        }

        public void Close()
        {
            this.RemoveScoreboardPlayers();
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

            gameManager.Server_SetGameState(
                ParseEnum(this.gameState.Phase, GamePhase.None),
                Math.Max(0, this.gameState.Tick),
                Math.Max(0, this.gameState.Period),
                Math.Max(0, this.gameState.BlueScore),
                Math.Max(0, this.gameState.RedScore),
                this.gameState.IsOvertime);
            gameManager.Server_StopTicking();
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
            if (uiManager == null || uiManager.Scoreboard == null || this.scoreboardPlayers.Contains(originalOwnerClientId))
            {
                return;
            }

            uiManager.Scoreboard.AddPlayer(replayPlayer);
            uiManager.Scoreboard.StylePlayer(replayPlayer);
            this.scoreboardPlayers.Add(originalOwnerClientId);
        }

        private void RemoveScoreboardPlayers()
        {
            List<ulong> ownerClientIds = new List<ulong>(this.scoreboardPlayers);
            foreach (ulong ownerClientId in ownerClientIds)
            {
                this.RemoveScoreboardPlayer(ownerClientId);
            }
        }

        private void RemoveScoreboardPlayer(ulong originalOwnerClientId)
        {
            UIManager uiManager = MonoBehaviourSingleton<UIManager>.Instance;
            PlayerManager playerManager = MonoBehaviourSingleton<PlayerManager>.Instance;
            Player replayPlayer = playerManager != null ? playerManager.GetReplayPlayerByClientId(originalOwnerClientId) : null;
            if (uiManager != null && uiManager.Scoreboard != null && replayPlayer != null)
            {
                uiManager.Scoreboard.RemovePlayer(replayPlayer);
            }

            this.scoreboardPlayers.Remove(originalOwnerClientId);
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
