using System;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Describes one dead player's self-selected fear visual.
/// </summary>
public sealed class FearModeSelectionState : IEquatable<FearModeSelectionState>
{
    /// <summary>
    /// Creates a player-owned fear visual selection.
    /// </summary>
    public FearModeSelectionState(
        ulong clientId,
        ulong playerSlotId,
        string modelKey,
        long revision)
    {
        ClientId = clientId;
        PlayerSlotId = playerSlotId;
        ModelKey = modelKey ?? string.Empty;
        Revision = revision;
    }

    /// <summary>Gets the player Netcode client id that owns this selection.</summary>
    public ulong ClientId { get; }

    /// <summary>Gets the player's vanilla slot id.</summary>
    public ulong PlayerSlotId { get; }

    /// <summary>Gets the bounded runtime model key.</summary>
    public string ModelKey { get; }

    /// <summary>Gets the monotonic revision supplied by the owning player.</summary>
    public long Revision { get; }

    /// <inheritdoc />
    public bool Equals(FearModeSelectionState? other)
    {
        return other != null
            && ClientId == other.ClientId
            && PlayerSlotId == other.PlayerSlotId
            && string.Equals(ModelKey, other.ModelKey, StringComparison.Ordinal)
            && Revision == other.Revision;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as FearModeSelectionState);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int hash = ClientId.GetHashCode();
        hash = (hash * 397) ^ PlayerSlotId.GetHashCode();
        hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ModelKey);
        hash = (hash * 397) ^ Revision.GetHashCode();
        return hash;
    }
}
