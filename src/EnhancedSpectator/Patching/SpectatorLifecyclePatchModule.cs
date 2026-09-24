using System;
using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.Logging;
using EnhancedSpectator.Runtime;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EnhancedSpectator.Patching;

/// <summary>
/// Registers local spectator lifecycle and input-compatibility patches.
/// </summary>
public sealed class SpectatorLifecyclePatchModule : IPatchModule
{
    /// <inheritdoc />
    public void Register(Harmony harmony)
    {
        harmony.CreateClassProcessor(typeof(NativeFadeCameraPatch)).Patch();
        harmony.CreateClassProcessor(typeof(NativeFadeOtherPassPatch)).Patch();
        harmony.CreateClassProcessor(typeof(SpectatorCanvasSubmissionPatch)).Patch();
        harmony.CreateClassProcessor(typeof(SpectatorToolPosePatch)).Patch();
        harmony.CreateClassProcessor(typeof(SpectatorLookPosePatch)).Patch();
        harmony.CreateClassProcessor(typeof(SpectatorCollisionPatch)).Patch();
        harmony.CreateClassProcessor(typeof(PlayerKillPatch)).Patch();
        harmony.CreateClassProcessor(typeof(SpectatedPlayerEffectsPatch)).Patch();
        harmony.CreateClassProcessor(typeof(SwitchCameraPatch)).Patch();
        harmony.CreateClassProcessor(typeof(GameOverSpectateModePatch)).Patch();
        harmony.CreateClassProcessor(typeof(ReviveDeadPlayersPatch)).Patch();
        harmony.CreateClassProcessor(typeof(PlayerConnectedPatch)).Patch();
        harmony.CreateClassProcessor(typeof(SpectateNextPlayerPatch)).Patch();
        harmony.CreateClassProcessor(typeof(InteractPerformedPatch)).Patch();
        harmony.CreateClassProcessor(typeof(ActivateItemPerformedPatch)).Patch();
    }

