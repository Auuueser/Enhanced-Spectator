using System;
using System.Collections.Generic;

namespace EnhancedSpectator.Features.SplitScreen;

// Counts successful renders, not scheduler requests. Frame time is the observed game frame interval.
internal sealed class SplitScreenRenderMetrics
{
    private readonly Dictionary<SplitScreenKey, int> _renders = new();
    private double _started, _frameSeconds, _renderSeconds;
    private int _frames, _tileRenders;
    private double _tileSeconds;
    internal void Reset(double now) { _renders.Clear(); _started = now; _frameSeconds = _renderSeconds = _tileSeconds = 0; _frames = _tileRenders = 0; }
    internal void Frame(float seconds) { _frameSeconds += seconds; _frames++; }
    // CPU time spent submitting one view; GPU execution is asynchronous and not included.
    internal void RenderCost(double seconds, bool tile)
    {
        _renderSeconds += seconds;
        if (tile) { _tileSeconds += seconds; _tileRenders++; }
    }
    internal void Rendered(SplitScreenKey key) => _renders[key] = _renders.TryGetValue(key, out int count) ? count + 1 : 1;
    internal SplitScreenMeasuredRates Sample(double now, IReadOnlyList<SplitScreenParticipant> players, SplitScreenKey? focused)
    {
        double elapsed = now - _started;
        float main = 0, minimum = float.PositiveInfinity, maximum = 0;
        foreach (var player in players)
        {
            float fps = elapsed > 0 && _renders.TryGetValue(player.Key, out int count) ? (float)(count / elapsed) : 0;
            if (player.Key == focused) main = fps;
            else { minimum = Math.Min(minimum, fps); maximum = Math.Max(maximum, fps); }
        }
        var sample = new SplitScreenMeasuredRates(main, float.IsPositiveInfinity(minimum) ? 0 : minimum, maximum,
            _frames > 0 ? (float)(_frameSeconds * 1000 / _frames) : 0,
            _frames > 0 ? (float)(_renderSeconds * 1000 / _frames) : 0, _tileRenders > 0 ? (float)(_tileSeconds * 1000 / _tileRenders) : 0);
        Reset(now);
        return sample;
    }
}

internal readonly struct SplitScreenMeasuredRates
{
    internal readonly float MainFps, MinimumTileFps, MaximumTileFps, FrameMilliseconds, RenderMillisecondsPerFrame, MillisecondsPerTile;
    internal SplitScreenMeasuredRates(float main, float minimum, float maximum, float frameMilliseconds, float renderPerFrame, float perTile)
    {
        MainFps = main; MinimumTileFps = minimum; MaximumTileFps = maximum; FrameMilliseconds = frameMilliseconds;
        RenderMillisecondsPerFrame = renderPerFrame; MillisecondsPerTile = perTile;
    }
}
