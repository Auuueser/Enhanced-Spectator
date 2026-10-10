using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// Steam profile pictures for the audience row, by Steam id. The game's loader is async and writes into whatever
/// image it was handed when the picture arrives, creating a new texture each call; so each id is requested once,
/// into an image this cache owns on a hidden object that survives scene changes. A request that finishes late can
/// then only fill its own entry, never an avatar chip that has been reused for someone else, and every player met
/// costs one small texture for the run instead of one per chip refresh. Chips read the picture from here.
/// </summary>
internal static class SteamAvatarCache
{
    private static readonly Dictionary<ulong, RawImage> Images = new Dictionary<ulong, RawImage>();
    private static Transform? _holder;

    /// <summary>The picture for a Steam id once it has arrived, otherwise null (it is requested on first ask).</summary>
    internal static Texture? Get(ulong steamId)
    {
        if (steamId == 0) return null;
        if (!Images.TryGetValue(steamId, out var image))
        {
            if (_holder == null)
            {
                var holder = new GameObject("EnhancedSpectator avatars") { hideFlags = HideFlags.HideAndDontSave };
                holder.SetActive(false); _holder = holder.transform;
            }
            image = new GameObject("Avatar " + steamId, typeof(RectTransform), typeof(RawImage)) { hideFlags = HideFlags.HideAndDontSave }.GetComponent<RawImage>();
            image.transform.SetParent(_holder, false);
            Images.Add(steamId, image);
            HUDManager.FillImageWithSteamProfile(image, steamId, false);
        }
        return image.texture;
    }
}
