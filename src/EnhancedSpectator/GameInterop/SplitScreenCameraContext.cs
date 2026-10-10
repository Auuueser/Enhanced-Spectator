using GameNetcodeStuff;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

// Only a locally owned rendering context. It never changes the game's current player,
// active camera, listener or any network snapshot for a preview.
internal static class SplitScreenCameraContext
{
    internal static bool Active, Preview, Focused, Enhanced;
    internal static Camera? PrimaryCamera;
    // The small-view camera currently inside its manual Render call.
    internal static Camera? TileCamera;
    internal static PlayerControllerB? Target;
    internal static bool IsPrimary(Camera camera) => Active && camera == PrimaryCamera;
    internal static Camera? PreviewView => Active && Preview && Focused ? PrimaryCamera : null;
    /// <summary>Any split-screen view: the primary (focused or tiled) or a small view being drawn.</summary>
    internal static bool IsView(Camera camera) => Active && (camera == PrimaryCamera || camera == TileCamera);
    internal static void Clear()
    { Active = Preview = Focused = Enhanced = false; PrimaryCamera = TileCamera = null; Target = null; }
}
