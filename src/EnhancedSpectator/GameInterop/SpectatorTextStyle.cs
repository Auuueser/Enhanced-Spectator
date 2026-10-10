using TMPro;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>Shared by the split-screen labels and the watch roster.</summary>
internal static class SpectatorTextStyle
{
    // The 3270 font's Latin strokes are about a tenth of an em, so at 12-16 px they fall to a pixel or less
    // and break apart. Labels use a copy of the font's own material with a slightly dilated face, and
    // without outline, underlay or glow in case another mod swapped in a decorated material. The shared
    // font material is never changed.
    internal static Material CreateLabelMaterial(TMP_FontAsset font)
    {
        var material = new Material(font.material) { name = "Enhanced Spectator label text", hideFlags = HideFlags.HideAndDontSave };
        material.SetFloat("_OutlineWidth", 0); material.SetFloat("_OutlineSoftness", 0); material.SetFloat("_FaceDilate", .2f);
        material.DisableKeyword("UNDERLAY_ON"); material.DisableKeyword("UNDERLAY_INNER"); material.DisableKeyword("GLOW_ON");
        return material;
    }

    /// <summary>Pixel-sized canvases keep their 1080p physical size on larger screens instead of shrinking with the pixel grid.</summary>
    internal static float UiScale(Vector2 viewport) => Mathf.Clamp(viewport.y / 1080f, 1, 2);
}
