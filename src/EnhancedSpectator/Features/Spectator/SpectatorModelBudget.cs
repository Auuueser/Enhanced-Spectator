using System;
using System.Collections.Generic;

namespace EnhancedSpectator.Features.Spectator;

internal sealed class SpectatorModelBudget
{
    private readonly List<(ulong owner, float distance)> _candidates = new List<(ulong, float)>();
    private readonly HashSet<ulong> _visible = new HashSet<ulong>();
    internal void Begin() => _candidates.Clear();
    internal void Add(ulong owner, float distanceSquared)
    {
        // A modest incumbent preference prevents near-equal distances churning models in and out.
        _candidates.Add((owner, Math.Max(0, distanceSquared) * (_visible.Contains(owner) ? .81f : 1f)));
    }
    internal void Finish(int limit)
    {
        _candidates.Sort((a,b) => { int d=a.distance.CompareTo(b.distance); return d!=0 ? d : a.owner.CompareTo(b.owner); });
        _visible.Clear();
        for(int i=0;i<_candidates.Count && (limit<=0 || i<limit);i++) _visible.Add(_candidates[i].owner);
    }
    internal bool Contains(ulong owner) => _visible.Contains(owner);
    internal void Clear() { _candidates.Clear(); _visible.Clear(); }
}
