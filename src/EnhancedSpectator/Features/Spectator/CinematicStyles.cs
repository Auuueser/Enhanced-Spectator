using System;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>Stable style IDs and continuous paths; no game state, FOV changes or random cuts.</summary>
internal static class CinematicStyles
{
    internal const int Count = 12;
    internal const float BlendSeconds = 2f;
    private static readonly string[] Chinese = { "经典长镜头", "英雄环绕", "平行追踪", "正面引领", "肩后追随", "缓推特写", "升降揭示", "高位巡航", "低位掠行", "弧线揭幕", "闪灵跟随", "居中跟随" };
    private static readonly string[] English = { "Classic sequence", "Hero orbit", "Lateral tracking", "Leading shot", "Shoulder pursuit", "Dolly intimacy", "Crane reveal", "High orbit", "Low glide", "Arc reveal", "Shining follow", "Centered follow" };
    internal static int Valid(int style) => style >= 0 && style < Count ? style : 0;
    internal static readonly int[] Order = { 10, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 11 };
    internal static int Cycle(int style, int direction) => Order[(Array.IndexOf(Order, Valid(style)) + Math.Sign(direction) + Count) % Count];
    internal static string Name(int style, bool chinese) => (chinese ? Chinese : English)[Valid(style)];
    internal static bool FollowsHeading(int style) => style == 2 || style == 3 || style == 4 || style == 8 || style == 10 || style == 11;
    internal static bool IsStyleKey(KeyCode key) => key == KeyCode.LeftArrow || key == KeyCode.RightArrow;
    internal static bool ReservesKey(SpectatorCameraMode? mode, KeyCode key) => (mode == SpectatorCameraMode.Cinematic || mode == SpectatorCameraMode.Monitor || mode == SpectatorCameraMode.Director) && IsStyleKey(key);
    internal static int InputDirection(bool left, bool right) => left == right ? 0 : right ? 1 : -1;

    internal static CinematicShotPath.Shot Sample(int style, double phase)
    {
        if (double.IsNaN(phase) || double.IsInfinity(phase)) phase = 0;
        double p = (phase % 360 + 360) % 360;
        float s = (float)Math.Sin(p * Math.PI / 180), c = (float)Math.Cos(p * Math.PI / 180);
        float lift = (1 - c) * .5f;
        // Distinct camera rigs: circle, parallel rail, front/rear tracking, dolly, crane,
        // elevated circle, low stabilizer and an offset rail. Each loop has continuous velocity.
        return Valid(style) switch
        {
            1 => new CinematicShotPath.Shot((float)p, .8f + .2f*c, .35f + 1.5f*lift, 1.35f, .18f*s),
            2 => Rail(.9f, .85f*s, 1.15f + .35f*lift, 1.3f, -.26f),
            3 => Rail(.45f*s, .95f + .3f*c, 1.2f, 1.35f, .22f),
            4 => Rail(.4f + .18f*s, -.8f - .18f*c, 1.65f, 1.3f, -.25f),
            5 => Rail(.2f*s, .5f + .9f*(1-lift), 1.15f, 1.4f, .12f*s),
            6 => new CinematicShotPath.Shot(-70 + 140*lift, .65f + .65f*lift, .4f + 5.5f*lift, 1.05f, .24f*s),
            7 => new CinematicShotPath.Shot(-(float)p, 1.1f + .25f*c, 4.5f + 1.5f*s, .85f, .23f),
            8 => Rail(.9f*s, -.8f, .32f + .12f*c, .8f, .25f*s),
            10 => Rail(0, -1f, 1.3f, 1.3f, 0),
            11 => Rail(0, -1f, 1.3f, 1.3f, 0),
            9 => Rail(1.3f*s, .6f, .65f + 1.4f*lift, 1.2f, -.26f*s),
            _ => ActionSequence(phase)
        };
    }

    internal static Vector3 RearStation(Vector3 body, Vector3 forward, float distance, float height)
    {
        forward.y = 0;
        if (forward.sqrMagnitude < .0001f) forward = Vector3.forward;
        return body - forward.normalized * distance + Vector3.up * height;
    }

    private static CinematicShotPath.Shot ActionSequence(double phase)
    {
        var shot = CinematicShotPath.Sample(phase);
        return new CinematicShotPath.Shot(shot.Yaw, shot.Radius, .3f + shot.Height * 1.25f,
            shot.FocusHeight, shot.Composition * 1.6f);
    }

    private static CinematicShotPath.Shot Rail(float x, float z, float height, float focus, float composition) =>
        new CinematicShotPath.Shot((float)(Math.Atan2(x, z)*180/Math.PI), (float)Math.Sqrt(x*x+z*z), height, focus, composition);

    internal static float AngleDelta(float from, float to) => ((to - from) % 360 + 540) % 360 - 180;

    internal static float FollowHeading(float current, float target, float elapsed, bool action = false)
    {
        float dt = Math.Max(0, Math.Min(.1f, elapsed));
        float step = AngleDelta(current, target) * (1 - (float)Math.Exp(-dt / (action ? .35f : .9f)));
        float rate = action ? 120f : 45f;
        return current + Math.Max(-rate*dt, Math.Min(rate*dt, step));
    }

    internal static CinematicShotPath.Shot WithYaw(CinematicShotPath.Shot shot, float yaw) =>
        new CinematicShotPath.Shot(yaw, shot.Radius, shot.Height, shot.FocusHeight, shot.Composition);

    internal static CinematicShotPath.Shot Blend(CinematicShotPath.Shot from, CinematicShotPath.Shot to, float elapsed)
    {
        float t = Math.Max(0, Math.Min(1, elapsed / BlendSeconds));
        // Quintic easing has zero velocity and acceleration at both ends. Interpolate
        // polar coordinates, not a chord through the watched player's body.
        t = t*t*t*(t*(6*t-15)+10);
        float Mix(float a, float b) => a + (b-a)*t;
        // Caller unwraps the destination continuously so it cannot reverse across 180 degrees mid-blend.
        return new CinematicShotPath.Shot(Mix(from.Yaw, to.Yaw),
            Mix(from.Radius,to.Radius), Mix(from.Height,to.Height), Mix(from.FocusHeight,to.FocusHeight), Mix(from.Composition,to.Composition));
    }
}
