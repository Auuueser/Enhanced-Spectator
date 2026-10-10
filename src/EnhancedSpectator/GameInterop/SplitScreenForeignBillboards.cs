using System.Collections.Generic;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// Other mods' world-space panels that face the game's active camera, turned toward each split-screen camera just
/// before it draws, as the split-screen does with its own name tags: facing the one active camera, they are seen from
/// the side, slanted or squashed, in every other view. The mod turns them back in its own next update.
/// Player_Status_Bars 0.2.x: a world-space canvas per player, "OtherPlayerStatusBar_n", turned in its Update.
/// </summary>
internal static class SplitScreenForeignBillboards
{
    private const string StatusBarPrefix = "OtherPlayerStatusBar_";
    private const float FindSeconds = 1;
    private static readonly List<Transform> Panels = new();
    private static float _foundAt = float.NegativeInfinity;
    /// <summary>
    /// Whether Player_Status_Bars is loaded, read at the first draw: plugins are all loaded before a split-screen
    /// opens, and without the mod there is nothing to look up every second.
    /// </summary>
    internal static bool? Installed;

    internal static void Face(Camera camera)
    {
        Installed ??= System.Array.Exists(System.AppDomain.CurrentDomain.GetAssemblies(), a => a.GetName().Name == "PlayerStatusBars");
        if (Installed == false) return;
        // Panels come and go with players: looked up again every second.
        if (Time.unscaledTime - _foundAt > FindSeconds) Find();
        var rotation = Quaternion.LookRotation(camera.transform.forward, camera.transform.up);
        foreach (var panel in Panels) if (panel != null) panel.rotation = rotation;
    }

    private static void Find()
    {
        _foundAt = Time.unscaledTime;
        Panels.Clear();
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (canvas.renderMode == RenderMode.WorldSpace && canvas.name.StartsWith(StatusBarPrefix, System.StringComparison.Ordinal))
                Panels.Add(canvas.transform);
    }
}
