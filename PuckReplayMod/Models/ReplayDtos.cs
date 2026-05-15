using System;
using System.Collections.Generic;
using UnityEngine;

namespace PuckReplayMod
{
    [Serializable]
    public class ReplaySessionData
    {
        public ReplayHeaderDto Header = new ReplayHeaderDto();
        public List<ReplayEventDto> Events = new List<ReplayEventDto>();
    }

    [Serializable]
    public class ReplayHeaderDto
    {
        public string Magic = ReplayModConstants.ReplayMagic;
        public int FormatVersion = 2;
        public string ModVersion = ReplayModConstants.ModVersion;
        public string GameVersion = "323";
        public string ServerName = "Unknown Server";
        public string RecordedBy = "Unknown Player";
        public long StartedUtcTicks;
        public long EndedUtcTicks;
        public int TickRate;
        public int TotalTicks;
        public int EventCount;
        public bool HasScoreboard;
        public bool HasChat;
        public bool HasMarkers;
    }

    [Serializable]
    public class ReplayEventDto
    {
        public int Tick;
        public string Type;
        public object Payload;
    }

    [Serializable]
    public class Vector3Dto
    {
        public float X;
        public float Y;
        public float Z;

        public static Vector3Dto From(Vector3 value)
        {
            return new Vector3Dto
            {
                X = value.x,
                Y = value.y,
                Z = value.z
            };
        }
    }

    [Serializable]
    public class QuaternionDto
    {
        public float X;
        public float Y;
        public float Z;
        public float W;

        public static QuaternionDto From(Quaternion value)
        {
            return new QuaternionDto
            {
                X = value.x,
                Y = value.y,
                Z = value.z,
                W = value.w
            };
        }
    }

    [Serializable]
    public class TransformFramePayload
    {
        public List<PlayerBodyTransformPayload> PlayerBodies = new List<PlayerBodyTransformPayload>();
        public List<StickTransformPayload> Sticks = new List<StickTransformPayload>();
        public List<PuckTransformPayload> Pucks = new List<PuckTransformPayload>();

        public TransformFramePayload()
        {
        }

        public TransformFramePayload(int playerBodyCapacity, int stickCapacity, int puckCapacity)
        {
            this.PlayerBodies = new List<PlayerBodyTransformPayload>(playerBodyCapacity);
            this.Sticks = new List<StickTransformPayload>(stickCapacity);
            this.Pucks = new List<PuckTransformPayload>(puckCapacity);
        }
    }

    [Serializable]
    public class PlayerBodyTransformPayload
    {
        public ulong OwnerClientId;
        public Vector3Dto Position;
        public QuaternionDto Rotation;
        public float Stamina;
        public float Speed;
        public bool IsSprinting;
        public bool IsSliding;
        public bool IsStopping;
        public bool IsExtendedLeft;
        public bool IsExtendedRight;
    }

    [Serializable]
    public class StickTransformPayload
    {
        public ulong OwnerClientId;
        public Vector3Dto Position;
        public QuaternionDto Rotation;
    }

    [Serializable]
    public class PuckTransformPayload
    {
        public ulong NetworkObjectId;
        public Vector3Dto Position;
        public QuaternionDto Rotation;
    }

    [Serializable]
    public class PlayerSnapshotPayload
    {
        public ulong OwnerClientId;
        public string SteamId;
        public string Username;
        public int Number;
        public int Goals;
        public int Assists;
        public int PatreonLevel;
        public int AdminLevel;
        public string Phase;
        public string Team;
        public string Role;
        public string PositionName;
        public bool IsMuted;
    }

    [Serializable]
    public class PuckSnapshotPayload
    {
        public ulong NetworkObjectId;
        public Vector3Dto Position;
        public QuaternionDto Rotation;
    }

    [Serializable]
    public class StickSnapshotPayload
    {
        public ulong OwnerClientId;
        public Vector3Dto Position;
        public QuaternionDto Rotation;
    }

    [Serializable]
    public class InitialSnapshotPayload
    {
        public GameStatePayload GameState;
        public List<PlayerSnapshotPayload> Players = new List<PlayerSnapshotPayload>();
        public List<PuckSnapshotPayload> Pucks = new List<PuckSnapshotPayload>();
        public List<StickSnapshotPayload> Sticks = new List<StickSnapshotPayload>();

        public InitialSnapshotPayload()
        {
        }

        public InitialSnapshotPayload(int playerCapacity, int puckCapacity, int stickCapacity)
        {
            this.Players = new List<PlayerSnapshotPayload>(playerCapacity);
            this.Pucks = new List<PuckSnapshotPayload>(puckCapacity);
            this.Sticks = new List<StickSnapshotPayload>(stickCapacity);
        }
    }

    [Serializable]
    public class GameStatePayload
    {
        public string Phase;
        public int Tick;
        public int Period;
        public int BlueScore;
        public int RedScore;
        public bool IsOvertime;
    }

    [Serializable]
    public class ChatMessagePayload
    {
        public string SteamId;
        public string Username;
        public string Team;
        public string Message;
        public bool IsQuickChat;
        public bool IsTeamChat;
        public bool IsSystem;
    }

    [Serializable]
    public class MarkerPayload
    {
        public long CreatedUtcTicks;
    }

    [Serializable]
    public class PlayerLifecyclePayload
    {
        public PlayerSnapshotPayload Player;
    }

    [Serializable]
    public class BodyLifecyclePayload
    {
        public ulong OwnerClientId;
        public Vector3Dto Position;
        public QuaternionDto Rotation;
    }

    [Serializable]
    public class PuckLifecyclePayload
    {
        public ulong NetworkObjectId;
        public Vector3Dto Position;
        public QuaternionDto Rotation;
    }

    [Serializable]
    public class ScoreboardSnapshotPayload
    {
        public List<PlayerSnapshotPayload> Players = new List<PlayerSnapshotPayload>();

        public ScoreboardSnapshotPayload()
        {
        }

        public ScoreboardSnapshotPayload(int playerCapacity)
        {
            this.Players = new List<PlayerSnapshotPayload>(playerCapacity);
        }
    }
}
