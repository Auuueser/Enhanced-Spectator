using System;
using EnhancedSpectator.GameInterop;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>
/// Primitive identity for the current spectator anchor target.
/// </summary>
public readonly struct SpectatorAnchorTargetIdentity : IEquatable<SpectatorAnchorTargetIdentity>
{
    private const byte NoneKind = 0;
    private const byte SlotKind = 1;
    private const byte SlotAndClientKind = 2;
    private const byte AnchorKind = 3;

    private readonly byte _kind;
    private readonly ulong _slotId;
    private readonly ulong _actualClientId;
    private readonly int _anchorInstanceId;

    private SpectatorAnchorTargetIdentity(
        byte kind,
        ulong slotId,
        ulong actualClientId,
        int anchorInstanceId)
    {
        _kind = kind;
        _slotId = slotId;
        _actualClientId = actualClientId;
        _anchorInstanceId = anchorInstanceId;
    }

    /// <summary>
    /// Creates a primitive identity for a valid spectator target.
    /// </summary>
    public static bool TryCreate(
        GameSpectatorSnapshot snapshot,
        int anchorInstanceId,
        out SpectatorAnchorTargetIdentity identity)
    {
        if (!snapshot.HasSpectatedTarget)
        {
            identity = default;
            return false;
        }

        if (snapshot.SpectatedPlayerSlotId.HasValue)
        {
            ulong slotId = snapshot.SpectatedPlayerSlotId.Value;
            if (snapshot.SpectatedPlayerActualClientId.HasValue)
            {
                identity = new SpectatorAnchorTargetIdentity(
                    SlotAndClientKind,
                    slotId,
                    snapshot.SpectatedPlayerActualClientId.Value,
                    0);
                return true;
            }

            identity = new SpectatorAnchorTargetIdentity(SlotKind, slotId, 0, 0);
            return true;
        }

        identity = new SpectatorAnchorTargetIdentity(AnchorKind, 0, 0, anchorInstanceId);
        return true;
    }

    /// <inheritdoc />
    public bool Equals(SpectatorAnchorTargetIdentity other)
    {
        return _kind == other._kind
            && _slotId == other._slotId
            && _actualClientId == other._actualClientId
            && _anchorInstanceId == other._anchorInstanceId;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is SpectatorAnchorTargetIdentity other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int hash = _kind;
        hash = (hash * 397) ^ _slotId.GetHashCode();
        hash = (hash * 397) ^ _actualClientId.GetHashCode();
        hash = (hash * 397) ^ _anchorInstanceId;
        return hash;
    }
}
