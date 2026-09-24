using EnhancedSpectator.Features.FearMode;
using EnhancedSpectator.GameInterop;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnhancedSpectator.Features.FloatingHead;

public sealed partial class FloatingHeadVisual
{
    private ModelCameraFade? _headFade;
    private bool _fadeNearby;
    private readonly ModelTargetFadeState _targetFade = new ModelTargetFadeState();
    /// <summary>Watched-body identity associated with this owner's model.</summary>
    public void SetWatchedTarget(ulong? clientId, ulong? slotId) { _targetFade.ClientId = clientId; _targetFade.SlotId = slotId; }
    /// <summary>Viewer-local preference, also restoring shared-material vanilla heads immediately.</summary>
    public bool FadeNearby
    {
        get => _fadeNearby;
        set
        {
            if (_fadeNearby == value) return;
            _fadeNearby = value;
            if (!value) { _headFade?.Disable(); _targetFade.Update(Vector3.zero, false); }
        }
    }
    /// <summary>Budgeted preparation from LateUpdate only, never from a render callback.</summary>
    public void PrepareCameraFade() { if (!_disposed && FadeNearby) _headFade?.Prepare(); }
    private void RegisterFade()
    {
        // RuntimeDetachedHead intentionally has _material == null; fade its renderer materials too.
        if (_meshRenderer != null) _headFade = new ModelCameraFade(new Renderer[] { _meshRenderer }, "Default");
        RenderPipelineManager.beginCameraRendering += BeginHeadFade;
        RenderPipelineManager.endCameraRendering += EndHeadFade;
    }
    private void BeginHeadFade(ScriptableRenderContext context, Camera camera)
    {
        _headFade?.Restore();
        if (!LethalCompanyFearViewCamera.IsActiveView(camera) || _disposed || !_gameObject.activeInHierarchy) return;
        float opacity = _targetFade.Update(_gameObject.transform.position, FadeNearby, _headFade?.Ready == true, "Default", _gameObject.transform, _meshFilter?.sharedMesh != null ? _meshFilter.sharedMesh.bounds : (Bounds?)null);
        if (FadeNearby) _headFade?.Begin(opacity);
    }
    private void EndHeadFade(ScriptableRenderContext context, Camera camera) => _headFade?.EndCamera(camera);
    private void RestoreHeadFade() => _headFade?.Restore();
    private void UnregisterFade()
    {
        _headFade?.Dispose(); _headFade = null;
        RenderPipelineManager.beginCameraRendering -= BeginHeadFade;
        RenderPipelineManager.endCameraRendering -= EndHeadFade;
    }
}
