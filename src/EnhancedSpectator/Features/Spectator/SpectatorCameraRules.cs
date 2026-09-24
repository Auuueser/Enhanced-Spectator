using System;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>Pure camera input and collision recovery decisions.</summary>
public static class SpectatorCameraRules
{
    /// <summary>Two and a half times the previous 15 metre limit.</summary>
    public const float MaximumFollowDistance = 37.5f;
    /// <summary>0.3.1 release radius 8m, expanded to 2.5 times for target-relative travel.</summary>
    public const float DefaultTravelRadius = 20f;

    /// <summary>0.3.1 entry framing, independent of a previous life or the expanded travel limit.</summary>
    public static float EntryDistance(float radius) => radius > .01f ? Math.Min(4f, radius * .6f) : 0f;

    /// <summary>A repeated active mode key returns to freecam; a disabled remembered mode is re-enabled.</summary>
    public static SpectatorCameraMode ResolveToggleMode(SpectatorCameraMode current, SpectatorCameraMode requested, bool enabled) =>
        enabled && current == requested ? SpectatorCameraMode.Freecam : requested;

    /// <summary>Clamps a wheel-adjusted FOV and rejects non-finite input.</summary>
    public static float AdjustFov(float current, float wheel)
    {
        if (float.IsNaN(current) || float.IsInfinity(current)) current = 66f;
        if (float.IsNaN(wheel) || float.IsInfinity(wheel)) wheel = 0f;
        return Math.Max(30f, Math.Min(110f, current - wheel * 2f));
    }

    /// <summary>Wheel changes distance in every enhanced mode except watched-player first person.</summary>
    public static bool AdjustsDistance(SpectatorCameraMode mode, bool altHeld = false) => mode != SpectatorCameraMode.FirstPerson;

    /// <summary>Wheel routing distinguishes target-relative travel from the self-model camera boom.</summary>
    internal static SpectatorWheelAction WheelAction(bool enhanced, SpectatorCameraMode mode, bool modifier) => !enhanced
        ? SpectatorWheelAction.VanillaDistance : mode switch
        {
            SpectatorCameraMode.FirstPerson => SpectatorWheelAction.None,
            SpectatorCameraMode.Cinematic => SpectatorWheelAction.CinematicDistance,
            SpectatorCameraMode.ThirdPerson when modifier => SpectatorWheelAction.SelfCameraDistance,
            _ => SpectatorWheelAction.TargetDistance
        };

    /// <summary>Half-metre distance steps, respecting a radius smaller than the usual lower bound.</summary>
    public static float AdjustDistance(float current, float wheel, float minimum, float maximum)
    {
        maximum = Math.Max(0f, maximum);
        minimum = Math.Min(Math.Max(0f, minimum), maximum);
        if (float.IsNaN(current) || float.IsInfinity(current)) current = minimum;
        if (float.IsNaN(wheel) || float.IsInfinity(wheel)) wheel = 0f;
        return Math.Max(minimum, Math.Min(maximum, current - wheel * 0.5f));
    }

    /// <summary>Keeps an unobstructed self camera outside the scaled visual without changing the user's distance setting.</summary>
    public static float FramingDistance(float requested, float radius) => Math.Max(requested, Math.Max(0f, radius) + 0.25f);

    /// <summary>Retracts immediately for safety; restores distance gradually after leaving a wall.</summary>
    public static float RecoverDistance(float previous, float safeDistance, float deltaTime)
    {
        if (previous < 0f || safeDistance <= previous) return safeDistance;
        return Math.Min(safeDistance, previous + Math.Max(0f, deltaTime) * 3f);
    }
}

/// <summary>Independent user-controlled distance quantities.</summary>
internal enum SpectatorWheelAction { None, VanillaDistance, TargetDistance, SelfCameraDistance, CinematicDistance }
