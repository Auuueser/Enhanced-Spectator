using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.Features.SpectatorPresence;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// Adapter boundary for reading spectator information from Lethal Company.
/// </summary>
public interface IGameSpectatorAdapter
{
    /// <summary>
    /// Reads spectator state from the game through confirmed publicized members.
    /// </summary>
    SpectatorState ReadSpectatorState();

    /// <summary>
    /// Attempts to read the current local spectator snapshot.
    /// </summary>
    bool TryGetLocalSpectatorSnapshot(out GameSpectatorSnapshot snapshot);

    /// <summary>
    /// Gets whether the local player is currently in a vanilla spectator state.
    /// </summary>
    bool IsLocalPlayerSpectating();

    /// <summary>
    /// Attempts to get the best available anchor for the current spectated player.
    /// </summary>
    bool TryGetSpectatedPlayerAnchor(out Transform? anchor);

    /// <summary>
    /// Attempts to get the vanilla spectator camera.
    /// </summary>
    bool TryGetSpectateCamera(out Camera? camera);

    /// <summary>
    /// Attempts to get the current active local camera.
    /// </summary>
    bool TryGetActiveCamera(out Camera? camera);

    /// <summary>
    /// Gets whether the game-over spectator camera override is active.
    /// </summary>
    bool IsGameOverSpectateOverrideActive();

    /// <summary>
    /// Gets whether the local quick menu is currently open and should block gameplay input.
    /// </summary>
    bool IsLocalQuickMenuOpen();

    /// <summary>
    /// Gets whether the provided object is a valid spectate target.
    /// </summary>
    bool IsValidSpectateTarget(object? target);

    /// <summary>
    /// Attempts to get the current spectated player's ids.
    /// </summary>
    bool TryGetSpectatedPlayerId(out ulong slotId, out ulong actualClientId);

    /// <summary>
    /// Attempts to get the local player identity ids.
    /// </summary>
    bool TryGetLocalPlayerIdentity(out ulong clientId, out ulong slotId);

    /// <summary>
    /// Attempts to get the current in-game display name for a player.
    /// </summary>
    bool TryGetPlayerDisplayName(ulong clientId, ulong slotId, out string displayName);

    /// <summary>
    /// Attempts to get the local Dissonance player id used by vanilla voice playback.
    /// </summary>
    bool TryGetLocalVoicePlayerName(out string voicePlayerName);

    /// <summary>
    /// Attempts to get the local player's confirmed head-adjacent point.
    /// </summary>
    bool TryGetLocalPlayerHeadPoint(out Transform? anchor);

    /// <summary>
    /// Attempts to get the best local player head anchor for local-only visuals.
    /// </summary>
    bool TryGetLocalPlayerHeadAnchor(out Transform? anchor);

    /// <summary>
    /// Attempts to get the best local player head anchor position for local-only visuals.
    /// </summary>
    bool TryGetLocalPlayerHeadAnchorPosition(out Vector3 position);

    /// <summary>Gets whether a world position is currently inside the moving ship volume.</summary>
    bool IsWorldPositionInsideShip(Vector3 position);

    /// <summary>Attempts to read the current moving ship reference pose.</summary>
    bool TryGetShipMotionReference(out SpectatorMotionReferencePose referencePose);
}

/// <summary>Optional game interop boundary for moving-ship lifecycle state.</summary>
public interface IGameShipMotionStateAdapter
{
    /// <summary>Gets whether the ship takeoff animation is currently active.</summary>
    bool IsShipLeaving();
}

/// <summary>Optional game interop boundary for resolving a watched player's moving frame.</summary>
public interface IGameSpectatedTargetMotionReferenceAdapter
{
    /// <summary>Attempts to resolve a watched player's current world reference pose.</summary>
    bool TryGetSpectatedTargetMotionReference(
        ulong? targetClientId,
        ulong? targetPlayerSlotId,
        out SpectatorMotionReferencePose referencePose);
}

internal sealed class NoopGameSpectatorAdapter : IGameSpectatorAdapter
{
    public static NoopGameSpectatorAdapter Instance { get; } = new NoopGameSpectatorAdapter();

    private NoopGameSpectatorAdapter()
    {
    }

    public SpectatorState ReadSpectatorState()
    {
        return SpectatorState.Unavailable;
    }

    public bool TryGetLocalSpectatorSnapshot(out GameSpectatorSnapshot snapshot)
    {
        snapshot = GameSpectatorSnapshot.Unavailable;
        return false;
    }

    public bool IsLocalPlayerSpectating()
    {
        return false;
    }

    public bool TryGetSpectatedPlayerAnchor(out Transform? anchor)
    {
        anchor = null;
        return false;
    }

    public bool TryGetSpectateCamera(out Camera? camera)
    {
        camera = null;
        return false;
    }

    public bool TryGetActiveCamera(out Camera? camera)
    {
        camera = null;
        return false;
    }

    public bool IsGameOverSpectateOverrideActive()
    {
        return false;
    }

    public bool IsLocalQuickMenuOpen()
    {
        return false;
    }

    public bool IsValidSpectateTarget(object? target)
    {
        _ = target;
        return false;
    }

    public bool TryGetSpectatedPlayerId(out ulong slotId, out ulong actualClientId)
    {
        slotId = 0;
        actualClientId = 0;
        return false;
    }

    public bool TryGetLocalPlayerIdentity(out ulong clientId, out ulong slotId)
    {
        clientId = 0;
        slotId = 0;
        return false;
    }

    public bool TryGetPlayerDisplayName(ulong clientId, ulong slotId, out string displayName)
    {
        _ = clientId;
        _ = slotId;
        displayName = string.Empty;
        return false;
    }

    public bool TryGetLocalVoicePlayerName(out string voicePlayerName)
    {
        voicePlayerName = string.Empty;
        return false;
    }

    public bool TryGetLocalPlayerHeadPoint(out Transform? anchor)
    {
        anchor = null;
        return false;
    }

    public bool TryGetLocalPlayerHeadAnchor(out Transform? anchor)
    {
        anchor = null;
        return false;
    }

    public bool TryGetLocalPlayerHeadAnchorPosition(out Vector3 position)
    {
        position = Vector3.zero;
        return false;
    }

    public bool IsWorldPositionInsideShip(Vector3 position)
    {
        _ = position;
        return false;
    }

    public bool TryGetShipMotionReference(out SpectatorMotionReferencePose referencePose)
    {
        referencePose = default;
        return false;
    }
}
