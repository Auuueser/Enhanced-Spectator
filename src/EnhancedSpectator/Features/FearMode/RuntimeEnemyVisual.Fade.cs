using UnityEngine;

namespace EnhancedSpectator.Features.FearMode;

public sealed partial class RuntimeEnemyVisual
{
    private ModelCameraFade? _cameraFade;
    private readonly ModelTargetFadeState _targetFade = new ModelTargetFadeState();
    internal float FadeOpacity => _targetFade.CurrentOpacity;
    internal void SeedFadeOpacity(float opacity) => _targetFade.SeedOpacity(opacity);
    /// <summary>Owner's watched teammate, independent of the viewer's camera.</summary>
    public void SetWatchedTarget(ulong? clientId, ulong? slotId) { _targetFade.ClientId = clientId; _targetFade.SlotId = slotId; }
    /// <summary>Prepares the selected fade backend outside render callbacks.</summary>
    public void PrepareFadeMaterials()
    {
        if (_disposed) return;
        if (_cameraFade == null) _cameraFade = new ModelCameraFade(_renderers, ModelKey);
        _cameraFade.Prepare();
    }
    /// <summary>Applies continuous transparency only to this camera, preserving full-opacity originals.</summary>
    public void BeginCameraFade(bool enabled)
    {
        RestoreCameraFade();
        if (_disposed || _root == null) return;
        float opacity = _targetFade.Update(_root.transform.position, enabled, _cameraFade?.Ready == true, ModelKey, _root.transform, _fadeEnvelope);
        if (enabled) _cameraFade?.Begin(opacity);
    }
    /// <summary>Immediately restores originals and clears the previous transition on opt-out.</summary>
    public void DisableCameraFade()
    {
        _cameraFade?.Disable();
        _targetFade.Update(Vector3.zero, false);
    }
    /// <summary>Restores exact original materials.</summary>
    public void RestoreCameraFade() => _cameraFade?.Restore();
    /// <summary>Ends only this camera's pending HDRP request after its draw completes.</summary>
    public void EndCameraFade(Camera camera) => _cameraFade?.EndCamera(camera);
    private void DisposeFadeMaterials() { _cameraFade?.Dispose(); _cameraFade = null; }
}
