using UnityEngine;

namespace EnhancedSpectator.Features.SpectatorPresence;

/// <summary>
/// World: fixed shots (cinematic, choreography). Target: the watched player's eyes (moves and turns with them).
/// TargetTranslation: a camera following the watched player (vanilla, free, third person): its offset from them,
/// carried by their movement here, not their turning. TargetPivot uses the vanilla spine pivot instead of the root.
/// LegacyReference: an older sender. Ship: aboard the ship.
/// </summary>
internal enum SpectatorCameraFrame { World, Target, LegacyReference, Ship, TargetTranslation, TargetPivot }

/// <summary>A completed camera sample, separate from the model's movement reference.</summary>
internal readonly struct SpectatorCameraSample
{
    internal readonly ulong Source;
    internal readonly long Timestamp;
    internal readonly Vector3 Position;
    internal readonly Quaternion Rotation;
    internal readonly float FieldOfView;
    internal readonly SpectatorCameraFrame Frame;
    internal readonly SpectatorMotionReferencePose Reference;
    internal readonly double ReceivedAt;
    internal readonly bool Buffered;

    internal SpectatorCameraSample(ulong source, long timestamp, Vector3 position, Quaternion rotation, float fieldOfView,
        SpectatorCameraFrame frame = SpectatorCameraFrame.World, SpectatorMotionReferencePose reference = default, double receivedAt = 0, bool buffered = true)
    { Source = source; Timestamp = timestamp; Position = position; Rotation = rotation; FieldOfView = fieldOfView; Frame = frame; Reference = reference; ReceivedAt = receivedAt; Buffered = buffered; }

}
