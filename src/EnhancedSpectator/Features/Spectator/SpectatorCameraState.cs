using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>
/// Tracks the current enhanced spectator camera state.
/// </summary>
public sealed class SpectatorCameraState
{
    /// <summary>
    /// Gets the current enhanced camera mode.
    /// </summary>
    public SpectatorCameraMode Mode { get; internal set; } = SpectatorCameraMode.Freecam;

    /// <summary>
    /// Gets whether enhanced freecam is actively writing the camera transform.
    /// </summary>
    public bool IsActive { get; internal set; }

    /// <summary>
    /// Gets whether the user currently wants freecam enabled.
    /// </summary>
    public bool UserEnabled { get; internal set; }

    /// <summary>
    /// Gets the current offset from the spectated target anchor.
    /// </summary>
    public Vector3 Offset { get; internal set; }

    /// <summary>
    /// Gets the current camera rotation.
    /// </summary>
    public Quaternion Rotation { get; internal set; } = Quaternion.identity;

    /// <summary>
    /// Gets the logical ghost/avatar rotation published to remote visuals and positional voice.
    /// </summary>
    public Quaternion RepresentationRotation { get; internal set; } = Quaternion.identity;

    /// <summary>
    /// Gets whether a world-space camera pose has been observed.
    /// </summary>
    public bool HasWorldPose { get; internal set; }

    /// <summary>
    /// Gets the latest spectator camera world position.
    /// </summary>
    public Vector3 WorldPosition { get; internal set; }

    /// <summary>
    /// Gets the latest locally rendered spectator camera position.
    /// </summary>
    public Vector3 RenderedWorldPosition { get; internal set; }

    /// <summary>
    /// Gets whether self-ghost third-person view is active.
    /// </summary>
    public bool IsThirdPerson => IsActive && Mode == SpectatorCameraMode.ThirdPerson;

    /// <summary>Actual local model center relative to the logical ghost, in representation coordinates.</summary>
    public Vector3 LocalModelCenter { get; internal set; }
    /// <summary>Radius of the local model bounds, including the configured scale.</summary>
    public float LocalModelRadius { get; internal set; }
    /// <summary>Whether the fear renderer currently supplies local composition bounds.</summary>
    public bool HasLocalModelBounds { get; internal set; }

    /// <summary>
    /// Gets the current target slot id when available.
    /// </summary>
    public ulong? TargetSlotId { get; internal set; }

    /// <summary>
    /// Gets the current target Netcode client id when available.
    /// </summary>
    public ulong? TargetActualClientId { get; internal set; }
}
