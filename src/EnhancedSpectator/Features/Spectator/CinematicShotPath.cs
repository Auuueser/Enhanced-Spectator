using System;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>A continuous sequence of low arcs, tracking, crane reveals and dolly approaches.</summary>
internal static class CinematicShotPath
{
    internal readonly struct Shot
    {
        internal readonly float Yaw, Radius, Height, FocusHeight, Composition;
        internal Shot(float yaw, float radius, float height, float focus, float composition)
        { Yaw = yaw; Radius = radius; Height = height; FocusHeight = focus; Composition = composition; }
    }
    // Smooth periodic splines connect intentional shot compositions, with no timed cuts.
    private static readonly float[] Yaws = { 0, 22, 72, 130, 184, 224, 282, 334 };
    private static readonly float[] Radii = { 1f, .78f, .84f, 1.18f, 1.25f, .93f, .76f, 1.05f };
    private static readonly float[] Heights = { .55f, .42f, 1.05f, 2.8f, 3.4f, 1.8f, .58f, 1.1f };
    private static readonly float[] Focus = { 1.15f, 1.28f, 1.15f, 1f, .95f, 1.15f, 1.3f, 1.15f };
    private static readonly float[] Composition = { -.10f, .04f, .12f, .04f, -.10f, -.12f, -.04f, .08f };
    internal static double Advance(double phase, float speed, float elapsed)
    {
        if (double.IsNaN(phase) || double.IsInfinity(phase)) phase = 0;
        if (float.IsNaN(speed) || float.IsInfinity(speed)) speed = 8;
        if (float.IsNaN(elapsed) || float.IsInfinity(elapsed) || elapsed <= 0) return phase;
        return (phase + Math.Max(0, Math.Min(30, speed)) * Math.Min(.1, elapsed)) % 360;
    }
    internal static Shot Sample(double phase)
    {
        if (double.IsNaN(phase) || double.IsInfinity(phase)) phase = 0;
        float p = (float)((phase % 360 + 360) % 360 / 45);
        int index = (int)p; float t = p - index;
        return new Shot(Interpolate(Yaws, index, t, true), Interpolate(Radii, index, t),
            Interpolate(Heights, index, t), Interpolate(Focus, index, t), Interpolate(Composition, index, t));
    }
    private static float Interpolate(float[] values, int index, float t, bool angle = false)
    {
        float At(int n) => values[(n + 8) % 8] + (angle ? n < 0 ? -360 : n >= 8 ? 360 : 0 : 0);
        float a = At(index - 1), b = At(index), c = At(index + 1), d = At(index + 2);
        return .5f * (2*b + (-a+c)*t + (2*a-5*b+4*c-d)*t*t + (-a+3*b-3*c+d)*t*t*t);
    }
}
