using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace EnhancedSpectator.GameInterop;

// HDRP reads directional cascades from each camera's own volume stack. This volume carries
// weight only for the duration of a small-view Camera.Render, so the main view keeps the game's shadows.
internal sealed class SplitScreenTileVolume
{
    // Above every V81 volume (highest observed priority 40).
    private const float Priority = 10000;
    private GameObject? _host;
    private Volume? _volume;
    private HDShadowSettings? _shadows;
    private VolumeProfile? _profile;

    internal void Begin(Camera camera, int cascades)
    {
        if (cascades <= 0) return;
        if (_volume == null)
        {
            _host = new GameObject("Enhanced Spectator Tile Volume") { hideFlags = HideFlags.HideAndDontSave };
            // Volumes are filtered by layer when they register, so pick a layer the copied camera evaluates.
            int mask = camera.TryGetComponent<HDAdditionalCameraData>(out var data) ? data.volumeLayerMask.value : 1;
            for (int layer = 0; layer < 32; layer++) if ((mask & 1 << layer) != 0) { _host.layer = layer; break; }
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.hideFlags = HideFlags.HideAndDontSave;
            _shadows = _profile.Add<HDShadowSettings>();
            _volume = _host.AddComponent<Volume>();
            _volume.isGlobal = true; _volume.priority = Priority; _volume.weight = 0; _volume.sharedProfile = _profile;
        }
        _shadows!.cascadeShadowSplitCount.Override(cascades);
        _volume.weight = 1;
    }

    internal void End()
    {
        if (_volume != null) _volume.weight = 0;
    }

    internal void Dispose()
    {
        if (_host != null) Object.Destroy(_host);
        if (_profile != null)
        {
            foreach (var component in _profile.components) Object.Destroy(component);
            Object.Destroy(_profile);
        }
        _host = null; _volume = null; _shadows = null; _profile = null;
    }
}
