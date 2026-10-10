using UnityEngine;

namespace EnhancedSpectator.Features.FearMode;

public sealed partial class RuntimeEnemyVisual
{
    private ModelCameraFade? _cameraFade;
    private readonly ModelTargetFadeState _targetFade = new ModelTargetFadeState();
    internal float FadeOpacity => _targetFade.CurrentOpacity;
    internal void SeedFadeOpacity(float opacity) => _targetFade.SeedOpacity(opacity);
    /// <summary>Opacity ceiling independent of the proximity fade, e.g. a speaking spectator revealed during idle centering.</summary>
    internal float OpacityCap { get; set; } = 1;
    /// <summary>Not yet shown, and its fade still being set up: shown now it would draw opaque for a frame or two.</summary>
    internal bool FadePending => _root != null && !_root.activeSelf && _cameraFade?.Pending == true;
    /// <summary>Owner's watched teammate, independent of the viewer's camera.</summary>
    public void SetWatchedTarget(ulong? clientId, ulong? slotId) { _targetFade.ClientId = clientId; _targetFade.SlotId = slotId; }
    /// <summary>The owner, and the leader whose formation it stands in (it fades with that leader).</summary>
    internal void SetParty(ulong owner, ulong? leader) { _targetFade.Owner = owner; _targetFade.Leader = leader; }
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
        if (_disposed || _root == null || !_root.activeInHierarchy) return;
        float opacity = _targetFade.Update(_root.transform.position, enabled, _cameraFade?.Ready == true, ModelKey, _root.transform, _fadeEnvelope);
        if (enabled || OpacityCap < .999f) _cameraFade?.Begin(Mathf.Min(opacity, OpacityCap));
    }
    /// <summary>Immediately restores originals and clears the previous transition on opt-out.</summary>
    public void DisableCameraFade()
    {
        _cameraFade?.Disable();
        _targetFade.Update(Vector3.zero, false);
    }
    // Reappearing starts at the circle's opacity (see ModelTargetFadeState.SnapNext).
    private void SnapFade() => _targetFade.SnapNext();
    /// <summary>Restores exact original materials.</summary>
    public void RestoreCameraFade() => _cameraFade?.Restore();
    /// <summary>Ends only this camera's pending HDRP request after its draw completes.</summary>
    public void EndCameraFade(Camera camera) => _cameraFade?.EndCamera(camera);
    private void DisposeFadeMaterials() { _cameraFade?.Dispose(); _cameraFade = null; }
}
