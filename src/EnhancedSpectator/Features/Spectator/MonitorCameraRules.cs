using System;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>Geometry-independent framing and hysteresis for room-mounted spectator shots.</summary>
internal static class MonitorCameraRules
{
    internal const int CandidateCount = 8;
    internal const float ProbeInterval = .2f;
    internal const float RoomDelay = .6f;
    internal const float OcclusionDelay = .35f;
    internal const float MaximumDistance = 18f;
    internal const float PlacementDistance = 12f;

    internal static float Blend(float dt, float seconds) =>
        float.IsNaN(dt) || dt <= 0 ? 0 : 1f - (float)Math.Exp(-Math.Min(dt, .1f) / seconds);

    // Room-local coordinates preserve rotated tiles. Corners precede wall midpoints.
    internal static Vector3 Candidate(Bounds bounds, Vector3 localFocus, int index)
    {
        Vector3 min = bounds.min, max = bounds.max;
        float insetX = Math.Min(.65f, bounds.size.x * .2f);
        float insetZ = Math.Min(.65f, bounds.size.z * .2f);
        float x0 = min.x + insetX, x1 = max.x - insetX;
        float z0 = min.z + insetZ, z1 = max.z - insetZ;
        float y = Clamp(localFocus.y + 2f, min.y + .5f, Math.Max(min.y + .5f, max.y - .5f));
        return index switch
        {
            0 => new Vector3(x0, y, z0), 1 => new Vector3(x1, y, z0),
            2 => new Vector3(x1, y, z1), 3 => new Vector3(x0, y, z1),
            4 => new Vector3(Clamp(localFocus.x, x0, x1), y, z0),
            5 => new Vector3(x1, y, Clamp(localFocus.z, z0, z1)),
            6 => new Vector3(Clamp(localFocus.x, x0, x1), y, z1),
            _ => new Vector3(x0, y, Clamp(localFocus.z, z0, z1))
        };
    }

    internal static Vector3 Slide(Vector3 station, Vector3 originFocus, Vector3 focus)
    {
        Vector3 delta = (focus - originFocus) * .16f;
        delta.y = 0;
        float length = delta.magnitude;
        if (length > .8f) delta *= .8f / length;
        return station + delta;
    }

    internal static bool NeedsNewShot(bool hasShot, bool changedRoom, float blockedSeconds, float distance, float heightChange) =>
        !hasShot || changedRoom || blockedSeconds >= OcclusionDelay || distance < .65f
        || distance > MaximumDistance - 1f;

    internal static float Score(Vector3 position, Vector3 focus, Vector3 oldPosition, bool hasOld, int index)
    {
        float distance = (position - focus).magnitude;
        if (distance < .75f || distance > MaximumDistance) return float.NegativeInfinity;
        float elevation = (position.y - focus.y) / distance;
        // Medium-wide diagonal composition, with a small continuity preference. Never chase tiny score changes.
        return 5f - Math.Abs(distance - 7f) * .18f - Math.Abs(elevation - .35f) * 2f
            + (index < 4 ? .25f : 0) - (hasOld ? Math.Min(1f, (position - oldPosition).magnitude * .04f) : 0);
    }

    private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
}

/// <summary>Subject-led pacing, separate from collision stalls and route completion.</summary>
internal sealed class MonitorSubjectPacing
{
    private bool _hasSample, _movingObserved, _waiting, _engaged;
    private Vector3 _last;
    private float _speed, _stillTime, _factor=1;
    internal bool Waiting => _waiting;
    internal float Speed => _speed;
    internal bool Engaged => _engaged && _movingObserved;
    internal void Clear() { _hasSample=_movingObserved=_waiting=_engaged=false; _speed=_stillTime=0; _factor=1; }
    internal void Sample(Vector3 focus,float dt)
    {
        if(_hasSample && dt>.0001f)
        {
            float displacement=(focus-_last).magnitude;
            // Teleports do not count as a walking/stopping gesture.
            float speed=displacement>2 ? 0 : displacement/dt;
            _speed=Mathf.Lerp(_speed,speed,MonitorCameraRules.Blend(dt,speed<_speed ? .07f : .12f));
            if(speed>.6f && displacement<=2) _movingObserved=true;
            _stillTime=_speed<.35f ? _stillTime+dt : 0;
        }
        _last=focus; _hasSample=true;
    }
    internal float Advance(float distance,bool visible,bool safe,float dt)
    {
        // Hysteresis avoids accelerating again when a stopping camera drifts across one radius.
        _engaged=visible && safe && distance>=1.2f && distance<=(_engaged ? 15f : 12f);
        bool comfortable=_engaged;
        if(!comfortable || _speed>.65f) _waiting=false;
        else if(_movingObserved && _stillTime>=.18f) _waiting=true;
        float desired=!comfortable || !_movingObserved ? 1 : _waiting ? 0 : Mathf.Clamp(_speed/3f,.12f,1);
        _factor=Mathf.MoveTowards(_factor,desired,dt/(desired<_factor ? .28f : .16f));
        return _factor;
    }
}
