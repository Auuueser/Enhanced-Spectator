using System;
using System.Collections.Generic;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>Bounded exclusive ownership of inactive visuals; rented objects leave the pool.</summary>
public sealed class VisualReusePool<TKey, TValue> : IDisposable where TValue : class, IDisposable
{
    private readonly int _capacity;
    private readonly List<(TKey Key, TValue Value)> _idle = new List<(TKey, TValue)>();
    /// <summary>Sets the maximum number of idle instances; active instances are never evicted.</summary>
    public VisualReusePool(int capacity) { if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity)); _capacity = capacity; }
    /// <summary>Transfers a matching idle instance to the caller exclusively.</summary>
    public bool TryTake(TKey key, out TValue? value)
    {
        for (int i = _idle.Count - 1; i >= 0; i--)
            if (EqualityComparer<TKey>.Default.Equals(key, _idle[i].Key))
            { value = _idle[i].Value; _idle.RemoveAt(i); return true; }
        value = null; return false;
    }
    /// <summary>Accepts an inactive instance and disposes the oldest idle instance on overflow.</summary>
    public void Return(TKey key, TValue value)
    {
        foreach (var entry in _idle)
            if (ReferenceEquals(entry.Value, value)) throw new InvalidOperationException("Visual returned twice");
        if (_capacity == 0) { value.Dispose(); return; }
        if (_idle.Count == _capacity) { _idle[0].Value.Dispose(); _idle.RemoveAt(0); }
        _idle.Add((key, value));
    }
    /// <summary>Destroys all idle instances. The empty pool can be reused after a scene change.</summary>
    public void Dispose() { foreach (var entry in _idle) entry.Value.Dispose(); _idle.Clear(); }
}
