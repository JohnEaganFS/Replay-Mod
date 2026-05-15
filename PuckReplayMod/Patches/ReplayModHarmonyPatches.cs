using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuckReplayMod
{
    [HarmonyPatch(typeof(UIManager), "Awake")]
    public static class UIManagerAwakePatch
    {
        [HarmonyPostfix]
        public static void Postfix(UIManager __instance)
        {
            ReplayModController instance = ReplayModController.Instance;
            if (instance != null)
            {
                instance.OnUiManagerAwake(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(UIMainMenu), "Initialize")]
    public static class UIMainMenuInitializePatch
    {
        [HarmonyPostfix]
        public static void Postfix(UIMainMenu __instance, VisualElement rootVisualElement)
        {
            ReplayModController instance = ReplayModController.Instance;
            if (instance != null)
            {
                instance.OnMainMenuInitialized(__instance, rootVisualElement);
            }
        }
    }

    [HarmonyPatch(typeof(UIPauseMenu), "Initialize")]
    public static class UIPauseMenuInitializePatch
    {
        [HarmonyPostfix]
        public static void Postfix(UIPauseMenu __instance, VisualElement rootVisualElement)
        {
            ReplayModController instance = ReplayModController.Instance;
            if (instance != null)
            {
                instance.OnPauseMenuInitialized(__instance, rootVisualElement);
            }
        }
    }

    [HarmonyPatch(typeof(UIScoreboard), "AddPlayer")]
    public static class ScoreboardAddObserverDuringReplayPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Player player)
        {
            if (!ReplayInputBlocker.ShouldHideObserverFromReplayScoreboard(player))
            {
                return true;
            }

            ReplayGameStatePlaybackService.RemoveLocalNonReplayPlayerFromScoreboard();
            return false;
        }
    }

    [HarmonyPatch(typeof(UIView), "Show")]
    public static class ReplaySelectionViewShowDuringReplayPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(UIView __instance, ref bool __result)
        {
            if (!ReplayInputBlocker.IsPlaybackActive() || !ReplayInputBlocker.IsReplaySelectionView(__instance))
            {
                return true;
            }

            if (__instance != null && __instance.IsVisible)
            {
                __instance.Hide();
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Player), "Client_RequestTeamRpc")]
    public static class PlayerRequestTeamDuringReplayPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Player __instance, PlayerTeam team, RpcParams rpcParams)
        {
            return !ReplayInputBlocker.ShouldBlock(__instance);
        }
    }

    [HarmonyPatch(typeof(Player), "Client_RequestTeamSelectRpc")]
    public static class PlayerRequestTeamSelectDuringReplayPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Player __instance, RpcParams rpcParams)
        {
            return !ReplayInputBlocker.ShouldBlock(__instance);
        }
    }

    [HarmonyPatch(typeof(Player), "Client_RequestPositionSelectRpc")]
    public static class PlayerRequestPositionSelectDuringReplayPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Player __instance, RpcParams rpcParams)
        {
            return !ReplayInputBlocker.ShouldBlock(__instance);
        }
    }

    [HarmonyPatch(typeof(Player), "Client_RequestClaimPositionRpc")]
    public static class PlayerRequestClaimPositionDuringReplayPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Player __instance, NetworkObjectReference playerPositionReference, RpcParams rpcParams)
        {
            return !ReplayInputBlocker.ShouldBlock(__instance);
        }
    }

    [HarmonyPatch(typeof(Player), "Client_RequestHandednessRpc")]
    public static class PlayerRequestHandednessDuringReplayPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Player __instance, PlayerHandedness handedness, RpcParams rpcParams)
        {
            return !ReplayInputBlocker.ShouldBlock(__instance);
        }
    }

    [HarmonyPatch(typeof(PuckManager), "Server_SpawnPucksForPhase")]
    public static class PuckManagerSpawnPucksForPhaseDuringReplayPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(GamePhase phase)
        {
            if (!ReplayInputBlocker.IsPlaybackActive())
            {
                return true;
            }

            ReplayModLog.Info("Blocked normal phase puck spawning during replay playback: " + phase);
            return false;
        }
    }

    [HarmonyPatch(typeof(PuckManager), "Server_SpawnPuck")]
    public static class PuckManagerSpawnPuckDuringReplayPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Vector3 position, Quaternion rotation, bool isReplay, ref Puck __result)
        {
            if (!ReplayInputBlocker.IsPlaybackActive() || isReplay)
            {
                if (ReplayInputBlocker.IsPlaybackActive() && isReplay)
                {
                    ReplayModLog.Info("Allowing replay puck spawn during replay playback.");
                }

                return true;
            }

            __result = null;
            ReplayModLog.Info("Blocked normal puck spawn during replay playback.");
            return false;
        }
    }

    [HarmonyPatch(typeof(Stick), "FixedUpdate")]
    public static class StickFixedUpdateDuringReplayPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Stick __instance)
        {
            return !ReplayInputBlocker.IsReplayStick(__instance);
        }
    }

    [HarmonyPatch(typeof(Player), "Server_SpawnSpectatorCamera")]
    public static class PlayerSpawnSpectatorCameraDuringReplayPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Player __instance, ref Vector3 position, ref Quaternion rotation)
        {
            if (!ReplayInputBlocker.ShouldUseReplaySpectatorCamera(__instance))
            {
                return;
            }

            position = NativeReplayPlaybackService.ReplaySpectatorSpawnPosition;
            rotation = NativeReplayPlaybackService.ReplaySpectatorSpawnRotation;
        }
    }

    internal static class ReplayInputBlocker
    {
        public static bool ShouldBlock(Player player)
        {
            return IsPlaybackActive() &&
                player != null &&
                player.IsLocalPlayer;
        }

        public static bool ShouldUseReplaySpectatorCamera(Player player)
        {
            return IsPlaybackActive() &&
                player != null &&
                player.IsLocalPlayer;
        }

        public static bool ShouldHideObserverFromReplayScoreboard(Player player)
        {
            if (!IsPlaybackActive() || player == null || !player.IsLocalPlayer)
            {
                return false;
            }

            return player.IsReplay == null || !player.IsReplay.Value;
        }

        public static bool IsPlaybackActive()
        {
            ReplayModController instance = ReplayModController.Instance;
            return instance != null &&
                instance.Playback != null &&
                instance.Playback.IsPlaybackActive;
        }

        public static bool IsReplayStick(Stick stick)
        {
            if (stick == null)
            {
                return false;
            }

            Player player = stick.Player;
            return player != null &&
                player.IsReplay != null &&
                player.IsReplay.Value;
        }

        public static bool IsReplaySelectionView(UIView view)
        {
            return view is UITeamSelect || view is UIPositionSelect;
        }
    }
}
