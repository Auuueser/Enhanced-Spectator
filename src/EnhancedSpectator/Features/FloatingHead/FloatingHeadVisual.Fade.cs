using EnhancedSpectator.Features.FearMode;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnhancedSpectator.Features.FloatingHead;

public sealed partial class FloatingHeadVisual
{
    private ModelCameraFade? _headFade;
    private bool _fadeNearby;
    private bool _reportedIsolatedPoseUpdate;
    private readonly ModelTargetFadeState _targetFade = new ModelTargetFadeState();
    // The name's own copy of the proximity fade: it does not wait for the head's fade backend, which never readies
    // while an enemy model stands in for the head (the head is then not drawn at all).
    private readonly ModelTargetFadeState _nameFade = new ModelTargetFadeState(publishes: false);
    /// <summary>Watched-body identity associated with this owner's model.</summary>
    public void SetWatchedTarget(ulong? clientId, ulong? slotId)
    { _targetFade.ClientId = _nameFade.ClientId = clientId; _targetFade.SlotId = _nameFade.SlotId = slotId; }
    /// <summary>The owner, and the leader whose formation it stands in (it fades with that leader, name included).</summary>
    internal void SetParty(ulong owner, ulong? leader)
    { _targetFade.Owner = _nameFade.Owner = owner; _targetFade.Leader = _nameFade.Leader = leader; }
    /// <summary>Viewer-local preference, also restoring shared-material vanilla heads immediately.</summary>
    public bool FadeNearby
    {
        get => _fadeNearby;
        set
        {
            if (_fadeNearby == value) return;
            _fadeNearby = value;
            if (!value) { _headFade?.Disable(); _targetFade.Update(Vector3.zero, false); _nameFade.Update(Vector3.zero, false); }
        }
    }
    /// <summary>Opacity ceiling independent of the proximity fade, e.g. a speaking spectator revealed during idle centering.</summary>
    internal float OpacityCap { get; set; } = 1;
    private bool Capped => OpacityCap < .999f;
    /// <summary>Not yet shown, and its fade still being set up: shown now it would draw opaque for a frame or two.</summary>
    internal bool FadePending => !_gameObject.activeSelf && _headFade?.Pending == true;
    /// <summary>Budgeted preparation from LateUpdate only, never from a render callback.</summary>
    public void PrepareCameraFade() { if (!_disposed && (FadeNearby || Capped)) _headFade?.Prepare(); }
    private void RegisterFade()
    {
        // RuntimeDetachedHead intentionally has _material == null; fade its renderer materials too.
        if (_meshRenderer != null) _headFade = new ModelCameraFade(new Renderer[] { _meshRenderer }, "Default");
        RenderPipelineManager.beginCameraRendering += BeginHeadFade;
        RenderPipelineManager.endCameraRendering += EndHeadFade;
    }
    private void BeginHeadFade(ScriptableRenderContext context, Camera camera)
    {
        _headFade?.Trace("head-camera-begin", camera);
        _headFade?.Restore();
        if (_disposed) return;
        // A split-screen small view, drawn by hand, shows the name unfaded until it is drawn.
        if (camera == SplitScreenCameraContext.TileCamera && _nameTag != null && _nameViewFade < .999f)
        { _nameTag.SetFade(1); _nameFadeCamera = camera; }
        if (!LethalCompanyFearViewCamera.IsActiveView(camera) || !_gameObject.activeInHierarchy) return;
        float opacity = _targetFade.Update(_gameObject.transform.position, FadeNearby, _headFade?.Ready == true, "Default", _gameObject.transform, _meshFilter?.sharedMesh != null ? _meshFilter.sharedMesh.bounds : (Bounds?)null);
        if (FadeNearby || Capped) _headFade?.Begin(Mathf.Min(opacity, OpacityCap));
        _emote?.Face(camera);
    }
    private Camera? _nameFadeCamera;
    private void EndHeadFade(ScriptableRenderContext context, Camera camera)
    {
        _headFade?.EndCamera(camera);
        if (camera != _nameFadeCamera) return;
        _nameTag?.SetFade(_nameViewFade);
        _nameFadeCamera = null;
    }

    // The name's fade in the player's own view.
    private float _nameViewFade = 1;
    /// <summary>
    /// Before rendering (from the visuals' LateUpdate): the name follows the model's fade in the player's own view
    /// (head or enemy model alike), never below its readable minimum. Unlike the head's render-pass fade, a name is
    /// ordinary renderer data, which the game takes before its cameras draw: set inside a camera callback, the fade
    /// missed that frame and the name never faded in game.
    /// </summary>
    internal void UpdateNameFade()
    {
        if (_disposed || _nameTag == null) return;
        _nameViewFade = LethalCompanyFearViewCamera.HasFadingView && _gameObject.activeInHierarchy
            ? Mathf.Min(_nameFade.Update(_gameObject.transform.position, FadeNearby, true, "Name"), OpacityCap) : 1;
        if (_nameFadeCamera == null) _nameTag.SetFade(_nameViewFade);
    }
    private void ObserveIsolatedPoseUpdate()
    {
        _headFade?.Trace("head-pose");
        if (_reportedIsolatedPoseUpdate || !ModLog.IsDebugEnabled || _headFade?.IsIsolated != true) return;
        _reportedIsolatedPoseUpdate = true;
        ModLog.Debug("NativeFade default-head pose updated during camera isolation; isolation retained.");
    }
    private void UnregisterFade()
    {
        _headFade?.Dispose(); _headFade = null;
        RenderPipelineManager.beginCameraRendering -= BeginHeadFade;
        RenderPipelineManager.endCameraRendering -= EndHeadFade;
    }
}
