namespace EnhancedSpectator.GameInterop;

// A short idle gap between default heads and fear models must not rebuild GPU resources.
internal sealed class NativeFadeResourceLease
{
    private float _releaseAt = float.PositiveInfinity;
    internal void Used() => _releaseAt = float.PositiveInfinity;
    internal void Idle(float now) => _releaseAt = now + 3f;
    internal bool Expired(float now, int owners) => owners == 0 && now >= _releaseAt;
}
