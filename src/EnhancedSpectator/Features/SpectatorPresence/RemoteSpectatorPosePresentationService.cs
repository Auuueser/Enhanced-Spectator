using System.Collections.Generic;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Networking;
using UnityEngine;

namespace EnhancedSpectator.Features.SpectatorPresence;

/// <summary>
/// Keeps remote poses attached to locally resolved moving frames between network samples.
/// </summary>
public sealed class RemoteSpectatorPosePresentationService
{
    private readonly IGameSpectatorAdapter _gameAdapter;
    private readonly Dictionary<ulong, SampleState> _samples = new Dictionary<ulong, SampleState>();

    /// <summary>Creates the presentation service.</summary>
    public RemoteSpectatorPosePresentationService(IGameSpectatorAdapter gameAdapter)
    {
        _gameAdapter = gameAdapter;
    }

    /// <summary>Resolves the current presentation pose for one received network sample.</summary>
    public void Resolve(
        SpectatorPoseState pose,
        out Vector3 position,
        out Quaternion rotation,
        out bool motionReferenced)
    {
        Resolve(
            pose,
            out position,
            out rotation,
            out motionReferenced,
            out _);
    }

    /// <summary>Resolves the current pose and moving reference used for presentation smoothing.</summary>
    public void Resolve(
        SpectatorPoseState pose,
        out Vector3 position,
        out Quaternion rotation,
        out bool motionReferenced,
        out SpectatorMotionReferencePose motionReference)
    {
        position = pose.Position;
        rotation = pose.Rotation;
        motionReferenced = false;
        motionReference = default;

        if (pose.HasTargetMotionReference
            && _gameAdapter is IGameSpectatedTargetMotionReferenceAdapter targetMotionReferenceAdapter
            && targetMotionReferenceAdapter.TryGetSpectatedTargetMotionReference(
                pose.TargetClientId,
                pose.TargetPlayerSlotId,
                out SpectatorMotionReferencePose currentTargetMotionReference))
        {
            position = RemoteSpectatorMotionCompensationRules.ResolveLocalPosition(
                pose.TargetMotionReferenceLocalPosition,
                currentTargetMotionReference);
            rotation = RemoteSpectatorMotionCompensationRules.ResolveLocalRotation(
                pose.TargetMotionReferenceLocalRotation,
                currentTargetMotionReference);
            motionReferenced = true;
            motionReference = currentTargetMotionReference;
            _samples.Remove(pose.LocalClientId);
            return;
        }

        if (pose.HasMotionReference
            && _gameAdapter.TryGetShipMotionReference(out SpectatorMotionReferencePose currentMotionReference))
        {
            position = RemoteSpectatorMotionCompensationRules.ResolveLocalPosition(
                pose.MotionReferenceLocalPosition,
                currentMotionReference);
            rotation = RemoteSpectatorMotionCompensationRules.ResolveLocalRotation(
                pose.MotionReferenceLocalRotation,
                currentMotionReference);
            motionReferenced = true;
            motionReference = currentMotionReference;
            _samples.Remove(pose.LocalClientId);
            return;
        }

        if (!_samples.TryGetValue(pose.LocalClientId, out SampleState sample)
            || sample.TimestampTicks != pose.TimestampTicks)
        {
            SpectatorMotionReferencePose sampledReference = default;
            bool referenced = _gameAdapter.IsWorldPositionInsideShip(pose.Position)
                && _gameAdapter.TryGetShipMotionReference(out sampledReference);
            sample = new SampleState(
                pose.TimestampTicks,
                pose.Position,
                pose.Rotation,
                referenced,
                referenced ? sampledReference : default);
            _samples[pose.LocalClientId] = sample;
        }

        if (!sample.HasReference
            || !_gameAdapter.TryGetShipMotionReference(out SpectatorMotionReferencePose currentReference))
        {
            return;
        }

        position = RemoteSpectatorMotionCompensationRules.ResolvePosition(
            sample.Position,
            sample.RotationReference,
            currentReference);
        rotation = RemoteSpectatorMotionCompensationRules.ResolveRotation(
            sample.Rotation,
            sample.RotationReference,
            currentReference);
        motionReferenced = true;
        motionReference = currentReference;
    }

    /// <summary>Forgets presentation history for a disconnected or hidden peer.</summary>
    public void Remove(ulong clientId)
    {
        _samples.Remove(clientId);
    }

    /// <summary>Clears all sampled reference state.</summary>
    public void Clear()
    {
        _samples.Clear();
    }

    private readonly struct SampleState
    {
        public SampleState(
            long timestampTicks,
            Vector3 position,
            Quaternion rotation,
            bool hasReference,
            SpectatorMotionReferencePose rotationReference)
        {
            TimestampTicks = timestampTicks;
            Position = position;
            Rotation = rotation;
            HasReference = hasReference;
            RotationReference = rotationReference;
        }

        public long TimestampTicks { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public bool HasReference { get; }
        public SpectatorMotionReferencePose RotationReference { get; }
    }
}
