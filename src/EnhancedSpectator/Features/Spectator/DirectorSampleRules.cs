using System;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

internal static class DirectorSampleRules
{
    internal const int Count = 4;
    internal static int Valid(int value) => value >= 0 && value < Count ? value : 0;
    internal static int Cycle(int value, int direction) => (Valid(value) + Math.Sign(direction) + Count) % Count;
    internal static string Name(int value, bool cn) => (cn ? Chinese : English)[Valid(value)];
    private static readonly string[] Chinese = { "走廊尽头", "纵身一跃", "双人反应", "滑动变焦" };
    private static readonly string[] English = { "Corridor suspense", "Floor handoff", "Two-person reaction", "Dolly zoom" };
    internal static float Ease(float time, float duration)
    { float t = Math.Max(0, Math.Min(1, time / duration)); return t*t*(3-2*t); }
    internal static float DollyFov(float distance) => (float)(2*Math.Atan(4*Math.Tan(Math.PI/6)/Math.Max(1.8f,distance))*180/Math.PI);
    internal static bool FallStarted(float drop, float velocity) => drop > .6f && velocity < -1f;
    internal static float Fov(int sample, float time, float distance, bool wide) => sample switch
    { 0 => 60-8*Ease(time,6), 1 => wide ? 66 : 58, 2 => wide ? 62 : 50, _ => DollyFov(distance) };
    internal static string FilterName(int value, bool cn) => (cn ? FiltersChinese : FiltersEnglish)[Math.Max(0,Math.Min(3,value))];
    private static readonly string[] FiltersChinese = { "原生", "冷调悬疑", "自然电影", "设施录像" };
    private static readonly string[] FiltersEnglish = { "Original", "Cool suspense", "Natural cinema", "Facility recording" };
}
