using System.Collections.Generic;
using DunGen;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

internal static class SplitScreenInteriorVisibility
{
    private static readonly List<Vector3> Samples = new List<Vector3>();
    private static readonly List<Tile> Required = new List<Tile>(), Added = new List<Tile>();
    // Last room per sample slot; ResolveRoom reuses it while the sample stays inside its bounds.
    private static readonly List<Tile?> Previous = new List<Tile?>();
    private static AdjacentRoomCullingModified? _culler;
    private static float _nextResolve, _nextRepair, _nextSample;
    private static bool _fresh;

    /// <summary>
    /// The renderer's own cadence for collecting positions. It is separate from resolving: the vanilla culler's
    /// refresh may resolve first in a frame, and must never use up the renderer's turn to sample.
    /// </summary>
    internal static bool SampleDue => Time.unscaledTime >= _nextSample;

    internal static void SetSamples(List<Vector3> samples)
    {
        if (Samples.Count != samples.Count) Previous.Clear();
        Samples.Clear(); Samples.AddRange(samples);
        _nextSample = Time.unscaledTime + .1f; _fresh = true;
    }

    internal static void Refresh(AdjacentRoomCullingModified culler)
    {
        // An unready dungeon is re-checked at the normal resolve cadence instead of re-sampling every frame.
        // The renderer's sampling keeps its own cadence through this (clearing must not make it sample every frame).
        if (!culler.isActiveAndEnabled || !culler.Ready)
        { float sample = _nextSample; Clear(); _nextSample = sample; _nextResolve = Time.unscaledTime + .1f; return; }
        if (_culler != culler) { Clear(clearSamples: false); _culler = culler; }
        bool changed = false;
        // New positions are resolved at once; otherwise rooms are re-checked at the normal cadence.
        if (_fresh || Time.unscaledTime >= _nextResolve)
        {
            _nextResolve = Time.unscaledTime + .1f; _fresh = false;
            Required.Clear();
            while (Previous.Count < Samples.Count) Previous.Add(null);
            for (int i = 0; i < Samples.Count; i++)
            {
                var tile = SpectatorInteriorVisibility.ResolveRoom(culler, Samples[i], Previous[i], out _);
                Previous[i] = tile;
                if (tile != null && !Required.Contains(tile)) Required.Add(tile);
            }
            int first = 0;
            for (int depth = 0; depth < culler.AdjacentTileDepth; depth++)
            {
                int end = Required.Count;
                for (int i = first; i < end; i++) foreach (var doorway in Required[i].UsedDoorways)
                {
                    var next = doorway != null && doorway.ConnectedDoorway != null ? doorway.ConnectedDoorway.Tile : null;
                    if (next == null || Required.Contains(next) || !culler.allTiles.Contains(next)) continue;
                    if (culler.CullBehindClosedDoors && doorway!.DoorComponent != null && doorway.DoorComponent.ShouldCullBehind) continue;
                    Required.Add(next);
                }
                first = end;
            }
            for (int i = Added.Count - 1; i >= 0; i--)
            {
                var tile = Added[i];
                if (Required.Contains(tile)) continue;
                culler.visibleTiles.Remove(tile);
                if (tile != null && !tile.Tags.Tags.Contains(culler.DisableCullingTag)) culler.SetTileVisibility(tile, false);
                Added.RemoveAt(i);
                changed = true;
            }
        }
        bool audit = Time.unscaledTime >= _nextRepair;
        if (audit) _nextRepair = Time.unscaledTime + .2f;
        foreach (var tile in Required)
        {
            if (tile == null || !tile.gameObject.activeInHierarchy) continue;
            if (!culler.visibleTiles.Contains(tile))
            { culler.visibleTiles.Add(tile); Added.Add(tile); culler.SetTileVisibility(tile, true); changed = true; }
            else if (!culler.IsTileVisible(tile)) { culler.SetTileVisibility(tile, true); changed = true; }
            else if (audit && SpectatorInteriorVisibility.NeedsRendererRepair(culler, tile))
            { culler.SetTileVisibility(tile, true); changed = true; }
        }
        if (changed) culler.RefreshDoorVisibilities();
    }

    internal static void AfterVanillaRefresh(AdjacentRoomCullingModified culler)
    { if (_culler == culler) Added.Clear(); }

    internal static void InvalidateRooms() => _nextResolve = _nextRepair = _nextSample = 0;

    internal static void Clear() => Clear(clearSamples: true);

    private static void Clear(bool clearSamples)
    {
        if (_culler != null && _culler.isActiveAndEnabled && _culler.Ready)
        {
            foreach (var tile in Added)
            {
                _culler.visibleTiles.Remove(tile);
                if (tile != null && !tile.Tags.Tags.Contains(_culler.DisableCullingTag)) _culler.SetTileVisibility(tile, false);
            }
            _culler.RefreshDoorVisibilities();
        }
        _culler = null; if (clearSamples) Samples.Clear(); Required.Clear(); Added.Clear(); Previous.Clear(); _nextResolve = _nextRepair = _nextSample = 0; _fresh = false;
    }
}
