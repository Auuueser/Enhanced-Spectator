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
        Quaternion targetMotionReferenceLocalRotation = default,
        bool modelStowed = false,
        bool autoCentering = false,
        SpectatorSplitView splitView = SpectatorSplitView.None,
        ulong? followingClientId = null,
        bool firstPersonView = false,
        byte cameraMode = 0,
        byte cameraStyle = 0,
        bool hasCameraPose = false,
        Vector3 cameraLocalPosition = default,
        Quaternion cameraLocalRotation = default,
        float cameraFieldOfView = 0)
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
        ModelStowed = modelStowed;
        AutoCentering = autoCentering;
        SplitView = splitView;
        FollowingClientId = followingClientId;
        FirstPersonView = firstPersonView;
        CameraMode = cameraMode;
        CameraStyle = cameraStyle;
        HasCameraPose = hasCameraPose; CameraLocalPosition = cameraLocalPosition;
        CameraLocalRotation = cameraLocalRotation; CameraFieldOfView = cameraFieldOfView;
    }

    /// <summary>The optional rendered camera pose, separate from the ghost/voice position.</summary>
    public bool HasCameraPose { get; }
    /// <summary>Rendered camera offset in the logical pose's coordinates, retaining its motion compensation.</summary>
    public Vector3 CameraLocalPosition { get; }
    /// <summary>Rendered camera rotation relative to the logical pose.</summary>
    public Quaternion CameraLocalRotation { get; }
    /// <summary>Rendered camera vertical field of view in degrees.</summary>
    public float CameraFieldOfView { get; }

    /// <summary>
    /// The camera this spectator watches through: 0 the game's own view, otherwise 1 + <c>SpectatorCameraMode</c>
    /// (a follower in "watch together" takes the same camera). <see cref="CameraStyle"/> is the choreography style.
    /// </summary>
    public byte CameraMode { get; }
    /// <summary>The choreography (monitor) style of <see cref="CameraMode"/>.</summary>
    public byte CameraStyle { get; }

    /// <summary>Watching through the target's eyes: the watched player is told when this spectator speaks.</summary>
    public bool FirstPersonView { get; }

    /// <summary>The audience member this split-screen spectator watches together with, if any.</summary>
    public ulong? FollowingClientId { get; }

    /// <summary>Split-screen audience or watching state; decides who sees and hears this spectator.</summary>
    public SpectatorSplitView SplitView { get; }

    /// <summary>
    /// Gets whether the local player is spectating.
    /// </summary>
    public bool IsSpectating { get; }

    /// <summary>Temporarily hide this spectator's model; pose and voice routing remain valid.</summary>
    public bool ModelStowed { get; }

    /// <summary>Sender camera is in idle centering; receivers decide whether to show its model.</summary>
    public bool AutoCentering { get; }

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
            || ModelStowed != other.ModelStowed
            || AutoCentering != other.AutoCentering
            || SplitView != other.SplitView
            || FollowingClientId != other.FollowingClientId
            || FirstPersonView != other.FirstPersonView
            || CameraMode != other.CameraMode
            || CameraStyle != other.CameraStyle
            || HasCameraPose != other.HasCameraPose
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
        return positionDeltaSqr <= PositionEpsilonSqr && rotationDot >= RotationDotEpsilon
            && (!HasCameraPose || (CameraLocalPosition - other.CameraLocalPosition).sqrMagnitude <= PositionEpsilonSqr
                && Mathf.Abs(Quaternion.Dot(CameraLocalRotation, other.CameraLocalRotation)) >= RotationDotEpsilon
                && Mathf.Abs(CameraFieldOfView - other.CameraFieldOfView) < .01f);
    }
}
