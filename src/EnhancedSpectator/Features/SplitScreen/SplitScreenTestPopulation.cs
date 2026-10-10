using System;
using System.Collections.Generic;

namespace EnhancedSpectator.Features.SplitScreen;

// Only a local list changes: sources remain real players and no network identity is registered.
internal sealed class SplitScreenTestPopulation
{
    private readonly bool[] _alive = new bool[31];
    private readonly string?[] _sourceNames = new string?[31], _names = new string?[31];
    private readonly Random _random = new(81);
    private int _spawned;
    // Audience-only identities (slots from AudienceFirstSlot), dead from the start, independent of the views.
    internal const int AudienceFirstSlot = 100, MaxAudience = 31;
    internal bool Playing { get; set; }
    internal int Count { get; private set; }
    internal int Audience { get; private set; }
    internal void SetAudience(int count) => Audience = Math.Max(0, Math.Min(MaxAudience, count));
    /// <summary>The audience's names follow the text language: the English game font has no Chinese glyphs.</summary>
    internal bool Chinese { get; set; }
    private string AudienceName(int i, IReadOnlyList<SplitScreenParticipant> sources)
        => $"{(Chinese ? "观众" : "VIEWER")} {i + 1:00} · {sources[i % sources.Count].Name}";
    internal void SetCount(int count)
    {
        Count = _spawned = Math.Max(0, Math.Min(31, count));
        for (int i = 0; i < _alive.Length; i++) _alive[i] = i < Count;
    }
    internal void Kill(SplitScreenKey key)
    {
        if (key.ClientId != ulong.MaxValue || key.SlotId >= 31 || !_alive[key.SlotId]) return;
        _alive[key.SlotId] = false; Count--;
    }
    internal void KillRandom()
    {
        if (Count == 0) return;
        int selected = _random.Next(Count);
        for (int i = 0; i < _alive.Length; i++)
            if (_alive[i] && selected-- == 0) { Kill(new SplitScreenKey(ulong.MaxValue, (ulong)i)); return; }
    }
    internal void RestoreOne()
    {
        for (int i = 0; i < _alive.Length; i++)
            if (!_alive[i]) { _alive[i] = true; Count++; _spawned = Math.Max(_spawned, i + 1); return; }
    }
    /// <summary>Every synthetic identity spawned in this preview, by slot, with its name and whether it is alive.</summary>
    internal void CopyPeopleTo(IReadOnlyList<SplitScreenParticipant> sources, List<(int Slot, string Name, bool Alive)> output)
    {
        output.Clear();
        if (sources.Count == 0) return;
        for (int i = 0; i < _spawned; i++) output.Add((i, _names[i] ?? $"TEST {i + 1:00} · {sources[i % sources.Count].Name}", _alive[i]));
        for (int i = 0; i < Audience; i++) output.Add((AudienceFirstSlot + i, AudienceName(i, sources), false));
    }
    /// <summary>Synthetic identities killed in this preview, for the dead-player bar.</summary>
    internal void CopyDeadTo(IReadOnlyList<SplitScreenParticipant> sources, List<SplitScreenDeadPlayer> output, Func<SplitScreenKey, ulong> steamId)
    {
        output.Clear();
        if (sources.Count == 0) return;
        for (int i = 0; i < _spawned; i++)
            if (!_alive[i])
            {
                var source = sources[i % sources.Count];
                output.Add(new SplitScreenDeadPlayer(new SplitScreenKey(ulong.MaxValue, (ulong)i), source.Source,
                    _names[i] ?? $"TEST {i + 1:00} · {source.Name}", steamId(source.Source), false));
            }
        // The row holds 32 with the local seat: audience-only identities fill what the dead views leave.
        for (int i = 0; i < Audience && output.Count < MaxAudience; i++)
        {
            var source = sources[i % sources.Count];
            output.Add(new SplitScreenDeadPlayer(new SplitScreenKey(ulong.MaxValue, (ulong)(AudienceFirstSlot + i)), source.Source,
                AudienceName(i, sources), steamId(source.Source), false));
        }
    }
    internal void CopyTo(IReadOnlyList<SplitScreenParticipant> sources, List<SplitScreenParticipant> output)
    {
        output.Clear();
        if (sources.Count == 0) return;
        for (int i = 0; i < _alive.Length; i++)
            if (_alive[i])
            {
                var source = sources[i % sources.Count];
                if (_names[i] == null || _sourceNames[i] != source.Name)
                { _sourceNames[i] = source.Name; _names[i] = $"TEST {i + 1:00} · {source.Name}"; }
                output.Add(new SplitScreenParticipant(new SplitScreenKey(ulong.MaxValue, (ulong)i), source.Source, _names[i]!));
            }
    }
}
