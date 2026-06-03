using EnhancedSpectator.GameInterop;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>
/// Tracks the current spectator anchor and detects target changes.
/// </summary>
public sealed class SpectatorAnchorService
{
    private SpectatorAnchorTargetIdentity _targetIdentity;
    private bool _hasTargetIdentity;

    /// <summary>
    /// Attempts to update the active anchor from a spectator snapshot.
    /// </summary>
    public bool TryUpdate(GameSpectatorSnapshot snapshot, out Transform? anchor, out bool targetChanged)
    {
        anchor = snapshot.Anchor;
        targetChanged = false;

        if (anchor == null || !snapshot.HasSpectatedTarget)
        {
            Clear();
            return false;
        }

        if (!SpectatorAnchorTargetIdentity.TryCreate(
            snapshot,
            anchor.GetInstanceID(),
            out SpectatorAnchorTargetIdentity nextIdentity))
        {
            Clear();
            return false;
        }

        targetChanged = _hasTargetIdentity && !_targetIdentity.Equals(nextIdentity);
        _targetIdentity = nextIdentity;
        _hasTargetIdentity = true;
        return true;
    }

    /// <summary>
    /// Clears the remembered anchor identity.
    /// </summary>
    public void Clear()
    {
        _targetIdentity = default;
        _hasTargetIdentity = false;
    }
}
