using EnhancedSpectator.Features.Spectator;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>Read-only observers of confirmed V81 RPCs; never modify player input, RPC payloads or target transforms.</summary>
internal static class LethalCompanyFirstPersonPose
{
    private static PlayerControllerB? _target;
    private static readonly FirstPersonLookState Look = new FirstPersonLookState();
    private static int _frame = -1;
    private static bool Watching(PlayerControllerB player) => SpectatorFreecamController.Current is { OwnsCamera: true } controller
        && controller.State.Mode == SpectatorCameraMode.FirstPerson
        && StartOfRound.Instance != null && StartOfRound.Instance.localPlayerController?.spectatedPlayerScript == player;
    private static bool Receiving(PlayerControllerB player) => !player.IsOwner
        && player.__rpc_exec_stage == NetworkBehaviour.__RpcExecStage.Execute && Watching(player);
    private static void EnsureTarget(PlayerControllerB target)
    {
        if (_target == target) return;
        _target = target; _frame = -1;
        Look.Seed(target.gameplayCamera.transform.localEulerAngles.x, target.targetLookRot);
    }
    internal static void ObserveTool(PlayerControllerB player, bool specialAnimation, float timed, bool climbingLadder)
    {
        if (!Receiving(player) || !specialAnimation || timed <= 0 || climbingLadder
            || !(player.currentlyHeldObjectServer is Shovel) || player.gameplayCamera == null) return;
        EnsureTarget(player); Look.BeginTool(Time.unscaledTime, timed);
    }
    internal static void ObserveRotation(PlayerControllerB player, short pitch)
    {
        if (!Receiving(player) || player.gameplayCamera == null) return;
        EnsureTarget(player); Look.Receive(pitch, Time.unscaledTime);
    }
    internal static Quaternion Rotation(PlayerControllerB target)
    {
        var camera = target.gameplayCamera.transform;
        if (!Watching(target) || target.inVehicleAnimation || target.isClimbingLadder || target.jetpackControls)
        { Clear(); return camera.rotation; }
        EnsureTarget(target);
        if (_frame != Time.frameCount) { _frame = Time.frameCount; Look.Advance(Time.unscaledDeltaTime); }
        var local = camera.localEulerAngles; local.x = Look.Pitch;
        return (camera.parent != null ? camera.parent.rotation : Quaternion.identity) * Quaternion.Euler(local);
    }
    internal static void Clear() { _target = null; _frame = -1; }
}
