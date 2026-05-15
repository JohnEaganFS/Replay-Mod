using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace PuckReplayMod
{
    public class NativeReplayEventConverter
    {
        private readonly Dictionary<ulong, PlayerSnapshotPayload> players = new Dictionary<ulong, PlayerSnapshotPayload>();
        private readonly HashSet<ulong> spawnedPlayers = new HashSet<ulong>();
        private readonly HashSet<ulong> spawnedBodies = new HashSet<ulong>();
        private readonly HashSet<ulong> spawnedSticks = new HashSet<ulong>();
        private readonly HashSet<ulong> spawnedPucks = new HashSet<ulong>();
        private int playerSpawnEvents;
        private int playerBodySpawnEvents;
        private int stickSpawnEvents;
        private int puckSpawnEvents;
        private int playerMoveEvents;
        private int stickMoveEvents;
        private int puckMoveEvents;

        public SortedList<int, List<ValueTuple<string, object>>> Convert(ReplaySessionData session)
        {
            this.players.Clear();
            this.spawnedPlayers.Clear();
            this.spawnedBodies.Clear();
            this.spawnedSticks.Clear();
            this.spawnedPucks.Clear();
            this.playerSpawnEvents = 0;
            this.playerBodySpawnEvents = 0;
            this.stickSpawnEvents = 0;
            this.puckSpawnEvents = 0;
            this.playerMoveEvents = 0;
            this.stickMoveEvents = 0;
            this.puckMoveEvents = 0;

            SortedList<int, List<ValueTuple<string, object>>> eventMap = new SortedList<int, List<ValueTuple<string, object>>>();
            if (session == null)
            {
                return eventMap;
            }

            List<ReplayEventDto> events = new List<ReplayEventDto>(session.Events);
            events.Sort((left, right) => left.Tick.CompareTo(right.Tick));

            foreach (ReplayEventDto replayEvent in events)
            {
                if (replayEvent == null)
                {
                    continue;
                }

                this.ConvertEvent(eventMap, replayEvent);
            }

            ReplayModLog.Info(
                "Native replay conversion: player spawns " + this.playerSpawnEvents +
                ", body spawns " + this.playerBodySpawnEvents +
                ", stick spawns " + this.stickSpawnEvents +
                ", puck spawns " + this.puckSpawnEvents +
                ", body moves " + this.playerMoveEvents +
                ", stick moves " + this.stickMoveEvents +
                ", puck moves " + this.puckMoveEvents +
                ", ticks " + eventMap.Count + ".");

            return eventMap;
        }

        private void ConvertEvent(SortedList<int, List<ValueTuple<string, object>>> eventMap, ReplayEventDto replayEvent)
        {
            switch (replayEvent.Type)
            {
                case "InitialSnapshot":
                    this.ConvertInitialSnapshot(eventMap, replayEvent.Tick, replayEvent.Payload as InitialSnapshotPayload);
                    break;
                case "TransformFrame":
                    this.ConvertTransformFrame(eventMap, replayEvent.Tick, replayEvent.Payload as TransformFramePayload);
                    break;
                case "PlayerSpawned":
                    this.ConvertPlayerSpawned(eventMap, replayEvent.Tick, replayEvent.Payload as PlayerLifecyclePayload);
                    break;
                case "PlayerDespawned":
                    this.ConvertPlayerDespawned(eventMap, replayEvent.Tick, replayEvent.Payload as PlayerLifecyclePayload);
                    break;
                case "PlayerBodySpawned":
                    this.ConvertPlayerBodySpawned(eventMap, replayEvent.Tick, replayEvent.Payload as BodyLifecyclePayload);
                    break;
                case "PlayerBodyDespawned":
                    this.ConvertPlayerBodyDespawned(eventMap, replayEvent.Tick, replayEvent.Payload as BodyLifecyclePayload);
                    break;
                case "PuckSpawned":
                    this.ConvertPuckSpawned(eventMap, replayEvent.Tick, replayEvent.Payload as PuckLifecyclePayload);
                    break;
                case "PuckDespawned":
                    this.ConvertPuckDespawned(eventMap, replayEvent.Tick, replayEvent.Payload as PuckLifecyclePayload);
                    break;
                case "PlayerState":
                    this.TrackPlayer(replayEvent.Payload as PlayerSnapshotPayload);
                    break;
            }
        }

        private void ConvertInitialSnapshot(SortedList<int, List<ValueTuple<string, object>>> eventMap, int tick, InitialSnapshotPayload snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            foreach (PlayerSnapshotPayload player in snapshot.Players)
            {
                this.TrackPlayer(player);
                this.EnsurePlayerSpawned(eventMap, tick, player);
            }

            foreach (StickSnapshotPayload stick in snapshot.Sticks)
            {
                this.EnsureStickSpawned(eventMap, tick, stick.OwnerClientId, ToVector3(stick.Position), ToQuaternion(stick.Rotation));
            }

            foreach (BodyLifecyclePayload body in snapshot.PlayerBodies)
            {
                this.EnsurePlayerSpawned(eventMap, tick, this.GetOrCreatePlayer(body.OwnerClientId));
                this.EnsureBodySpawned(eventMap, tick, body.OwnerClientId, ToVector3(body.Position), ToQuaternion(body.Rotation));
            }

            foreach (PuckSnapshotPayload puck in snapshot.Pucks)
            {
                this.EnsurePuckSpawned(eventMap, tick, puck.NetworkObjectId, ToVector3(puck.Position), ToQuaternion(puck.Rotation));
            }
        }

        private void ConvertTransformFrame(SortedList<int, List<ValueTuple<string, object>>> eventMap, int tick, TransformFramePayload frame)
        {
            if (frame == null)
            {
                return;
            }

            foreach (PlayerBodyTransformPayload body in frame.PlayerBodies)
            {
                Vector3 position = ToVector3(body.Position);
                Quaternion rotation = ToQuaternion(body.Rotation);
                this.EnsurePlayerSpawned(eventMap, tick, this.GetOrCreatePlayer(body.OwnerClientId));
                this.EnsureBodySpawned(eventMap, tick, body.OwnerClientId, position, rotation);
                this.Add(eventMap, tick, "PlayerBodyMove", new ReplayPlayerBodyMove
                {
                    OwnerClientId = body.OwnerClientId,
                    Position = position,
                    Rotation = rotation,
                    Stamina = body.Stamina,
                    Speed = body.Speed,
                    IsSprinting = body.IsSprinting,
                    IsSliding = body.IsSliding,
                    IsStopping = body.IsStopping,
                    IsExtendedLeft = body.IsExtendedLeft,
                    IsExtendedRight = body.IsExtendedRight
                });
                this.playerMoveEvents++;
            }

            foreach (StickTransformPayload stick in frame.Sticks)
            {
                Vector3 position = ToVector3(stick.Position);
                Quaternion rotation = ToQuaternion(stick.Rotation);
                this.EnsurePlayerSpawned(eventMap, tick, this.GetOrCreatePlayer(stick.OwnerClientId));
                this.EnsureStickSpawned(eventMap, tick, stick.OwnerClientId, position, rotation);
                this.Add(eventMap, tick, "StickMove", new ReplayStickMove
                {
                    OwnerClientId = stick.OwnerClientId,
                    Position = position,
                    Rotation = rotation
                });
                this.stickMoveEvents++;
            }

            foreach (PuckTransformPayload puck in frame.Pucks)
            {
                Vector3 position = ToVector3(puck.Position);
                Quaternion rotation = ToQuaternion(puck.Rotation);
                this.EnsurePuckSpawned(eventMap, tick, puck.NetworkObjectId, position, rotation);
                this.Add(eventMap, tick, "PuckMove", new ReplayPuckMove
                {
                    NetworkObjectId = puck.NetworkObjectId,
                    Position = position,
                    Rotation = rotation
                });
                this.puckMoveEvents++;
            }
        }

        private void ConvertPlayerSpawned(SortedList<int, List<ValueTuple<string, object>>> eventMap, int tick, PlayerLifecyclePayload payload)
        {
            if (payload == null || payload.Player == null)
            {
                return;
            }

            this.TrackPlayer(payload.Player);
            this.EnsurePlayerSpawned(eventMap, tick, payload.Player);
        }

        private void ConvertPlayerDespawned(SortedList<int, List<ValueTuple<string, object>>> eventMap, int tick, PlayerLifecyclePayload payload)
        {
            if (payload == null || payload.Player == null)
            {
                return;
            }

            this.Add(eventMap, tick, "PlayerDespawned", new ReplayPlayerDespawned
            {
                OwnerClientId = payload.Player.OwnerClientId
            });
            this.spawnedPlayers.Remove(payload.Player.OwnerClientId);
            this.spawnedBodies.Remove(payload.Player.OwnerClientId);
            this.spawnedSticks.Remove(payload.Player.OwnerClientId);
        }

        private void ConvertPlayerBodySpawned(SortedList<int, List<ValueTuple<string, object>>> eventMap, int tick, BodyLifecyclePayload payload)
        {
            if (payload == null)
            {
                return;
            }

            this.EnsurePlayerSpawned(eventMap, tick, this.GetOrCreatePlayer(payload.OwnerClientId));
            this.EnsureBodySpawned(eventMap, tick, payload.OwnerClientId, ToVector3(payload.Position), ToQuaternion(payload.Rotation));
        }

        private void ConvertPlayerBodyDespawned(SortedList<int, List<ValueTuple<string, object>>> eventMap, int tick, BodyLifecyclePayload payload)
        {
            if (payload == null)
            {
                return;
            }

            this.Add(eventMap, tick, "PlayerBodyDespawned", new ReplayPlayerBodyDespawned
            {
                OwnerClientId = payload.OwnerClientId
            });
            this.spawnedBodies.Remove(payload.OwnerClientId);
        }

        private void ConvertPuckSpawned(SortedList<int, List<ValueTuple<string, object>>> eventMap, int tick, PuckLifecyclePayload payload)
        {
            if (payload == null)
            {
                return;
            }

            this.EnsurePuckSpawned(eventMap, tick, payload.NetworkObjectId, ToVector3(payload.Position), ToQuaternion(payload.Rotation));
        }

        private void ConvertPuckDespawned(SortedList<int, List<ValueTuple<string, object>>> eventMap, int tick, PuckLifecyclePayload payload)
        {
            if (payload == null)
            {
                return;
            }

            this.Add(eventMap, tick, "PuckDespawned", new ReplayPuckDespawned
            {
                NetworkObjectId = payload.NetworkObjectId
            });
            this.spawnedPucks.Remove(payload.NetworkObjectId);
        }

        private void EnsurePlayerSpawned(SortedList<int, List<ValueTuple<string, object>>> eventMap, int tick, PlayerSnapshotPayload player)
        {
            if (player == null || this.spawnedPlayers.Contains(player.OwnerClientId))
            {
                return;
            }

            this.Add(eventMap, tick, "PlayerSpawned", new ReplayPlayerSpawned
            {
                OwnerClientId = player.OwnerClientId,
                GameState = BuildGameState(player),
                CustomizationState = BuildCustomizationState(player),
                Handedness = ParseEnum(player.Handedness, PlayerHandedness.Right),
                SteamId = ToFixedString(player.SteamId),
                Username = ToFixedString(player.Username),
                Number = player.Number,
                PatreonLevel = player.PatreonLevel,
                AdminLevel = player.AdminLevel,
                IsMuted = player.IsMuted
            });
            this.spawnedPlayers.Add(player.OwnerClientId);
            this.playerSpawnEvents++;
        }

        private void EnsureBodySpawned(SortedList<int, List<ValueTuple<string, object>>> eventMap, int tick, ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            if (this.spawnedBodies.Contains(ownerClientId))
            {
                return;
            }

            PlayerSnapshotPayload player = this.GetOrCreatePlayer(ownerClientId);
            this.Add(eventMap, tick, "PlayerBodySpawned", new ReplayPlayerBodySpawned
            {
                OwnerClientId = ownerClientId,
                Position = position,
                Rotation = rotation,
                GameState = BuildGameState(player),
                CustomizationState = BuildCustomizationState(player),
                Username = ToFixedString(player.Username),
                Number = player.Number
            });
            this.spawnedBodies.Add(ownerClientId);
            this.playerBodySpawnEvents++;
        }

        private void EnsureStickSpawned(SortedList<int, List<ValueTuple<string, object>>> eventMap, int tick, ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            if (this.spawnedSticks.Contains(ownerClientId))
            {
                return;
            }

            this.Add(eventMap, tick, "StickSpawned", new ReplayStickSpawned
            {
                OwnerClientId = ownerClientId,
                Position = position,
                Rotation = rotation
            });
            this.spawnedSticks.Add(ownerClientId);
            this.stickSpawnEvents++;
        }

        private void EnsurePuckSpawned(SortedList<int, List<ValueTuple<string, object>>> eventMap, int tick, ulong networkObjectId, Vector3 position, Quaternion rotation)
        {
            if (this.spawnedPucks.Contains(networkObjectId))
            {
                return;
            }

            this.Add(eventMap, tick, "PuckSpawned", new ReplayPuckSpawned
            {
                NetworkObjectId = networkObjectId,
                Position = position,
                Rotation = rotation
            });
            this.spawnedPucks.Add(networkObjectId);
            this.puckSpawnEvents++;
        }

        private void TrackPlayer(PlayerSnapshotPayload player)
        {
            if (player != null)
            {
                this.players[player.OwnerClientId] = player;
            }
        }

        private PlayerSnapshotPayload GetOrCreatePlayer(ulong ownerClientId)
        {
            PlayerSnapshotPayload player;
            if (this.players.TryGetValue(ownerClientId, out player))
            {
                return player;
            }

            player = new PlayerSnapshotPayload
            {
                OwnerClientId = ownerClientId,
                SteamId = string.Empty,
                Username = "Replay Player " + ownerClientId,
                Number = 0,
                Phase = PlayerPhase.Play.ToString(),
                Team = PlayerTeam.Spectator.ToString(),
                Role = PlayerRole.Attacker.ToString(),
                Handedness = PlayerHandedness.Right.ToString()
            };
            this.players[ownerClientId] = player;
            return player;
        }

        private void Add(SortedList<int, List<ValueTuple<string, object>>> eventMap, int tick, string eventName, object eventData)
        {
            List<ValueTuple<string, object>> events;
            if (!eventMap.TryGetValue(tick, out events))
            {
                events = new List<ValueTuple<string, object>>();
                eventMap.Add(tick, events);
            }

            events.Add(new ValueTuple<string, object>(eventName, eventData));
        }

        private static PlayerGameState BuildGameState(PlayerSnapshotPayload player)
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

        private static PlayerCustomizationState BuildCustomizationState(PlayerSnapshotPayload player)
        {
            PlayerCustomizationPayload customization = player != null ? player.Customization : null;
            if (customization == null)
            {
                return default(PlayerCustomizationState);
            }

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

        private static FixedString32Bytes ToFixedString(string value)
        {
            return new FixedString32Bytes(value ?? string.Empty);
        }

        private static Vector3 ToVector3(Vector3Dto dto)
        {
            return dto == null ? Vector3.zero : new Vector3(dto.X, dto.Y, dto.Z);
        }

        private static Quaternion ToQuaternion(QuaternionDto dto)
        {
            return dto == null ? Quaternion.identity : new Quaternion(dto.X, dto.Y, dto.Z, dto.W);
        }
    }
}
