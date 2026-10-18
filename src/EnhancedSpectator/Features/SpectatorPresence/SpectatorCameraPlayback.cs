using System;
using System.Collections.Generic;
using UnityEngine;

namespace EnhancedSpectator.Features.SpectatorPresence;

/// <summary>Plays completed camera samples on a short delayed timeline instead of chasing each new packet.</summary>
internal sealed class SpectatorCameraPlayback
{
    // Two to three samples of the 30 Hz follow stream. A follower's camera is played relative to the watched player
    // (moved by them here), so a late packet only holds the leader's own camera moves for a moment.
    internal const float Delay = .08f;
    private const int Capacity = 16;
    private readonly List<Frame> _frames = new List<Frame>(Capacity);
    private ulong _source;
    private long _originStamp, _lastStamp;
    private double _originTime;
    private SpectatorCameraFrame _kind;
    private bool _cachedOrigin;
    internal bool Ready => _frames.Count != 0;
    internal int Count => _frames.Count;

    internal void Clear() => _frames.Clear();

    internal (Vector3 Position, Quaternion Rotation, float FieldOfView) Update(SpectatorCameraSample sample, double now,
        Vector3 initialPosition, Quaternion initialRotation, float initialFov)
    {
        if (!Ready || _source != sample.Source || _kind != sample.Frame
            || _cachedOrigin && sample.Timestamp > _lastStamp
            || sample.Timestamp - _lastStamp > TimeSpan.TicksPerSecond / 2)
        {
            // Starting to follow can first expose a cached heartbeat: glide to it from the view shown now (its own
            // receive time would place the timeline's start behind its first sample). The first fresh sample starts
            // the live clock from the displayed view; otherwise the heartbeat's age becomes permanent extra delay.
            Clear(); _source = sample.Source; _kind = sample.Frame;
            _cachedOrigin = now - sample.ReceivedAt >= Delay;
            _originStamp = _lastStamp = sample.Timestamp; _originTime = _cachedOrigin ? now : sample.ReceivedAt;
            if (_kind != SpectatorCameraFrame.World)
            {
                initialPosition = RemoteSpectatorMotionCompensationRules.CaptureLocalPosition(initialPosition, sample.Reference);
                initialRotation = RemoteSpectatorMotionCompensationRules.CaptureLocalRotation(initialRotation, sample.Reference);
            }
            _frames.Add(new Frame(now - _originTime - Delay, initialPosition, initialRotation, sample.FieldOfView > 0 ? initialFov : 0));
            _frames.Add(new Frame(0, sample.Position, sample.Rotation, sample.FieldOfView));
        }
        else if (sample.Timestamp > _lastStamp)
        {
            if (_frames.Count == Capacity) _frames.RemoveAt(0);
            _frames.Add(new Frame((sample.Timestamp - _originStamp) / (double)TimeSpan.TicksPerSecond,
                sample.Position, sample.Rotation, sample.FieldOfView));
            _lastStamp = sample.Timestamp;
        }
        double at = now - _originTime - Delay;
        int right = 1;
        while (right < _frames.Count - 1 && _frames[right].Time < at) right++;
        var a = _frames[right - 1]; var b = _frames[right];
        float span = (float)(b.Time - a.Time), t = Mathf.Clamp01((float)(at - a.Time) / span);
        // The local camera rig re-seeds beyond eight metres. Replay its cut instead of flying through that gap.
        if ((b.Position - a.Position).sqrMagnitude > 64) t = at < b.Time ? 0 : 1;
        Vector3 delta = (b.Position - a.Position) / span;
        Vector3 va = right > 1 && (a.Position - _frames[right - 2].Position).sqrMagnitude <= 64
            ? (b.Position - _frames[right - 2].Position) / (float)(b.Time - _frames[right - 2].Time) : delta;
        Vector3 vb = right + 1 < _frames.Count && (_frames[right + 1].Position - b.Position).sqrMagnitude <= 64
            ? (_frames[right + 1].Position - a.Position) / (float)(_frames[right + 1].Time - a.Time) : delta;
        float t2 = t * t, t3 = t2 * t;
        Vector3 position = (2 * t3 - 3 * t2 + 1) * a.Position + (t3 - 2 * t2 + t) * span * va
            + (-2 * t3 + 3 * t2) * b.Position + (t3 - t2) * span * vb;
        // Camera interpolation stays inside the observed segment, including at a stop or reversal.
        position.x = Mathf.Clamp(position.x, Mathf.Min(a.Position.x, b.Position.x), Mathf.Max(a.Position.x, b.Position.x));
        position.y = Mathf.Clamp(position.y, Mathf.Min(a.Position.y, b.Position.y), Mathf.Max(a.Position.y, b.Position.y));
        position.z = Mathf.Clamp(position.z, Mathf.Min(a.Position.z, b.Position.z), Mathf.Max(a.Position.z, b.Position.z));
        Quaternion rotation = Quaternion.Slerp(a.Rotation, b.Rotation, t);
        return (_kind == SpectatorCameraFrame.World ? position : RemoteSpectatorMotionCompensationRules.ResolveLocalPosition(position, sample.Reference),
            _kind == SpectatorCameraFrame.World ? rotation : RemoteSpectatorMotionCompensationRules.ResolveLocalRotation(rotation, sample.Reference),
            Mathf.Lerp(a.Fov, b.Fov, t));
    }

    private readonly struct Frame
    {
        internal readonly double Time;
        internal readonly Vector3 Position;
        internal readonly Quaternion Rotation;
        internal readonly float Fov;
        internal Frame(double time, Vector3 position, Quaternion rotation, float fov)
        { Time = time; Position = position; Rotation = rotation; Fov = fov; }
    }
}
