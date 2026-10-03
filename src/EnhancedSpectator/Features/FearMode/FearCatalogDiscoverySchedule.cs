namespace EnhancedSpectator.Features.FearMode;

/// <summary>Retains discovered sources until lifecycle, definitions, or a fallback rescan changes them.</summary>
internal sealed class FearCatalogDiscoverySchedule
{
    private bool _dirty = true;
    private int _round, _definitions, _count;
    private float _nextFallback;
    internal void Invalidate() => _dirty = true;
    internal bool NeedsRefresh(int round, int definitions, int count, float now) =>
        _dirty || round != _round || definitions != _definitions || count != _count || now >= _nextFallback;
    internal void Refreshed(int round, int definitions, int count, float now)
    {
        _dirty = false; _round = round; _definitions = definitions; _count = count;
        _nextFallback = now + 60f;
    }
}
