using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>Reads the watched player through the brief invalid-target interval after their death.</summary>
public interface IGameSpectatorDeathHandoffAdapter
{
    /// <summary>Reads the raw watched target, including a target which has just died.</summary>
    bool TryGetSpectatedHandoffTarget(out SpectatorHandoffTarget target);
}

/// <summary>Direct game state needed to distinguish a death handoff from a manual target change.</summary>
public readonly struct SpectatorHandoffTarget
{
    /// <summary>Creates a snapshot of the confirmed watched player.</summary>
    public SpectatorHandoffTarget(ulong slotId, ulong clientId, Vector3 bodyPosition,
        bool isDead, bool isIndoors, bool isValid)
    {
        SlotId = slotId;
        ClientId = clientId;
        BodyPosition = bodyPosition;
        IsDead = isDead;
        IsIndoors = isIndoors;
        IsValid = isValid;
    }

    /// <summary>The vanilla player slot.</summary>
    public ulong SlotId { get; }
    /// <summary>The connected client identity.</summary>
    public ulong ClientId { get; }
    /// <summary>The player body position used for the subject-to-subject distance.</summary>
    public Vector3 BodyPosition { get; }
    /// <summary>Whether the watched player has died.</summary>
    public bool IsDead { get; }
    /// <summary>Whether the watched player is inside the factory.</summary>
    public bool IsIndoors { get; }
    /// <summary>Whether the player is a valid living spectator target.</summary>
    public bool IsValid { get; }
}
