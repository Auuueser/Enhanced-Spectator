using System.Collections.Generic;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>Bounded visible-page demand; completed sprites are retained by the renderer.</summary>
internal sealed class FearThumbnailRequests
{
    private readonly Queue<string> _pending = new Queue<string>();
    private readonly HashSet<string> _keys = new HashSet<string>(System.StringComparer.Ordinal);
    internal void BeginVisiblePage() { _pending.Clear(); _keys.Clear(); }
    internal bool Request(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || _pending.Count >= 18 || !_keys.Add(key)) return false;
        _pending.Enqueue(key); return true;
    }
    internal bool TryTake(out string key)
    {
        if (_pending.Count == 0) { key = string.Empty; return false; }
        key = _pending.Dequeue(); _keys.Remove(key); return true;
    }
    internal bool HasPending => _pending.Count > 0;
}
