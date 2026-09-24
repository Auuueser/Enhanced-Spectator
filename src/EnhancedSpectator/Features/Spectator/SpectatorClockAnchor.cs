namespace EnhancedSpectator.Features.Spectator;

// Hold clock clearance for the entire death view, independent of visibility alpha or animation.
internal sealed class SpectatorClockAnchor
{
    private bool _hasValue;
    private float _bottom;
    internal float Update(float measuredBottom, bool dead, bool layoutChanged)
    {
        if (!_hasValue || !dead || layoutChanged) { _bottom = measuredBottom; _hasValue = true; }
        return _bottom;
    }
    internal void Clear() => _hasValue = false;
}
