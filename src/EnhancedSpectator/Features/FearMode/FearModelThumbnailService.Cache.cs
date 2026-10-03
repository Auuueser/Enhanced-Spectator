using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BepInEx;
using UnityEngine;

namespace EnhancedSpectator.Features.FearMode;

public sealed partial class FearModelThumbnailService
{
    private readonly FearThumbnailDiskCache _diskCache = new FearThumbnailDiskCache(
        Path.Combine(Paths.CachePath, "EnhancedSpectator", "Thumbnails"),
        () => FearThumbnailDiskCache.EnvironmentStamp(
            Path.Combine(Paths.ManagedPath, "Assembly-CSharp.dll"), Paths.PluginPath));
    private readonly Dictionary<string, Task<byte[]?>> _cacheReads = new Dictionary<string, Task<byte[]?>>(StringComparer.Ordinal);
    private readonly HashSet<string> _cacheChecked = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _firstRequested = new Dictionary<string, float>(StringComparer.Ordinal);
    private int _cacheUploadFrame = -1, _cacheUploads;

    /// <inheritdoc />
    public bool IsPending(string modelKey) => !TryGet(modelKey, out _)
        && (!_retryAfter.ContainsKey(modelKey) || !_firstRequested.TryGetValue(modelKey, out float started)
            || Time.unscaledTime - started < 3f);

    /// <inheritdoc />
    public void Prefetch(string modelKey)
    {
        if (_sprites.Count < MaxCachedThumbnails && !TryGet(modelKey, out _)) Request(modelKey);
    }

    private void StartCacheRead(string key)
    {
        if (!_firstRequested.ContainsKey(key)) _firstRequested[key] = Time.unscaledTime;
        if (_cacheChecked.Add(key)) _cacheReads[key] = _diskCache.Read(key);
    }

    private bool TryLoadCached(string key)
    {
        if (!_cacheReads.TryGetValue(key, out var task) || !task.IsCompleted) return false;
        byte[]? pixels = task.GetAwaiter().GetResult(); // Read catches cache errors; completion was checked above.
        if (pixels == null) { _cacheReads.Remove(key); return false; }
        if (_cacheUploadFrame != Time.frameCount) { _cacheUploadFrame = Time.frameCount; _cacheUploads = 0; }
        // A cached visible page fits one upload budget; prefetch cannot upload the whole catalog in that frame.
        if (_cacheUploads >= FearQuickMenuRules.PageSize) return false;
        _cacheUploads++;
        _cacheReads.Remove(key);
        var texture = new Texture2D(ThumbnailSize, ThumbnailSize, TextureFormat.RGBA32, false, false);
        texture.LoadRawTextureData(pixels);
        texture.Apply(false, false);
        StoreThumbnail(key, texture);
        return true;
    }

    private void StoreThumbnail(string key, Texture2D texture)
    {
        texture.name = $"Enhanced Spectator Fear Thumbnail {key}";
        texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, ThumbnailSize, ThumbnailSize), new Vector2(0.5f, 0.5f), ThumbnailSize);
        sprite.name = $"Enhanced Spectator Fear Card {key}";
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        RemoveThumbnail(key);
        while (_sprites.Count >= MaxCachedThumbnails && _lru.First != null) RemoveThumbnail(_lru.First.Value);
        _sprites[key] = sprite;
        _lruNodes[key] = _lru.AddLast(key);
        _retryAfter.Remove(key);
        Revision++;
    }

    private void RemoveThumbnail(string key)
    {
        if (_sprites.TryGetValue(key, out var old) && old != null)
        { if (old.texture != null) UnityEngine.Object.Destroy(old.texture); UnityEngine.Object.Destroy(old); }
        _sprites.Remove(key);
        if (_lruNodes.TryGetValue(key, out var node)) { _lru.Remove(node); _lruNodes.Remove(key); }
        // An evicted native asset may be read from disk again without another model render.
        _cacheChecked.Remove(key);
    }
}
