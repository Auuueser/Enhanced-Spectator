using System;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>Stable style IDs and continuous paths; no game state, FOV changes or random cuts.</summary>
internal static class CinematicStyles
{
    internal const int Count = 10;
    internal const float BlendSeconds = 2f;
    private static readonly string[] Chinese = { "经典长镜头", "英雄环绕", "平行追踪", "正面引领", "肩后追随", "缓推特写", "升降揭示", "高位巡航", "低位掠行", "弧线揭幕" };
    private static readonly string[] English = { "Classic sequence", "Hero orbit", "Lateral tracking", "Leading shot", "Shoulder pursuit", "Dolly intimacy", "Crane reveal", "High orbit", "Low glide", "Arc reveal" };
    internal static int Valid(int style) => style >= 0 && style < Count ? style : 0;
    internal static int Cycle(int style, int direction) => (Valid(style) + Math.Sign(direction) + Count) % Count;
    internal static string Name(int style, bool chinese) => (chinese ? Chinese : English)[Valid(style)];
    internal static bool FollowsHeading(int style) => style == 2 || style == 3 || style == 4 || style == 8;
    internal static bool IsStyleKey(KeyCode key) => key == KeyCode.LeftArrow || key == KeyCode.RightArrow;
    internal static bool ReservesKey(SpectatorCameraMode? mode, KeyCode key) => mode == SpectatorCameraMode.Cinematic && IsStyleKey(key);
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
            1 => new CinematicShotPath.Shot((float)p, .95f + .07f*c, .48f + .24f*lift, 1.35f, 0),
            2 => Rail(1f, .35f*s, 1.3f, 1.3f, -.16f),
            3 => Rail(.18f*s, 1.05f + .12f*c, 1.5f, 1.35f, .10f),
            4 => Rail(.35f + .05f*s, -.88f - .08f*c, 1.7f, 1.3f, -.13f),
            5 => Rail(.06f*s, .65f + .65f*(1-lift), 1.25f, 1.4f, .03f),
            6 => new CinematicShotPath.Shot(-30 + 60*lift, .85f + .35f*lift, .55f + 4.2f*lift, 1.05f, .10f*s),
            7 => new CinematicShotPath.Shot(-(float)p, 1.25f, 5f + .5f*s, .85f, .10f),
            8 => Rail(.6f*s, -.95f, .30f + .08f*c, .8f, .14f*s),
            9 => Rail(.95f*s, .8f, .95f + .45f*lift, 1.2f, -.12f*s),
            _ => CinematicShotPath.Sample(phase)
        };
    }

    private static CinematicShotPath.Shot Rail(float x, float z, float height, float focus, float composition) =>
        new CinematicShotPath.Shot((float)(Math.Atan2(x, z)*180/Math.PI), (float)Math.Sqrt(x*x+z*z), height, focus, composition);

    internal static float AngleDelta(float from, float to) => ((to - from) % 360 + 540) % 360 - 180;

    internal static float FollowHeading(float current, float target, float elapsed)
    {
        float dt = Math.Max(0, Math.Min(.1f, elapsed));
        float step = AngleDelta(current, target) * (1 - (float)Math.Exp(-dt / .9f));
        return current + Math.Max(-45*dt, Math.Min(45*dt, step));
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
