namespace EnhancedSpectator.Features.FearMode;

// Aggregates every sampled frame, so a five-second log cannot hide alternating states.
// The caller does not collect samples while diagnostics are off.
internal sealed class FadeFrameDiagnostics
{
    private int _frame = -1;
    private bool _previousReady;
    private float _previousOpacity;
    internal int Frames { get; private set; }
    internal int NotReady { get; private set; }
    internal int MissingBody { get; private set; }
    internal int ReadinessChanges { get; private set; }
    internal float MinOpacity { get; private set; } = 1f;
    internal float MaxOpacity { get; private set; }
    internal float LargestStep { get; private set; }

    internal void Sample(int frame, bool ready, bool body, float opacity)
    {
        if (_frame == frame) return;
        if (Frames > 0)
        {
            if (_previousReady != ready) ReadinessChanges++;
            LargestStep = System.Math.Max(LargestStep, System.Math.Abs(opacity - _previousOpacity));
        }
        _frame = frame; _previousReady = ready; _previousOpacity = opacity;
        Frames++;
        if (!ready) NotReady++;
        if (!body) MissingBody++;
        MinOpacity = System.Math.Min(MinOpacity, opacity);
        MaxOpacity = System.Math.Max(MaxOpacity, opacity);
    }

    internal void Clear()
    {
        _frame = -1; Frames = NotReady = MissingBody = ReadinessChanges = 0;
        MinOpacity = 1f; MaxOpacity = LargestStep = 0f;
    }
}
