using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>Limits local model fading to the player's actual view, excluding thumbnails and ship monitors.</summary>
public static class LethalCompanyFearViewCamera
{
    internal static bool ShouldFade(Config.SpectatorCameraConfig config) =>
        Features.FearMode.FearModelAppearanceRules.ShouldFade(config.FadeModelsNearby.Value,
            StartOfRound.Instance != null && StartOfRound.Instance.localPlayerController != null
                && StartOfRound.Instance.localPlayerController.isPlayerDead, config.FadeModelsWhileSpectating.Value);

    internal static string DescribeViewer()
    {
        var net = Unity.Netcode.NetworkManager.Singleton;
        var local = StartOfRound.Instance != null ? StartOfRound.Instance.localPlayerController : null;
        var camera = ActiveView;
        return $"viewer={net?.LocalClientId}, host={net?.IsHost}, dead={local?.isPlayerDead}, camera={camera?.name}, mask={camera?.cullingMask:X8}, cameraState={NativeFadePass.CameraBlockReason(camera) ?? "ready"}";
    }

    internal static Camera? ActiveView => StartOfRound.Instance != null ? StartOfRound.Instance.activeCamera : null;
    /// <summary>Checks the confirmed active camera reference.</summary>
    public static bool IsActiveView(Camera camera) => StartOfRound.Instance != null && StartOfRound.Instance.activeCamera == camera;
}
