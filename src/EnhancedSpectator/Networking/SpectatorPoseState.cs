using System;
using UnityEngine;

namespace EnhancedSpectator.Networking;

/// <summary>
/// Describes a modded spectator's world representation pose for remote visuals and positional voice.
/// </summary>
public sealed class SpectatorPoseState
{
    private const float PositionEpsilonSqr = 0.0004f;
    private const float RotationDotEpsilon = 0.9995f;

    /// <summary>
    /// Creates a spectator pose state snapshot.
    /// </summary>
    public SpectatorPoseState(
        bool isSpectating,
        ulong localClientId,
        ulong localPlayerSlotId,
        ulong? targetClientId,
        ulong? targetPlayerSlotId,
        Vector3 position,
        Quaternion rotation,
        long timestampTicks,
        bool hasMotionReference = false,
        Vector3 motionReferenceLocalPosition = default,
        Quaternion motionReferenceLocalRotation = default,
        bool hasTargetMotionReference = false,
        Vector3 targetMotionReferenceLocalPosition = default,
        Quaternion targetMotionReferenceLocalRotation = default)
    {
        IsSpectating = isSpectating;
        LocalClientId = localClientId;
        LocalPlayerSlotId = localPlayerSlotId;
        TargetClientId = targetClientId;
        TargetPlayerSlotId = targetPlayerSlotId;
        Position = position;
        Rotation = rotation;
        TimestampTicks = timestampTicks;
        HasMotionReference = hasMotionReference;
        MotionReferenceLocalPosition = motionReferenceLocalPosition;
        MotionReferenceLocalRotation = motionReferenceLocalRotation;
        HasTargetMotionReference = hasTargetMotionReference;
        TargetMotionReferenceLocalPosition = targetMotionReferenceLocalPosition;
        TargetMotionReferenceLocalRotation = targetMotionReferenceLocalRotation;
    }

    /// <summary>
    /// Gets whether the local player is spectating.
    /// </summary>
    public bool IsSpectating { get; }

    /// <summary>
    /// Gets the spectator Netcode client id.
    /// </summary>
    public ulong LocalClientId { get; }

    /// <summary>
    /// Gets the spectator player slot id.
    /// </summary>
    public ulong LocalPlayerSlotId { get; }

    /// <summary>
    /// Gets the watched player's Netcode client id when available.
    /// </summary>
    public ulong? TargetClientId { get; }

    /// <summary>
    /// Gets the watched player's slot id when available.
    /// </summary>
    public ulong? TargetPlayerSlotId { get; }

    /// <summary>
    /// Gets the logical spectator/ghost world position.
    /// </summary>
    public Vector3 Position { get; }

    /// <summary>
    /// Gets the logical spectator/ghost world rotation.
    /// </summary>
    public Quaternion Rotation { get; }

    /// <summary>
    /// Gets when the pose was observed.
    /// </summary>
    public long TimestampTicks { get; }

    /// <summary>Gets whether this pose includes sender-captured moving-reference coordinates.</summary>
    public bool HasMotionReference { get; }

    /// <summary>Gets the representation position in the moving reference's local space.</summary>
    public Vector3 MotionReferenceLocalPosition { get; }

    /// <summary>Gets the representation rotation in the moving reference's local space.</summary>
    public Quaternion MotionReferenceLocalRotation { get; }

    /// <summary>Gets whether this pose includes watched-player-local coordinates.</summary>
    public bool HasTargetMotionReference { get; }

    /// <summary>Gets the representation position in the watched player's local space.</summary>
    public Vector3 TargetMotionReferenceLocalPosition { get; }

    /// <summary>Gets the representation rotation in the watched player's local space.</summary>
    public Quaternion TargetMotionReferenceLocalRotation { get; }

    /// <summary>
    /// Gets whether this pose is close enough to another pose to skip a send.
    /// </summary>
    public bool ApproximatelyEquals(SpectatorPoseState? other)
    {
        if (other == null)
        {
            return false;
        }

        if (IsSpectating != other.IsSpectating
            || LocalClientId != other.LocalClientId
            || LocalPlayerSlotId != other.LocalPlayerSlotId
            || TargetClientId != other.TargetClientId
            || TargetPlayerSlotId != other.TargetPlayerSlotId)
        {
            return false;
        }

        if (HasTargetMotionReference != other.HasTargetMotionReference
            || (!HasTargetMotionReference && HasMotionReference != other.HasMotionReference))
        {
            return false;
        }

        Vector3 comparablePosition = HasTargetMotionReference
            ? TargetMotionReferenceLocalPosition
            : HasMotionReference ? MotionReferenceLocalPosition : Position;
        Vector3 otherComparablePosition = other.HasTargetMotionReference
            ? other.TargetMotionReferenceLocalPosition
            : other.HasMotionReference ? other.MotionReferenceLocalPosition : other.Position;
        Quaternion comparableRotation = HasTargetMotionReference
            ? TargetMotionReferenceLocalRotation
            : HasMotionReference ? MotionReferenceLocalRotation : Rotation;
        Quaternion otherComparableRotation = other.HasTargetMotionReference
            ? other.TargetMotionReferenceLocalRotation
            : other.HasMotionReference ? other.MotionReferenceLocalRotation : other.Rotation;
        float positionDeltaSqr = (comparablePosition - otherComparablePosition).sqrMagnitude;
        float rotationDot = Mathf.Abs(Quaternion.Dot(comparableRotation, otherComparableRotation));
        return positionDeltaSqr <= PositionEpsilonSqr && rotationDot >= RotationDotEpsilon;
    }
}
