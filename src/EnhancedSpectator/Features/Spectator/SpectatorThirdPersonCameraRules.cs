using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>
/// Pure third-person camera and representation-pose rules.
/// </summary>
public static class SpectatorThirdPersonCameraRules
{
    /// <summary>Resolves a wheel-adjusted third-person distance.</summary>
    public static float ResolveZoomDistance(
        float currentDistance,
        float scrollDelta,
        float zoomStep,
        float minimumDistance,
        float maximumDistance)
    {
        float safeMinimum = Mathf.Max(0.25f, minimumDistance);
        float safeMaximum = Mathf.Max(safeMinimum, maximumDistance);
        return Mathf.Clamp(
            currentDistance - (scrollDelta * Mathf.Max(0f, zoomStep)),
            safeMinimum,
            safeMaximum);
    }

    /// <summary>
    /// Resolves the desired trailing camera position around a logical ghost/avatar pose.
    /// </summary>
    public static Vector3 ResolveDesiredCameraPosition(
        Vector3 representationPosition,
        Quaternion viewRotation,
        float distance,
        float height)
    {
        float safeDistance = Mathf.Max(0f, distance);
        return representationPosition
            - (viewRotation * Vector3.forward * safeDistance)
            + (Vector3.up * height);
    }

    /// <summary>
    /// Preserves the full spectator look rotation for default ghost-head pitch/yaw feedback.
    /// </summary>
    public static Quaternion ResolveRepresentationRotation(Quaternion cameraRotation)
    {
        return cameraRotation;
    }
}
