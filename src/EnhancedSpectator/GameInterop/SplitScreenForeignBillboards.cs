using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// Player_Status_Bars (0.2.x) in the split-screen. It keeps a world-space canvas per other player, named
/// "PlayerStatusBar_{playerClientId}", turned toward the game's active camera in its Update and shown within its
/// display distance of that camera. Each split-screen camera sees them as its own: just before it draws, every bar is
/// turned toward it (facing the one active camera they would be seen slanted from the side), and a bar shown for being
/// near another view is hidden in a view it is far from. The mod decides its distance from the nearest split-screen
/// camera (see the patch module), so a bar near any view stays shown and kept up to date; it turns and shows them
/// again in its own next update.
/// </summary>
internal static class SplitScreenForeignBillboards
{
    private const string StatusBarPrefix = "PlayerStatusBar_";
    private const float FindSeconds = 1, CameraSeconds = 1;

    private struct Panel
    {
        internal Transform Transform;
        internal Canvas Canvas;
        internal Transform? Target;
        internal bool Shown;
    }

    private static readonly List<Panel> Panels = new();
    // Each split-screen camera's place when it last drew: views draw at their own rates, so one not drawn this frame
    // still counts where it was for a moment.
    private static readonly Dictionary<int, (Vector3 Position, float At)> Cameras = new();
    private static readonly List<int> Expired = new();
    private static float _foundAt = float.NegativeInfinity, _maxDistanceSqr = float.MaxValue;
    private static int _frame = -1;

    /// <summary>
    /// Whether Player_Status_Bars is loaded, read at the first draw: plugins are all loaded before a split-screen
    /// opens, and without the mod there is nothing to look up every second.
    /// </summary>
    internal static bool? Installed;

    /// <summary>The mod's display distance (its own setting), set when its distance check is patched.</summary>
    internal static Func<float>? MaxDistance;

    internal static void Face(Camera camera)
    {
        Installed ??= Array.Exists(AppDomain.CurrentDomain.GetAssemblies(), a => a.GetName().Name == "PlayerStatusBars");
        if (Installed == false) return;
        var at = camera.transform.position;
        Cameras[camera.GetInstanceID()] = (at, Time.unscaledTime);
        if (_frame != Time.frameCount)
        {
            // The mod has decided this frame's bars in its Update: which are shown is read before any view hides one.
            _frame = Time.frameCount;
            // Panels come and go with players: looked up again every second.
            if (Time.unscaledTime - _foundAt > FindSeconds) Find();
            _maxDistanceSqr = MaxDistance != null ? MaxDistance() * MaxDistance() : float.MaxValue;
            for (int i = 0; i < Panels.Count; i++)
            {
                var panel = Panels[i];
                panel.Shown = panel.Canvas != null && panel.Canvas.enabled;
                Panels[i] = panel;
            }
        }
        var rotation = Quaternion.LookRotation(camera.transform.forward, camera.transform.up);
        foreach (var panel in Panels)
        {
            if (panel.Transform == null) continue;
            panel.Transform.rotation = rotation;
            if (panel.Shown)
                panel.Canvas.enabled = panel.Target == null || (panel.Target.position - at).sqrMagnitude <= _maxDistanceSqr;
        }
    }

    /// <summary>
    /// The squared distance from <paramref name="position"/> to the nearest split-screen camera that drew within the
    /// last second; false when none did (no split-screen).
    /// </summary>
    internal static bool TryNearestCamera(Vector3 position, out float distanceSqr)
    {
        distanceSqr = float.MaxValue;
        foreach (var camera in Cameras.Values)
            if (Time.unscaledTime - camera.At <= CameraSeconds)
                distanceSqr = Mathf.Min(distanceSqr, (camera.Position - position).sqrMagnitude);
        return distanceSqr < float.MaxValue;
    }

    private static void Find()
    {
        _foundAt = Time.unscaledTime;
        Expired.Clear();
        foreach (var camera in Cameras)
            if (Time.unscaledTime - camera.Value.At > CameraSeconds) Expired.Add(camera.Key);
        foreach (var key in Expired) Cameras.Remove(key);
        Panels.Clear();
        PlayerControllerB[] players = StartOfRound.Instance.allPlayerScripts;
        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas.renderMode != RenderMode.WorldSpace || !canvas.name.StartsWith(StatusBarPrefix, StringComparison.Ordinal)) continue;
            // The bar's player, by the client id in its name.
            Transform? target = null;
            if (ulong.TryParse(canvas.name.Substring(StatusBarPrefix.Length), out ulong clientId))
                foreach (var player in players)
                    if (player.playerClientId == clientId) { target = player.transform; break; }
            Panels.Add(new Panel { Transform = canvas.transform, Canvas = canvas, Target = target });
        }
    }
}
