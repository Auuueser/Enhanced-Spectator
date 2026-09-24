namespace EnhancedSpectator.Features.FearMode;

/// <summary>One transition step per frame, with reversible enable/disable behavior.</summary>
internal sealed class ModelOpacityTransition
{
    private float _opacity = 1f;
    private int _frame = -1;

    internal float Current => _opacity;
    internal void Seed(float opacity)
    {
        _opacity = float.IsNaN(opacity) || float.IsInfinity(opacity) ? 1f : System.Math.Max(0f, System.Math.Min(1f, opacity));
        _frame = -1;
    }

    internal float Update(float target, bool enabled, int frame, float elapsed)
    {
        if (!enabled) { _opacity = 1f; _frame = -1; return 1f; }
        if (_frame == frame) return _opacity;
        _frame = frame;
        _opacity = FearModelAppearanceRules.SmoothOpacity(_opacity, target, elapsed);
        // Reach the original material exactly after recovery; floating-point easing alone
        // stays just below one forever and would retain a replacement outside the radius.
        if (target <= 0f && _opacity < .00001f) _opacity = 0f;
        if (target >= 1f && _opacity > .99999f) _opacity = 1f;
        return _opacity;
    }
}