    /// <inheritdoc />
    public void Unregister(Harmony harmony)
    {
        _ = harmony;
    }

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.RaycastSpectateCameraAroundPivot))]
    private static class SpectatorCollisionPatch
    {
        private static int _nextDiagnosticFrame;
        private static bool Prefix(PlayerControllerB __instance)
        {
            bool owned = SpectatorFreecamController.Current?.OwnsCamera == true
                && IsLocalSpectatorContext(__instance)
                && RuntimeConnectionState.CanRunLocalDiagnostics(out _)
                && StartOfRound.Instance != null && !StartOfRound.Instance.overrideSpectateCamera
                && __instance.isInGameOverAnimation <= 0f;
            if (owned && ModLog.IsDebugEnabled && Time.frameCount >= _nextDiagnosticFrame)
            {
                _nextDiagnosticFrame = Time.frameCount + 300;
                ModLog.Debug("Enhanced camera owns pose; skipped vanilla obstacle camera write.");
            }
            if (!owned && RuntimeConnectionState.CanRunLocalDiagnostics(out _)
                && SpectatorFreecamController.Current?.TryGetVanillaDistance(out float distance) == true
                && GameInterop.LethalCompanyVanillaDistance.TryApply(__instance, distance)) return false;
            return !owned;
        }
    }

    [HarmonyPatch(
        typeof(PlayerControllerB),
        nameof(PlayerControllerB.KillPlayer),
        new Type[] { typeof(Vector3), typeof(bool), typeof(CauseOfDeath), typeof(int), typeof(Vector3), typeof(bool) })]
    private static class PlayerKillPatch
    {
        private static void Postfix(PlayerControllerB __instance)
        {
            if (IsLocalPlayerOrOwner(__instance))
            {
                SpectatorLifecycleEvents.Raise(SpectatorLifecycleEventKind.PlayerDied);
            }
        }
    }

    [HarmonyPatch(
        typeof(PlayerControllerB),
        nameof(PlayerControllerB.SetSpectatedPlayerEffects),
        new Type[] { typeof(bool) })]
    private static class SpectatedPlayerEffectsPatch
    {
        private static void Postfix(PlayerControllerB __instance)
        {
            if (IsLocalSpectatorContext(__instance))
            {
                SpectatorLifecycleEvents.Raise(SpectatorLifecycleEventKind.SpectatedPlayerEffectsApplied);
            }
        }
    }

    [HarmonyPatch(
        typeof(StartOfRound),
        nameof(StartOfRound.SwitchCamera),
        new Type[] { typeof(Camera) })]
    private static class SwitchCameraPatch
    {
        private static void Postfix()
        {
            SpectatorLifecycleEvents.Raise(SpectatorLifecycleEventKind.CameraSwitched);
        }
    }

    [HarmonyPatch(
        typeof(StartOfRound),
        nameof(StartOfRound.SetSpectateCameraToGameOverMode),
        new Type[] { typeof(bool), typeof(PlayerControllerB) })]
    private static class GameOverSpectateModePatch
    {
        private static void Postfix()
        {
            SpectatorLifecycleEvents.Raise(SpectatorLifecycleEventKind.GameOverOverrideChanged);
        }
    }

    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ReviveDeadPlayers))]
    private static class ReviveDeadPlayersPatch
    {
        private static void Postfix()
        {
            SpectatorLifecycleEvents.Raise(SpectatorLifecycleEventKind.Revived);
        }
    }

    [HarmonyPatch(
        typeof(StartOfRound),
        nameof(StartOfRound.OnPlayerConnectedClientRpc),
        new Type[]
        {
            typeof(ulong),
            typeof(int),
            typeof(ulong[]),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(bool)
        })]
    private static class PlayerConnectedPatch
    {
        private static int _nextDebugFrame;

        private static void Postfix()
        {
            RestoreLocalDeadSpectatorStateAfterPlayerJoin();
        }

        private static void RestoreLocalDeadSpectatorStateAfterPlayerJoin()
        {
            if (!RuntimeConnectionState.CanRepairVanillaPlayerState(out string unsafeReason))
            {
                LogDebug($"Skipped local spectator state restore after player connect: {unsafeReason}.");
                return;
            }

            StartOfRound round = StartOfRound.Instance;
            if (round == null)
            {
                return;
            }

            PlayerControllerB localPlayer = round.localPlayerController;
            if (localPlayer == null || !IsLocalPlayerOrOwner(localPlayer))
            {
                return;
            }

            if (!localPlayer.isPlayerDead || !localPlayer.isPlayerControlled)
            {
                return;
            }

            // Vanilla OnPlayerConnectedClientRpc can mark already-dead local players as controlled
            // when another client joins mid-round. Restore only the local dead spectator flags here
            // instead of patching broad per-frame player update logic.
            localPlayer.isPlayerControlled = false;
            Camera spectateCamera = round.spectateCamera;
            if (spectateCamera != null && round.activeCamera != spectateCamera)
            {
                round.SwitchCamera(spectateCamera);
            }

            LogDebug("Restored local dead spectator state after vanilla player-connect sync.");
        }

        private static void LogDebug(string message)
        {
            if (Time.frameCount < _nextDebugFrame)
            {
                return;
            }

            _nextDebugFrame = Time.frameCount + 120;
            ModLog.Debug(message);
        }
    }

    [HarmonyPatch(
        typeof(PlayerControllerB),
        nameof(PlayerControllerB.SpectateNextPlayer),
        new Type[] { typeof(bool) })]
    private static class SpectateNextPlayerPatch
    {
        private static int _nextSuppressDebugFrame;
        private static int _nextUnsafeDebugFrame;

        private static bool Prefix(PlayerControllerB __instance)
        {
            if (!TryGetLocalDeadSpectatorContext(__instance, out string contextReason))
            {
                if (contextReason.Length > 0 && Time.frameCount >= _nextUnsafeDebugFrame)
                {
                    _nextUnsafeDebugFrame = Time.frameCount + 120;
                    ModLog.Debug($"Vanilla spectator target switch suppression skipped: {contextReason}.");
                }

                return true;
            }

            if (RuntimeConnectionState.ShouldSkipVanillaSpectatorTargetSwitch(out string unsafeReason))
            {
                if (Time.frameCount >= _nextUnsafeDebugFrame)
                {
                    _nextUnsafeDebugFrame = Time.frameCount + 120;
                    ModLog.Debug($"Skipped vanilla spectator target switch during unsafe lifecycle: {unsafeReason}.");
                }

                // During network shutdown vanilla can continue one or two spectator LateUpdate calls
                // after player arrays are partially torn down. Skipping this narrow input path avoids
                // teardown-time NullReferenceException without changing normal spectator behavior.
                return false;
            }

            if (!TryGetLocalSpectatorContext(__instance, out string skipReason))
            {
                if (skipReason.Length > 0 && Time.frameCount >= _nextUnsafeDebugFrame)
                {
                    _nextUnsafeDebugFrame = Time.frameCount + 120;
                    ModLog.Debug($"Vanilla spectator target switch suppression skipped: {skipReason}.");
                }

                return true;
            }

            if (SpectatorVanillaInputGuard.ShouldSuppressTargetSwitchInput(out string suppressionReason))
            {
                if (Time.frameCount >= _nextSuppressDebugFrame)
                {
                    _nextSuppressDebugFrame = Time.frameCount + 120;
                    ModLog.Debug(
                        "Suppressed vanilla spectator target switch while "
                        + (suppressionReason.Length > 0 ? suppressionReason : "quick menu is open")
                        + ".");
                }

                return false;
            }

            return true;
        }

        private static bool TryGetLocalDeadSpectatorContext(PlayerControllerB player, out string reason)
        {
            reason = string.Empty;
            if (player == null)
            {
                reason = "patch instance unavailable";
                return false;
            }

            StartOfRound round = StartOfRound.Instance;
            if (round == null)
            {
                reason = "round unavailable";
                return false;
            }

            PlayerControllerB localPlayer = round.localPlayerController;
            if (localPlayer == null)
            {
                reason = "local player unavailable";
                return false;
            }

            if (!IsLocalPlayerOrOwner(player, localPlayer))
            {
                return false;
            }

            if (!localPlayer.isPlayerDead)
            {
                return false;
            }

            return true;
        }

        private static bool TryGetLocalSpectatorContext(PlayerControllerB player, out string reason)
        {
            reason = string.Empty;
            if (player == null)
            {
                reason = "patch instance unavailable";
                return false;
            }

            StartOfRound round = StartOfRound.Instance;
            if (round == null)
            {
                reason = "round unavailable";
                return false;
            }

            PlayerControllerB localPlayer = round.localPlayerController;
            if (localPlayer == null)
            {
                reason = "local player unavailable";
                return false;
            }

            if (!IsLocalPlayerOrOwner(player, localPlayer))
            {
                return false;
            }

            if (!localPlayer.isPlayerDead)
            {
                return false;
            }

            if (round.spectateCamera == null || localPlayer.spectatedPlayerScript == null)
            {
                reason = "spectator camera or target unavailable";
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(
        typeof(PlayerControllerB),
        nameof(PlayerControllerB.Interact_performed),
        new Type[] { typeof(InputAction.CallbackContext) })]
    private static class InteractPerformedPatch
    {
        private static int _nextSuppressDebugFrame;

        private static bool Prefix(PlayerControllerB __instance)
        {
            if (!ShouldSuppressQuickMenuGameplayInput(__instance, out string reason))
            {
                return true;
            }

            LogQuickMenuGameplaySuppression("interact", reason, ref _nextSuppressDebugFrame);
            return false;
        }
    }

    [HarmonyPatch(
        typeof(PlayerControllerB),
        nameof(PlayerControllerB.ActivateItem_performed),
        new Type[] { typeof(InputAction.CallbackContext) })]
    private static class ActivateItemPerformedPatch
    {
        private static int _nextSuppressDebugFrame;

        private static bool Prefix(PlayerControllerB __instance)
        {
            if (!ShouldSuppressQuickMenuGameplayInput(__instance, out string reason))
            {
                return true;
            }

            LogQuickMenuGameplaySuppression("activate item", reason, ref _nextSuppressDebugFrame);
            return false;
        }
    }

    private static bool IsLocalSpectatorContext(PlayerControllerB player)
    {
        if (IsLocalPlayerOrOwner(player))
        {
            return true;
        }

        PlayerControllerB? localPlayer = GetLocalPlayer();
        return localPlayer != null
            && localPlayer.isPlayerDead
            && localPlayer.spectatedPlayerScript == player;
    }

    private static bool IsLocalPlayerOrOwner(PlayerControllerB player)
    {
        if (player == null)
        {
            return false;
        }

        PlayerControllerB? localPlayer = GetLocalPlayer();
        if (localPlayer == player)
        {
            return true;
        }

        return player.IsOwner || player.IsLocalPlayer;
    }

    private static bool IsLocalPlayerOrOwner(PlayerControllerB player, PlayerControllerB localPlayer)
    {
        if (player == null)
        {
            return false;
        }

        if (localPlayer == player)
        {
            return true;
        }

        return player.IsOwner || player.IsLocalPlayer;
    }

    private static PlayerControllerB? GetLocalPlayer()
    {
        StartOfRound round = StartOfRound.Instance;
        return round != null ? round.localPlayerController : null;
    }

    private static bool ShouldSuppressQuickMenuGameplayInput(PlayerControllerB player, out string reason)
    {
        reason = string.Empty;
        if (!SpectatorVanillaInputGuard.ShouldSuppressGameplayInteractInput() && !IsLocalQuickMenuOpen())
        {
            return false;
        }

        PlayerControllerB? localPlayer = GetLocalPlayer();
        if (localPlayer == null)
        {
            reason = "local player unavailable";
            return false;
        }

        if (!IsLocalPlayerOrOwner(player, localPlayer))
        {
            return false;
        }

        reason = "quick menu is open";
        return true;
    }

    private static bool IsLocalQuickMenuOpen()
    {
        PlayerControllerB? localPlayer = GetLocalPlayer();
        return localPlayer != null
            && localPlayer.quickMenuManager != null
            && localPlayer.quickMenuManager.isMenuOpen;
    }

    private static void LogQuickMenuGameplaySuppression(
        string inputName,
        string reason,
        ref int nextSuppressDebugFrame)
    {
        if (Time.frameCount < nextSuppressDebugFrame)
        {
            return;
        }

        nextSuppressDebugFrame = Time.frameCount + 120;
        ModLog.Debug($"Suppressed local {inputName} input while {reason}.");
    }
}
