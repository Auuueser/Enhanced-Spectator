using System;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>Room-relative rigs driven by player position, never by an autonomous orbit timer.</summary>
internal static class MonitorCameraStyles
{
    internal const int Count = 4;
    internal static int Valid(int style) => style >= 0 && style < Count ? style : 0;
    // Keep the dormant rigs and persisted IDs 1 (Veronica) and 3 (tracking) for later work; no user entry activates them.
    internal static int Available(int style) => Valid(style) switch { 1 => 0, 3 => 2, var valid => valid };
    internal static readonly int[] Order = { 2, 0 };
    internal static bool IsContinuous(int style) => style == 2 || style == 3;
    internal static int Cycle(int style, int direction) => Order[(Array.IndexOf(Order,Available(style)) + Math.Sign(direction) + Order.Length) % Order.Length];
    internal static string Name(int style, bool chinese) => Available(style) switch
    {
        2 => chinese ? "穿梭运镜" : "Travelling camera",
        3 => chinese ? "追踪运镜" : "Tracking camera",
        _ => chinese ? "监视器" : "Monitor"
    };

    internal static Vector3 Candidate(Bounds room, Vector3 focus, int index, int style)
    {
        int tier = index / MonitorCameraRules.CandidateCount;
        Bounds framing = Valid(style) == 1 ? FramingBounds(room, focus) : room;
        Vector3 p = MonitorCameraRules.Candidate(framing, focus, index % MonitorCameraRules.CandidateCount);
        float lift = tier == 1 ? .8f : tier == 2 ? 3.2f : Valid(style) == 1 ? 1.2f : 2f;
        p.y = Math.Max(room.min.y + .35f, Math.Min(room.max.y - .35f, focus.y + lift));
        // Alternate heights and shorter wall-side stations rescue low ceilings and cluttered corners.
        if (tier == 2) { p.x = focus.x + (p.x - focus.x) * .55f; p.z = focus.z + (p.z - focus.z) * .55f; }
        return p;
    }

    private static int Zone(Bounds room, Vector3 focus)
    {
        bool x = room.size.x >= room.size.z;
        float length = x ? room.size.x : room.size.z;
        return length < 12f ? 0 : Math.Max(0, Math.Min(2,
            (int)Math.Floor(((x ? focus.x - room.min.x : focus.z - room.min.z) / length) * 3f)));
    }

    private static Bounds FramingBounds(Bounds room, Vector3 focus)
    {
        bool x = room.size.x >= room.size.z;
        float length = x ? room.size.x : room.size.z;
        if (length < 12f) return room;
        Vector3 size = room.size, center = room.center;
        if (x) { size.x = length / 3f; center.x = room.min.x + size.x * (Zone(room, focus) + .5f); }
        else { size.z = length / 3f; center.z = room.min.z + size.z * (Zone(room, focus) + .5f); }
        return new Bounds(center, size);
    }

    internal static bool CrossedZone(Bounds room, Vector3 origin, Vector3 focus)
    {
        int oldZone = Zone(room, origin), nextZone = Zone(room, focus);
        if (oldZone == nextZone) return false;
        Bounds framing = FramingBounds(room, focus);
        // Enter the new zone by 0.6m before cutting; door/zone boundary jitter cannot alternate shots.
        bool x = room.size.x >= room.size.z;
        float value = x ? focus.x : focus.z;
        return nextZone > oldZone ? value > (x ? framing.min.x : framing.min.z) + .6f
            : value < (x ? framing.max.x : framing.max.z) - .6f;
    }

    internal static Vector3 Rail(Bounds room, Vector3 station, Vector3 origin, Vector3 focus)
    {
        bool alongX = room.size.x >= room.size.z;
        float limit=Math.Min(3f,Math.Max(.25f,(alongX?room.size.x:room.size.z)*.15f));
        float travel = Math.Max(-limit, Math.Min(limit, (alongX ? focus.x - origin.x : focus.z - origin.z) * .65f));
        Vector3 p = station;
        if (alongX) p.x += travel; else p.z += travel;
        p.x = Math.Max(room.min.x + .35f, Math.Min(room.max.x - .35f, p.x));
        p.z = Math.Max(room.min.z + .35f, Math.Min(room.max.z - .35f, p.z));
        return p;
    }

    internal static Vector3 LookPoint(int style, Vector3 focus, Vector3 stationFocus)
    {
        if (Valid(style) == 0) return focus;
        // A little space ahead of travel; bounded so the player stays in frame. No roll or FOV override.
        Vector3 lead = focus - stationFocus; lead.y = 0;
        float length = lead.magnitude;
        return focus + (length > .001f ? lead * (Math.Min(.55f, length * .12f) / length) : Vector3.zero);
    }
}
