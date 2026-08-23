using UnityEngine;

namespace EnhancedSpectator.Features.SpectatorPresence;

/// <summary>
/// Describes one moving world reference used to present remote spectator poses smoothly.
/// </summary>
public readonly struct SpectatorMotionReferencePose
{
    /// <summary>Creates a moving-reference pose.</summary>
    public SpectatorMotionReferencePose(Vector3 position, Quaternion rotation)
    {
        Position = position;
        Rotation = rotation;
    }

    /// <summary>Gets the world position of the reference.</summary>
    public Vector3 Position { get; }

    /// <summary>Gets the world rotation of the reference.</summary>
    public Quaternion Rotation { get; }
}
