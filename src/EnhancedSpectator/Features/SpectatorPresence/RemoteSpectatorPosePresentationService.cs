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

    /// <summary>Watch-together parties: a follower is presented at its place in its leader's formation.</summary>
    public SpectatorParties? Parties { get; set; }

    /// <summary>The rendered view, independent of a model's formation place, in its locally resolved moving frame.</summary>
    public void ResolveCamera(SpectatorPoseState pose, out Vector3 position, out Quaternion rotation)
    {
        var sample = CameraSample(pose);
        position = sample.Frame == SpectatorCameraFrame.World ? sample.Position
            : RemoteSpectatorMotionCompensationRules.ResolveLocalPosition(sample.Position, sample.Reference);
        rotation = sample.Frame == SpectatorCameraFrame.World ? sample.Rotation
            : RemoteSpectatorMotionCompensationRules.ResolveLocalRotation(sample.Rotation, sample.Reference);
    }

    // Replay fixed shots in world space and following cameras in their own moving reference. The playback buffer
    // must distinguish those references: offsets from a spine pivot cannot be reused as offsets from a root.
    internal SpectatorCameraSample CameraSample(SpectatorPoseState pose, double receivedAt = 0)
    {
        Vector3 position = pose.Position;
        Quaternion rotation = pose.Rotation;
        var kind = SpectatorCameraFrame.World;
        SpectatorMotionReferencePose reference = default;
        if (!pose.CameraTimeline)
        {
            // Older senders omit unchanged world movement. Keep their existing reference-based following.
            ResolveOwn(pose, out position, out rotation, out bool referenced, out reference);
            if (referenced)
            {
                position = RemoteSpectatorMotionCompensationRules.CaptureLocalPosition(position, reference);
                rotation = RemoteSpectatorMotionCompensationRules.CaptureLocalRotation(rotation, reference);
                kind = SpectatorCameraFrame.LegacyReference;
            }
        }
        else if (pose.HasTargetMotionReference && !FixedShots(pose)
            && _gameAdapter is IGameSpectatedTargetMotionReferenceAdapter targetAdapter
            && targetAdapter.TryGetSpectatedTargetMotionReference(pose.TargetClientId, pose.TargetPlayerSlotId, out reference))
        {
            if (pose.FirstPersonView || pose.CameraMode == (byte)(Features.Spectator.SpectatorCameraMode.FirstPerson + 1))
            { position = pose.TargetMotionReferenceLocalPosition; rotation = pose.TargetMotionReferenceLocalRotation; kind = SpectatorCameraFrame.Target; }
            else
            {
                // A camera following the watched player is played back where it sits relative to them, as the sender
                // saw them, around their position here: replayed in world space (a moment late) the player ran out of
                // the middle of the follower's picture. Their turning does not orbit it (see ResolveOwn).
                position = RemoteSpectatorMotionCompensationRules.RestoreWorldOffset(
                    pose.TargetMotionReferenceLocalPosition, pose.TargetMotionReferenceLocalRotation, pose.Rotation);
                Vector3 anchor = reference.Position;
                kind = SpectatorCameraFrame.TargetTranslation;
                // The game's own spectate camera turns around a pivot that sways with the walk: measured from the
                // player's root, that sway (a moment late) shook the player in the follower's picture. Anchor it to
                // the same pivot here, swaying with the player as seen here.
                if (pose.HasCameraPivot && targetAdapter.TryGetSpectatedTargetPivot(pose.TargetClientId, pose.TargetPlayerSlotId, out Vector3 pivot))
                { position -= pose.CameraPivotOffset; anchor = pivot; kind = SpectatorCameraFrame.TargetPivot; }
                reference = new SpectatorMotionReferencePose(anchor, Quaternion.identity);
            }
        }
        else if (pose.HasMotionReference && _gameAdapter.TryGetShipMotionReference(out reference))
        { position = pose.MotionReferenceLocalPosition; rotation = pose.MotionReferenceLocalRotation; kind = SpectatorCameraFrame.Ship; }
        if (pose.HasCameraPose) { position += rotation * pose.CameraLocalPosition; rotation *= pose.CameraLocalRotation; }
        return new SpectatorCameraSample(pose.LocalClientId, pose.TimestampTicks, position, rotation,
            pose.HasCameraPose ? pose.CameraFieldOfView : 0, kind, reference, receivedAt, pose.CameraTimeline);
    }

    // Cinematic and choreography cameras hold their own shots in the world (fixed room positions, camera moves).
    private static bool FixedShots(SpectatorPoseState pose) => pose.CameraMode > 0
        && (Features.Spectator.SpectatorCameraMode)(pose.CameraMode - 1) is Features.Spectator.SpectatorCameraMode.Cinematic
            or Features.Spectator.SpectatorCameraMode.Monitor or Features.Spectator.SpectatorCameraMode.Director;

    /// <summary>Resolves the current pose and moving reference used for presentation smoothing.</summary>
    public void Resolve(
        SpectatorPoseState pose,
        out Vector3 position,
        out Quaternion rotation,
        out bool motionReferenced,
        out SpectatorMotionReferencePose motionReference)
    {
        if (Parties != null && Parties.TryGetFormation(pose.LocalClientId, out SpectatorPoseState leader, out int slot))
        {
            // Moving with the leader's frame too, so the formation keeps its shape between samples.
            ResolveModel(leader, out position, out rotation, out motionReferenced, out motionReference);
            position = SpectatorParties.Place(slot, position, rotation);
            return;
        }
        ResolveModel(pose, out position, out rotation, out motionReferenced, out motionReference);
    }

    private void ResolveModel(SpectatorPoseState pose, out Vector3 position, out Quaternion rotation,
        out bool motionReferenced, out SpectatorMotionReferencePose motionReference)
    {
        ResolveOwn(pose, out position, out rotation, out motionReferenced, out motionReference);
        // Watching through the cinematic or choreography camera, the model is shown (while speaking) where that camera
        // is, not at the ghost's place beside the watched player, which that camera leaves behind.
        if (!pose.HasCameraPose || !LethalCompanyModelVisibility.CameraStowed(pose)) return;
        position += rotation * pose.CameraLocalPosition;
        rotation *= pose.CameraLocalRotation;
    }

    private void ResolveOwn(
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
            // First person looks out of the watched player's eyes: that view turns with them, also between packets.
            if (pose.CameraMode == (byte)(Spectator.SpectatorCameraMode.FirstPerson + 1))
            {
                position = RemoteSpectatorMotionCompensationRules.ResolveLocalPosition(pose.TargetMotionReferenceLocalPosition, currentTargetMotionReference);
                rotation = RemoteSpectatorMotionCompensationRules.ResolveLocalRotation(pose.TargetMotionReferenceLocalRotation, currentTargetMotionReference);
                motionReferenced = true;
                motionReference = currentTargetMotionReference;
                _samples.Remove(pose.LocalClientId);
                return;
            }
            // A turning in place must not orbit B's model or C's view of B between pose packets. Recover the
            // offset captured by B, then follow only A's locally resolved translation. Keep B's own heading.
            position = currentTargetMotionReference.Position + RemoteSpectatorMotionCompensationRules.RestoreWorldOffset(
                pose.TargetMotionReferenceLocalPosition,
                pose.TargetMotionReferenceLocalRotation,
                pose.Rotation);
            motionReferenced = true;
            // Visual smoothing must transport with the same translation-only frame, without reapplying A's yaw.
            motionReference = new SpectatorMotionReferencePose(currentTargetMotionReference.Position, Quaternion.identity);
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
