using System.Collections.Generic;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// Isolates confirmed Lethal Company state used by fear-mode selection and render sources.
/// </summary>
public interface IGameFearModeAdapter
{
    /// <summary>Attempts to get the local dead player's ids.</summary>
    bool TryGetLocalDeadPlayerIdentity(out ulong clientId, out ulong slotId);

    /// <summary>Gets whether the specified connected player is currently dead.</summary>
    bool IsPlayerDead(ulong clientId, ulong claimedSlotId);

    /// <summary>Gets whether the local ESC quick menu is open.</summary>
    bool IsLocalQuickMenuOpen();

    /// <summary>Gets the active gameplay camera culling mask used to exclude hidden radar/debug layers.</summary>
    bool TryGetActiveCameraCullingMask(out int cullingMask);

    /// <summary>Attempts to get the local active audio-listener world position.</summary>
    bool TryGetActiveAudioListenerPosition(out Vector3 position);

    /// <summary>Copies available bounded original visual model keys into caller-owned storage.</summary>
    void CopyAvailableModelKeysTo(List<string> destination);

    /// <summary>Resolves loaded hierarchy and renderer roots as read-only visual source data.</summary>
    bool TryGetVisualSource(string modelKey, out FearVisualSource? source);

    /// <summary>Copies safe original clips for the selected model into caller-owned storage.</summary>
    void CopyFearSoundClipsTo(string modelKey, List<AudioClip> destination);
}
