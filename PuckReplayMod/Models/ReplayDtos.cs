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
        public int FormatVersion = ReplayModConstants.ReplayDtoFormatVersion;
        public string ModVersion = ReplayModConstants.ModVersion;
        public string GameVersion = ReplayModConstants.TargetGameVersion;
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
    public class Vector2Dto
    {
        public float X;
        public float Y;

        public static Vector2Dto From(Vector2 value)
        {
            return new Vector2Dto
            {
                X = value.x,
                Y = value.y
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
        public List<PlayerInputPayload> PlayerInputs = new List<PlayerInputPayload>();

        public TransformFramePayload()
        {
        }

        public TransformFramePayload(int playerBodyCapacity, int stickCapacity, int puckCapacity)
        {
            this.PlayerBodies = new List<PlayerBodyTransformPayload>(playerBodyCapacity);
            this.Sticks = new List<StickTransformPayload>(stickCapacity);
            this.Pucks = new List<PuckTransformPayload>(puckCapacity);
            this.PlayerInputs = new List<PlayerInputPayload>(playerBodyCapacity);
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
    public class PlayerInputPayload
    {
        public ulong OwnerClientId;
        public Vector2Dto LookAngleInput;
        public sbyte BladeAngleInput;
        public bool TrackInput;
        public bool LookInput;
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
        public string Handedness;
        public string PositionName;
        public bool IsMuted;
        public PlayerCustomizationPayload Customization;
    }

    [Serializable]
    public class PlayerCustomizationPayload
    {
        public int FlagID;
        public int HeadgearIDBlueAttacker;
        public int HeadgearIDRedAttacker;
        public int HeadgearIDBlueGoalie;
        public int HeadgearIDRedGoalie;
        public int MustacheID;
        public int BeardID;
        public int JerseyIDBlueAttacker;
        public int JerseyIDRedAttacker;
        public int JerseyIDBlueGoalie;
        public int JerseyIDRedGoalie;
        public int StickSkinIDBlueAttacker;
        public int StickSkinIDRedAttacker;
        public int StickSkinIDBlueGoalie;
        public int StickSkinIDRedGoalie;
        public int StickShaftTapeIDBlueAttacker;
        public int StickShaftTapeIDRedAttacker;
        public int StickShaftTapeIDBlueGoalie;
        public int StickShaftTapeIDRedGoalie;
        public int StickBladeTapeIDBlueAttacker;
        public int StickBladeTapeIDRedAttacker;
        public int StickBladeTapeIDBlueGoalie;
        public int StickBladeTapeIDRedGoalie;
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
        public List<BodyLifecyclePayload> PlayerBodies = new List<BodyLifecyclePayload>();
        public List<PuckSnapshotPayload> Pucks = new List<PuckSnapshotPayload>();
        public List<StickSnapshotPayload> Sticks = new List<StickSnapshotPayload>();
        public List<PlayerInputPayload> PlayerInputs = new List<PlayerInputPayload>();

        public InitialSnapshotPayload()
        {
        }

        public InitialSnapshotPayload(int playerCapacity, int puckCapacity, int stickCapacity)
        {
            this.Players = new List<PlayerSnapshotPayload>(playerCapacity);
            this.PlayerBodies = new List<BodyLifecyclePayload>(playerCapacity);
            this.Pucks = new List<PuckSnapshotPayload>(puckCapacity);
            this.Sticks = new List<StickSnapshotPayload>(stickCapacity);
            this.PlayerInputs = new List<PlayerInputPayload>(playerCapacity);
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
        public PlayerSnapshotPayload Player;
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
