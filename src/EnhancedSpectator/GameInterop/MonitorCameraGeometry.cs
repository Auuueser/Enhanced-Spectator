using EnhancedSpectator.Features.Spectator;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>Read-only world probes; never adds colliders, cameras, or room components.</summary>
internal static class MonitorCameraGeometry
{
    internal const float Clearance = .3f;

    internal static bool SightClear(Vector3 position, Vector3 focus, int mask) =>
        !Physics.Linecast(position, focus, mask, QueryTriggerInteraction.Ignore);

    internal static bool PathClear(Vector3 from, Vector3 to, int mask)
    {
        Vector3 delta = to - from;
        if (Physics.CheckSphere(to, Clearance, mask, QueryTriggerInteraction.Ignore)) return false;
        return delta.sqrMagnitude < .0001f || !Physics.SphereCast(from, Clearance, delta.normalized,
            out _, delta.magnitude, mask, QueryTriggerInteraction.Ignore);
    }

    internal static bool TryPlace(Vector3 focus, Vector3 candidate, int mask, out Vector3 position)
        => TryPlace(focus,candidate,mask,out position,out _);

    internal static bool TryPlace(Vector3 focus, Vector3 candidate, int mask, out Vector3 position,out string reason)
    {
        position = candidate; reason="accepted";
        Vector3 delta = candidate - focus;
        // Leave travel headroom after acquiring a shot; placing at the reselect limit causes repeated cuts.
        float length = Mathf.Min(delta.magnitude, MonitorCameraRules.PlacementDistance);
        if (length < .75f) { reason="candidate-too-close"; return false; }
        if (Physics.CheckSphere(focus, .12f, mask, QueryTriggerInteraction.Ignore)) { reason="focus-inside-geometry"; return false; }
        Vector3 direction = delta.normalized;
        // Probe from the occupied room, so concave bounds cannot place a camera behind its walls.
        if (Physics.SphereCast(focus, Clearance, direction, out var hit, length, mask, QueryTriggerInteraction.Ignore))
            length = Mathf.Max(0, hit.distance - .15f);
        position = focus + direction * length;
        if(length<.75f) { reason="blocked-near-focus"; return false; }
        if(Physics.CheckSphere(position,Clearance,mask,QueryTriggerInteraction.Ignore)) { reason="station-overlap"; return false; }
        if(!SightClear(position,focus,mask)) { reason="station-sight-blocked"; return false; }
        if(!Physics.Raycast(position,Vector3.down,12f,mask,QueryTriggerInteraction.Ignore)) { reason="station-floor-missing"; return false; }
        return true;
    }
}
