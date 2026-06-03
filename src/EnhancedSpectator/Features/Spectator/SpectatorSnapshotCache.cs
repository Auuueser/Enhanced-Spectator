using System;
using EnhancedSpectator.GameInterop;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>
/// Caches local spectator snapshots for one Unity frame.
/// </summary>
public sealed class SpectatorSnapshotCache
{
    private readonly IGameSpectatorAdapter _adapter;
    private int _cachedFrame = -1;
    private bool _cachedResult;
    private GameSpectatorSnapshot _cachedSnapshot;

    /// <summary>
    /// Creates a frame-scoped spectator snapshot cache.
    /// </summary>
    public SpectatorSnapshotCache(IGameSpectatorAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _cachedSnapshot = GameSpectatorSnapshot.Unavailable;
    }

    /// <summary>
    /// Attempts to get the cached snapshot for Unity's current frame.
    /// </summary>
    public bool TryGetCurrentFrameSnapshot(out GameSpectatorSnapshot snapshot)
    {
        return TryGetSnapshotForFrame(Time.frameCount, out snapshot);
    }

    /// <summary>
    /// Attempts to get the cached snapshot for a caller-provided frame.
    /// </summary>
    public bool TryGetSnapshotForFrame(int frame, out GameSpectatorSnapshot snapshot)
    {
        if (_cachedFrame == frame)
        {
            snapshot = _cachedSnapshot;
            return _cachedResult;
        }

        bool result = _adapter.TryGetLocalSpectatorSnapshot(out GameSpectatorSnapshot sampledSnapshot);
        _cachedFrame = frame;
        _cachedResult = result;
        _cachedSnapshot = sampledSnapshot;
        snapshot = sampledSnapshot;
        return result;
    }

    /// <summary>
    /// Clears the cached snapshot.
    /// </summary>
    public void Clear()
    {
        _cachedFrame = -1;
        _cachedResult = false;
        _cachedSnapshot = GameSpectatorSnapshot.Unavailable;
    }
}
