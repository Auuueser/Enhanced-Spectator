using UnityEngine;
using UnityEngine.UI;
using EnhancedSpectator.Logging;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// The game's own speaker glyph (sprite `SpeakingSymbol`, a white mask): the one on the ESC menu's microphone setting
/// and on the spectate list's talking players. Read from the spectate-box prefab's `SpeakerIcon` image once the HUD exists.
/// </summary>
internal static class LethalCompanySpeakerIcon
{
    private static Sprite? _sprite;
    private static bool _reportedMissing;

    internal static Sprite? Sprite
    {
        get
        {
            if (_sprite != null) return _sprite;
            var hud = HUDManager.Instance;
            if (hud == null) return null;
            // This optional HUD element can be replaced or removed by another mod.
            var prefab = hud.spectatingPlayerBoxPrefab;
            var icon = prefab != null ? prefab.transform.Find("SpeakerIcon") : null;
            var graphic = icon != null ? icon.GetComponent<Image>() : null;
            _sprite = graphic != null ? graphic.sprite : null;
            if (_sprite == null && !_reportedMissing)
            {
                _reportedMissing = true;
                ModLog.Warning("Spectator speaker icon is unavailable in the HUD prefab; names will display without it.");
            }
            return _sprite;
        }
    }
}
