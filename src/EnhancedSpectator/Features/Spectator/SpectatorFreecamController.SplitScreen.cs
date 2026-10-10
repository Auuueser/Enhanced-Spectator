using EnhancedSpectator.GameInterop;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

public sealed partial class SpectatorFreecamController
{
    private bool _splitScreenActive, _splitScreenFocused;
    private SpectatorCameraMode? _splitScreenMode;
    private Camera? _previewCamera;
    private Transform? _previewAnchor;
    private int _splitScreenHandledInputFrame = -1;

    internal void SuppressModeInputThisFrame() => _splitScreenHandledInputFrame = Time.frameCount;

    /// <summary>
    /// Watching together: the large view shows the followed spectator's camera, so this player's camera input (look,
    /// move, wheel, mode keys, recentring) is ignored until they stop following.
    /// </summary>
    internal bool Mirrored { get => _state.Mirrored; set => _state.Mirrored = value; }

    /// <summary>Recentres the camera behind its player, as the recentre key does.</summary>
    internal void RecenterView()
    {
        _monitorDirector.Clear();
        ClearFollowRig();
        _recenterRequested = true;
    }

    internal bool IsPreview => _previewCamera != null;
    internal SpectatorCameraMode? SplitScreenMode => _splitScreenMode;
    internal bool UsesFirstPersonRendering => _state.Mode == SpectatorCameraMode.FirstPerson
        || (_state.Mode == SpectatorCameraMode.Monitor && _monitorEyeFallback);
    private bool CanRenderSplitScreenMode => _splitScreenFocused && _splitScreenMode.HasValue
        && (_splitScreenMode != SpectatorCameraMode.Monitor || CanUseMonitorView);

    internal void SetSplitScreenContext(bool active, bool focused)
    {
        _splitScreenActive = active;
        _splitScreenFocused = active && focused;
        if (active && !focused) SuspendSplitScreenView();
    }

    internal void SetSplitScreenMode(SpectatorCameraMode? mode, int style)
    {
        _splitScreenMode = mode;
        SelectMonitorStyle(style);
        if (!mode.HasValue || !CanRenderSplitScreenMode) { SuspendSplitScreenView(); return; }
        if (!_state.UserEnabled || _state.Mode != mode.Value) SetCameraMode(mode.Value, true);
    }

    private void SuspendSplitScreenView()
    {
        if (!_state.UserEnabled && _ownedCamera == null) return;
        _state.UserEnabled = false;
        Deactivate(clearAnchor: true);
    }

    // This is a camera evaluation context, never a synthetic dead-player snapshot.
    // The same controller and photography rig serve production and preview exclusively.
    internal void BeginPreview(IGameSpectatorAdapter adapter, Camera camera, Transform anchor)
    {
        SuspendSplitScreenView();
        _adapter = adapter;
        _previewCamera = camera;
        _previewAnchor = anchor;
        _state.Mode = SpectatorCameraMode.Freecam;
        _state.UserEnabled = false;
        _entryRecenterPending = false;
        InitializePoseFromCamera(camera, anchor);
    }

    internal void UpdatePreviewTarget(Transform anchor)
    {
        if (_previewAnchor == anchor) return;
        SuspendSplitScreenView();
        _previewAnchor = anchor;
        if (_previewCamera != null) InitializePoseFromCamera(_previewCamera, anchor);
    }

    internal void EndPreview()
    {
        if (!IsPreview) return;
        SuspendSplitScreenView();
        _previewCamera = null;
        _previewAnchor = null;
        _adapter = _productionAdapter;
        ResetForNonSpectator();
    }

    internal void RestoreSplitScreenMode(SpectatorCameraMode mode, bool enabled)
    {
        SuspendSplitScreenView();
        if (enabled && TryGetEligibleSnapshot(out _)) SetCameraMode(mode, true);
        else { _state.Mode = mode; _state.UserEnabled = enabled; }
    }

    /// <summary>
    /// Preview only: puts the self third-person ghost at a world pose, as if it had been flown there (the demo uses
    /// this to have the ghost face the watched body, which a solo preview otherwise leaves out of the shot).
    /// </summary>
    internal void PlacePreviewGhost(Vector3 position, Quaternion rotation)
    {
        if (_previewAnchor == null) return;
        _state.Offset = position - _previewAnchor.position;
        ClampOffset();
        SetYawPitchFromRotation(rotation);
        _state.Rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        _state.RepresentationRotation = SpectatorThirdPersonCameraRules.ResolveRepresentationRotation(_state.Rotation);
        _smoothedPosition = _previewAnchor.position + _state.Offset; _hasSmoothedPosition = true; _smoothVelocity = Vector3.zero;
        _previousAnchorPosition = _previewAnchor.position; _hasPreviousAnchorPosition = true;
        _collisionDistance = -1f; _hasPose = true;
    }

    private void ApplyPreviewPose()
    {
        if (_previewCamera == null || _previewAnchor == null || !CanRenderSplitScreenMode) return;
        SpectatorCameraMode mode = _splitScreenMode.GetValueOrDefault();
        if (!_state.UserEnabled || _state.Mode != mode) SetCameraMode(mode, false);
        if (!_hasPose) InitializePoseFromCamera(_previewCamera, _previewAnchor);
        ApplyCameraTransform(_previewCamera, _previewAnchor);
        _state.IsActive = true;
    }
}
