using TMPro;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// The game's own HUD font for world text such as ghost names: vanilla's English glyphs, plus whatever fallback the HUD
/// font carries for other scripts (LC Chinese Project adds its Chinese font there). Read once the HUD exists.
/// </summary>
internal static class LethalCompanyWorldTextFont
{
    private static Material? _label;
    internal static TMP_FontAsset? Font => HUDManager.Instance != null ? HUDManager.Instance.spectatingPlayerText.font : null;
    /// <summary>The split-screen label look (thickened face, no outline) in that font; null before the HUD exists.</summary>
    internal static Material? LabelMaterial => _label != null ? _label : Font is { } font ? _label = SpectatorTextStyle.CreateLabelMaterial(font) : null;
}
