using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// Immutable snapshot of the current local spectator state.
/// </summary>
public readonly struct GameSpectatorSnapshot
{
    /// <summary>
    /// Creates a spectator snapshot.
    /// </summary>
    public GameSpectatorSnapshot(
        bool hasRound,
        bool hasLocalPlayer,
        bool isLocalPlayerDead,
        bool hasBegunSpectating,
        bool hasSpectatedTarget,
        bool isGameOverOverride,
        bool isSpectateCameraActive,
        Camera? spectateCamera,
        Transform? anchor,
        ulong? localPlayerSlotId,
        ulong? localPlayerActualClientId,
        ulong? spectatedPlayerSlotId,
        ulong? spectatedPlayerActualClientId)
    {
        HasRound = hasRound;
        HasLocalPlayer = hasLocalPlayer;
        IsLocalPlayerDead = isLocalPlayerDead;
        HasBegunSpectating = hasBegunSpectating;
        HasSpectatedTarget = hasSpectatedTarget;
        IsGameOverOverride = isGameOverOverride;
        IsSpectateCameraActive = isSpectateCameraActive;
        SpectateCamera = spectateCamera;
        Anchor = anchor;
        LocalPlayerSlotId = localPlayerSlotId;
        LocalPlayerActualClientId = localPlayerActualClientId;
        SpectatedPlayerSlotId = spectatedPlayerSlotId;
        SpectatedPlayerActualClientId = spectatedPlayerActualClientId;
    }

    /// <summary>
    /// Gets an empty snapshot used when round state is unavailable.
    /// </summary>
    public static GameSpectatorSnapshot Unavailable { get; } = new GameSpectatorSnapshot(
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        null,
        null,
        null,
        null,
        null,
        null);

    /// <summary>
    /// Gets whether a round manager exists.
    /// </summary>
    public bool HasRound { get; }

    /// <summary>
    /// Gets whether a local player exists.
    /// </summary>
    public bool HasLocalPlayer { get; }

    /// <summary>
    /// Gets whether the local player is dead.
    /// </summary>
    public bool IsLocalPlayerDead { get; }

    /// <summary>
    /// Gets whether vanilla spectator transition state has begun.
    /// </summary>
    public bool HasBegunSpectating { get; }

    /// <summary>
    /// Gets whether a valid vanilla spectated target exists.
    /// </summary>
    public bool HasSpectatedTarget { get; }

    /// <summary>
    /// Gets whether vanilla game-over spectator override is active.
    /// </summary>
    public bool IsGameOverOverride { get; }

    /// <summary>
    /// Gets whether vanilla currently considers the spectator camera active.
    /// </summary>
    public bool IsSpectateCameraActive { get; }

    /// <summary>
    /// Gets the vanilla spectator camera when available.
    /// </summary>
    public Camera? SpectateCamera { get; }

    /// <summary>
    /// Gets the selected anchor transform for the current target.
    /// </summary>
    public Transform? Anchor { get; }

    /// <summary>
    /// Gets the local player's slot id when available.
    /// </summary>
    public ulong? LocalPlayerSlotId { get; }

    /// <summary>
    /// Gets the local player's Netcode client id when available.
    /// </summary>
    public ulong? LocalPlayerActualClientId { get; }

    /// <summary>
    /// Gets the spectated player's slot id when available.
    /// </summary>
    public ulong? SpectatedPlayerSlotId { get; }

    /// <summary>
    /// Gets the spectated player's Netcode client id when available.
    /// </summary>
    public ulong? SpectatedPlayerActualClientId { get; }
}
