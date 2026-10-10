using System;
using System.Collections.Generic;
using EnhancedSpectator.Config;
using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.Features.SplitScreen;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace EnhancedSpectator.GameInterop;

/// <summary>Owns local camera outputs; renders complete views serially outside SRP callbacks.</summary>
internal sealed class LethalCompanySplitScreenRenderAdapter : IGameSplitScreenRenderAdapter
{
    private sealed class View
    {
        internal RenderTexture? Texture;
        internal Camera? Camera;
        internal HDAdditionalCameraData? Data;
        internal bool TransitionPending;
        internal int PosedFrame = -1, LodHeight;
    }

    private readonly EnhancedSpectatorConfig _config;
    private readonly Dictionary<SplitScreenKey, View> _views = new Dictionary<SplitScreenKey, View>();
    private readonly List<RenderTexture> _retired = new List<RenderTexture>();
    private readonly List<Vector3> _roomSamples = new List<Vector3>();
    private readonly LethalCompanySpectatorAdapter _previewAdapter = new LethalCompanySpectatorAdapter();
    private readonly SplitScreenEnvironmentScope _environment = new SplitScreenEnvironmentScope();
    private readonly SplitScreenPlayerBodyScope _body = new SplitScreenPlayerBodyScope();
    private readonly SplitScreenTileVolume _tileVolume = new SplitScreenTileVolume();
    // Effects with no consumer in a small view: anti-aliasing is off and motion blur only appears in the insanity
    // filter, which would smear across throttled frames anyway. V81's always-on depth of field only blurs 0-0.5 m.
    // The borrowed main camera turns them off too while split-screen runs: with them on, the jetpack's heat haze
    // (distortion) in its view showed the HUD behind it, other mods' panels included. Found in game (2026-10-07):
    // distortion was clean in our copied views and in the main view with these off, wrong only with them on.
    private static readonly FrameSettingsField[] TileDisabled =
    {
        FrameSettingsField.MotionVectors, FrameSettingsField.ObjectMotionVectors, FrameSettingsField.TransparentsWriteMotionVector,
        FrameSettingsField.MotionBlur, FrameSettingsField.DepthOfField
    };
    private bool _savedCustomSettings;
    private readonly bool[] _savedOverride = new bool[5], _savedEnabledField = new bool[5];
    private Camera? _primaryCamera;
    private SpectatorFreecamController? _controller;
    private PlayerControllerB? _primaryTarget;
    private SplitScreenParticipant? _primary;
    private RenderTexture? _savedTexture;
    private Rect _savedRect;
    private float _savedAspect;
    private bool _savedEnabled, _active, _preview, _focused, _savedEnhanced;
    private bool _savedPersistentHistory, _reposePending;
    private SpectatorCameraMode _savedMode;
    private SpectatorCameraMode? _observedMode;
    private int _observedStyle, _tileCascades;
    // The followed spectator's camera as last shown: eased toward each of their poses, which arrive a few times a second.
    private const float MirrorSeconds = .1f;
    private bool _mirroring;
    private Vector3 _mirrorPosition;
    private Quaternion _mirrorRotation;
    private float _mirrorFov;

    internal LethalCompanySplitScreenRenderAdapter(EnhancedSpectatorConfig config) { _config = config; }
    public SpectatorCameraMode? PrimaryMode => _controller?.SplitScreenMode;
    public float PrimaryTransitionOpacity => _focused && SplitScreenCameraContext.Enhanced
        ? _controller?.TransitionOpacity ?? 0 : 0;
    public int CameraCount
    {
        get
        {
            int count = _primaryCamera != null ? 1 : 0;
            foreach (var pair in _views) if (pair.Value.Camera != null && pair.Key != _primary?.Key) count++;
            return count;
        }
    }
    public long TextureBytes
    {
        get
        {
            long bytes = 0;
            foreach (var view in _views.Values) if (view.Texture != null) bytes += (long)view.Texture.width * view.Texture.height * 4;
            foreach (var texture in _retired) bytes += (long)texture.width * texture.height * 4;
            return bytes;
        }
    }

    public bool TryBegin(bool preview)
    {
        if (_active) return _preview == preview;
        var round = StartOfRound.Instance;
        var local = round != null ? round.localPlayerController : null;
        _controller = SpectatorFreecamController.Current;
        if (round == null || local == null || _controller == null || round.spectateCamera == null) return false;
        // The game-over camera (all crew dead, or leaving early or at midnight) is allowed: the split-screen then shows
        // the ship taking off through it.
        if (!preview && (!local.isPlayerDead || !local.hasBegunSpectating || local.isInGameOverAnimation > 0
            || round.activeCamera != round.spectateCamera)) return false;
        _savedMode = _controller.State.Mode;
        _savedEnhanced = _controller.State.UserEnabled;
        _controller.SetSplitScreenContext(true, false);
        _preview = preview;
        _primaryCamera = preview ? CreateCamera(round.spectateCamera, "Enhanced Spectator Preview Main", out _) : round.spectateCamera;
        _savedTexture = _primaryCamera.targetTexture;
        _savedRect = _primaryCamera.rect;
        _savedAspect = _primaryCamera.aspect;
        _savedEnabled = _primaryCamera.enabled;
        _primaryCamera.enabled = false;
        if (_primaryCamera.TryGetComponent<HDAdditionalCameraData>(out var data))
        {
            _savedPersistentHistory = data.hasPersistentHistory; data.hasPersistentHistory = true;
            _savedCustomSettings = data.customRenderingSettings;
            for (int i = 0; i < TileDisabled.Length; i++)
            {
                _savedOverride[i] = data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)TileDisabled[i]];
                _savedEnabledField[i] = data.renderingPathCustomFrameSettings.IsEnabled(TileDisabled[i]);
            }
            DisableTileEffects(data);
        }
        SplitScreenCameraContext.Active = true;
        SplitScreenCameraContext.Preview = preview;
        SplitScreenCameraContext.PrimaryCamera = _primaryCamera;
        _active = true;
        return true;
    }

    public void BindPrimary(SplitScreenParticipant? participant, bool focused)
    {
        if (!_active) return;
        bool changed = _primary?.Key != participant?.Key || _primary?.Source != participant?.Source || _focused != focused;
        if (changed) { FreezePrimaryTexture(); SplitScreenInteriorVisibility.InvalidateRooms(); }
        // A view keeps the angle it is seen from as it moves between the main camera (large, or the heard one among
        // the tiles) and its own small-view camera; only the distance returns to vanilla's. One not seen yet starts
        // from behind its player.
        Quaternion? shown = changed && _primary.HasValue && _primaryTarget != null ? _primaryCamera!.transform.rotation : null;
        Quaternion? carried = !changed || !participant.HasValue ? null
            : participant.Value.Key == _primary?.Key ? shown : OrbitOf(participant.Value.Key).Current;
        if (shown.HasValue && _primary is { } left && left.Key != participant?.Key) OrbitOf(left.Key).Set(shown.Value);
        _primary = participant;
        _focused = focused && participant.HasValue;
        _primaryTarget = participant.HasValue ? Resolve(participant.Value.Source) : null;
        if (_primaryTarget != null)
        {
            // A new large-view player starts from a vanilla pose at that player, before the camera rig re-seeds
            // from the camera. Views switch far more often than normal spectating, so they never travel across
            // the map (or through walls) from the previous player; the picture crossfade hides the cut.
            if (changed)
            {
                if (carried.HasValue) _primaryOrbit.Set(carried.Value); else _primaryOrbit.Reset();
                // A large view opened anew turns from that angle with the mouse, at vanilla's default distance.
                if (_focused) { FacePivot(_primaryOrbit.Rotation(_primaryTarget)); _controller!.ResetVanillaDistance(); }
                ApplyVanillaPose(_primaryCamera!, _primaryTarget, PrimaryOrbit()); _reposePending = true;
            }
            if (_preview)
            {
                _previewAdapter.BindView(_primaryTarget, _primaryCamera!);
                if (_focused)
                {
                    if (!_controller!.IsPreview) _controller.BeginPreview(_previewAdapter, _primaryCamera!, LethalCompanySpectatorAdapter.ViewAnchor(_primaryTarget));
                    else _controller.UpdatePreviewTarget(LethalCompanySpectatorAdapter.ViewAnchor(_primaryTarget));
                }
                else _controller!.EndPreview();
            }
            else
            {
                var round = StartOfRound.Instance;
                var local = round.localPlayerController;
                if (local.spectatedPlayerScript != _primaryTarget)
                {
                    local.spectatedPlayerScript = _primaryTarget;
                    local.spectatedPlayerDeadTimer = 0;
                    round.SetPlayerSafeInShip();
                    local.SetSpectatedPlayerEffects(false);
                }
            }
        }
        else if (_preview) _controller!.EndPreview();
        _controller!.SetSplitScreenContext(true, _focused);
        SplitScreenCameraContext.Target = _primaryTarget;
        SplitScreenCameraContext.Focused = _focused;
    }

    public void SetPrimaryMode(SpectatorCameraMode? mode, int monitorStyle)
    {
        if (!_active) return;
        if (mode != PrimaryMode || monitorStyle != _observedStyle) FreezePrimaryTexture();
        _controller!.SetSplitScreenMode(mode, monitorStyle);
        _observedMode = mode;
        _observedStyle = monitorStyle;
    }

    public void FacePreviewGhost()
    {
        if (!_active || !_preview || _primaryTarget == null) return;
        // A couple of metres in front of the player and a little above, looking at their chest: the camera behind
        // the ghost then has the ghost in the foreground and the player's front beyond it.
        var body = _primaryTarget.transform;
        Vector3 chest = body.position + Vector3.up * 1.2f, ghost = chest + body.forward * 2.4f + Vector3.up * .4f;
        _controller!.PlacePreviewGhost(ghost, Quaternion.LookRotation(chest - ghost, Vector3.up));
    }


    public void UpdatePrimaryPose()
    {
        if (!_active || _primaryCamera == null || _primaryTarget == null) return;
        _primaryCamera.enabled = false;
        if (_observedMode != PrimaryMode || _observedStyle != _config.Camera.MonitorStyle.Value)
        {
            FreezePrimaryTexture();
            _observedMode = PrimaryMode;
            _observedStyle = _config.Camera.MonitorStyle.Value;
        }
        bool enhanced = _focused && PrimaryMode.HasValue
            && (PrimaryMode != SpectatorCameraMode.Monitor || _primaryTarget.isInsideFactory);
        SplitScreenCameraContext.Enhanced = enhanced;
        // The borrowed spectate camera hangs under vanilla's pivot, which only reaches the new player in its
        // LateUpdate; pose it again here, just before the rig re-seeds, so the seed is really at that player.
        if (_reposePending) { _reposePending = false; ApplyVanillaPose(_primaryCamera, _primaryTarget, PrimaryOrbit()); }
        _controller!.EvaluateSplitScreenPose();
        // The vanilla large view keeps the wheel-set distance of normal vanilla spectating; tiled (the audio tile is
        // drawn with this camera too) it is vanilla's 1.3 m like every other tile.
        if (!enhanced) ApplyVanillaPose(_primaryCamera, _primaryTarget, PrimaryOrbit(), _focused ? _controller.VanillaDistance : 1.3f);
        LethalCompanySpectatorPresentation.Tick();
    }

    public void MirrorPrimaryPose((Vector3 Position, Quaternion Rotation, float FieldOfView)? pose, bool following)
    {
        if (!_active) return;
        // Input ownership lasts until following ends, including gaps between target and pose packets.
        _controller!.Mirrored = following;
        if (!following || !_focused || _primaryCamera == null || _primaryTarget == null)
        {
            // Following over, the large view turns on from the angle it was last showing.
            if (_mirroring && _focused && !following) FacePivot(_mirrorRotation);
            _mirroring = false; return;
        }
        var camera = _primaryCamera!.transform;
        // Hold the current picture while a matching sample is still on its way.
        if (!_mirroring) { _mirrorPosition = camera.position; _mirrorRotation = camera.rotation; _mirrorFov = 0; _mirroring = true; }
        if (!pose.HasValue)
        {
            camera.SetPositionAndRotation(_mirrorPosition, _mirrorRotation);
            return;
        }
        // From the view as it was (our own camera) on the first frame, so following glides into place.
        float ease = 1 - Mathf.Exp(-Time.unscaledDeltaTime / MirrorSeconds);
        _mirrorPosition = Vector3.Lerp(_mirrorPosition, pose!.Value.Position, ease);
        _mirrorRotation = Quaternion.Slerp(_mirrorRotation, pose.Value.Rotation, ease);
        camera.SetPositionAndRotation(_mirrorPosition, _mirrorRotation);
        _mirrorFov = pose.Value.FieldOfView;
    }

    // The ship's furniture (the suits on their rack, the cupboard, ...) follows the ship in AutoParentToShip.LateUpdate,
    // which may run after ours: while the ship moves, the views would draw it where the ship was last frame, shaking.
    // It is moved now as the game is about to; the game's own camera draws after every LateUpdate and never sees this.
    private AutoParentToShip[] _furniture = System.Array.Empty<AutoParentToShip>();
    private float _furnitureFoundAt = float.NegativeInfinity;
    private Vector3 _shipPosition;
    private Quaternion _shipRotation;
    public void FollowShip()
    {
        var round = StartOfRound.Instance;
        var ship = round.elevatorTransform;
        if (ship.position == _shipPosition && ship.rotation == _shipRotation || round.suckingFurnitureOutOfShip)
        { _shipPosition = ship.position; _shipRotation = ship.rotation; return; }
        _shipPosition = ship.position; _shipRotation = ship.rotation;
        // Furniture is bought and placed during the round: looked up again every couple of seconds of ship motion.
        if (Time.unscaledTime - _furnitureFoundAt > 2)
        { _furniture = UnityEngine.Object.FindObjectsByType<AutoParentToShip>(FindObjectsSortMode.None); _furnitureFoundAt = Time.unscaledTime; }
        foreach (var piece in _furniture)
            if (piece != null && !piece.disableObject) piece.MoveToOffset();
    }

    public void SetTileShadowCascades(int cascades) => _tileCascades = cascades;

    public void RefreshVisibleRooms(IReadOnlyList<SplitScreenParticipant> participants)
    {
        var culler = StartOfRound.Instance?.occlusionCuller;
        // Rooms are re-resolved every 0.1 s; undrawn small views only need a pose for those samples.
        if (culler != null && SplitScreenInteriorVisibility.SampleDue) CollectRoomSamples(participants);
        if (culler != null) SpectatorInteriorVisibility.Refresh(culler);
    }

    private void CollectRoomSamples(IReadOnlyList<SplitScreenParticipant> participants)
    {
        _roomSamples.Clear();
        foreach (var participant in participants)
        {
            var player = Resolve(participant.Source);
            if (player == null) continue;
            Camera? camera = _primaryCamera;
            if (_primary?.Key != participant.Key)
            {
                camera = _views.TryGetValue(participant.Key, out var view) ? view.Camera : null;
                if (camera != null) { ApplyVanillaPose(camera, player, OrbitOf(participant.Key).Rotation(player)); view.PosedFrame = Time.frameCount; }
            }
            if (!player.isInsideFactory) continue;
            Vector3 subject = player.transform.position + Vector3.up * .5f;
            if (!_roomSamples.Contains(subject)) _roomSamples.Add(subject);
            if (camera != null) _roomSamples.Add(camera.transform.position);
        }
        SplitScreenInteriorVisibility.SetSamples(_roomSamples);
    }

    public Texture? RenderView(SplitScreenParticipant participant, bool primary, int width, int height)
    {
        if (!_active) return null;
        var target = Resolve(participant.Source);
        if (target == null) return GetTexture(participant.Key);
        var view = ViewFor(participant.Key, participant.Name, width, height);
        Camera camera;
        if (primary) camera = _primaryCamera!;
        else
        {
            if (view.Camera == null)
            {
                view.Camera = CreateCamera(StartOfRound.Instance.spectateCamera, "Enhanced Spectator Tile " + participant.Name, out view.Data);
                ConfigureTile(view.Data);
                view.LodHeight = 0;
            }
            camera = view.Camera;
            // LOD selection is resolution independent; match detail to the pixels this view actually has.
            if (view.LodHeight != height)
            { view.LodHeight = height; view.Data!.renderingPathCustomFrameSettings.lodBias = Mathf.Clamp(height / (float)Screen.height, .35f, 1); }
            // Room sampling may already have posed this camera this frame; reuse that pose and its raycast.
            if (view.PosedFrame != Time.frameCount) { ApplyVanillaPose(camera, target, OrbitOf(participant.Key).Rotation(target)); view.PosedFrame = Time.frameCount; }
        }
        camera.targetTexture = view.Texture;
        camera.rect = new Rect(0, 0, 1, 1);
        camera.aspect = (float)width / height;
        var previousTarget = RenderTexture.active;
        _environment.Begin(target);
        if (_preview && (!primary || !SplitScreenCameraContext.Enhanced || !_controller!.UsesFirstPersonRendering))
            _body.Begin(target, camera);
        if (!primary) { _tileVolume.Begin(camera, _tileCascades); SplitScreenCameraContext.TileCamera = camera; }
        SplitScreenForeignBillboards.Face(camera);
        // The remote lens belongs only to this submission. Keep it out of local mode capture, other views and
        // shutdown, even when they happen before the next pose update. Old peers have no lens to override.
        bool mirrorLens = primary && _mirroring && _mirrorFov > 0;
        float localFov = camera.fieldOfView;
        try
        {
            if (mirrorLens) camera.fieldOfView = _mirrorFov;
            camera.Render();
        }
        finally
        {
            if (mirrorLens) camera.fieldOfView = localFov;
            SplitScreenCameraContext.TileCamera = null;
            _tileVolume.End();
            LethalCompanyFirstPersonVisibility.Restore();
            _body.Restore();
            _environment.Restore();
            RenderTexture.active = previousTarget;
        }
        return view.Texture;
    }

    // The borrowed spectate camera hangs on the ship's game-over handle; it draws as the game poses it.
    public Texture? RenderGameView(SplitScreenKey key, int width, int height)
    {
        if (!_active || _preview) return null;
        SplitScreenCameraContext.Enhanced = false;
        LethalCompanySpectatorPresentation.Tick();
        var view = ViewFor(key, "Ship", width, height);
        var camera = _primaryCamera!;
        // The game's own takeoff shot: it puts its camera on the game-over handle once, as it switches; a view posed
        // through this camera just before (the crew's, as the ship left) must not leave it elsewhere.
        var round = StartOfRound.Instance;
        if (round.overrideSpectateCamera && camera.transform.parent == round.gameOverCameraHandle)
        { camera.transform.localPosition = Vector3.zero; camera.transform.localRotation = Quaternion.identity; }
        camera.enabled = false;
        camera.targetTexture = view.Texture;
        camera.rect = new Rect(0, 0, 1, 1);
        camera.aspect = (float)width / height;
        var previousTarget = RenderTexture.active;
        SplitScreenForeignBillboards.Face(camera);
        try { camera.Render(); }
        finally { RenderTexture.active = previousTarget; }
        return view.Texture;
    }

    private View ViewFor(SplitScreenKey key, string name, int width, int height)
    {
        if (!_views.TryGetValue(key, out var view)) { view = new View(); _views.Add(key, view); }
        if (view.Texture == null || view.Texture.width != width || view.Texture.height != height || view.TransitionPending)
        {
            if (view.Texture != null) _retired.Add(view.Texture);
            view.Texture = CreateTexture(width, height, name);
            view.TransitionPending = false;
        }
        return view;
    }

    public void InvalidateRooms() => SplitScreenInteriorVisibility.InvalidateRooms();

    public Texture? GetTexture(SplitScreenKey key) => _views.TryGetValue(key, out var view) ? view.Texture : null;

    public void BeginViewTransition(SplitScreenKey key)
    { if (_views.TryGetValue(key, out var view)) view.TransitionPending = true; }

    public void ReleaseView(SplitScreenKey key)
    {
        _orbits.Remove(key);
        if (!_views.TryGetValue(key, out var view)) return;
        if (view.Camera != null) UnityEngine.Object.Destroy(view.Camera.gameObject);
        if (view.Texture != null) _retired.Add(view.Texture);
        _views.Remove(key);
    }

    public void CollectRetiredTextures(Predicate<Texture> inUse)
    {
        for (int i = _retired.Count - 1; i >= 0; i--)
        {
            var texture = _retired[i];
            if (inUse(texture)) continue;
            texture.Release(); UnityEngine.Object.Destroy(texture); _retired.RemoveAt(i);
        }
    }

    public void Restore(Predicate<Texture>? keep = null)
    {
        if (!_active) return;
        _controller!.Mirrored = _mirroring = false;
        _controller.EndPreview();
        _controller.SetSplitScreenContext(false, false);
        _controller.RestoreSplitScreenMode(_savedMode, _savedEnhanced);
        if (_primaryCamera != null)
        {
            _primaryCamera.targetTexture = _savedTexture;
            _primaryCamera.rect = _savedRect;
            _primaryCamera.aspect = _savedAspect;
            if (_preview) UnityEngine.Object.Destroy(_primaryCamera.gameObject);
            else _primaryCamera.enabled = _savedEnabled && StartOfRound.Instance?.activeCamera == _primaryCamera;
            if (!_preview && _primaryCamera.TryGetComponent<HDAdditionalCameraData>(out var data))
            {
                data.hasPersistentHistory = _savedPersistentHistory;
                for (int i = 0; i < TileDisabled.Length; i++)
                {
                    data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)TileDisabled[i]] = _savedOverride[i];
                    data.renderingPathCustomFrameSettings.SetEnabled(TileDisabled[i], _savedEnabledField[i]);
                }
                data.customRenderingSettings = _savedCustomSettings;
            }
        }
        foreach (var view in _views.Values)
        {
            if (view.Camera != null) UnityEngine.Object.Destroy(view.Camera.gameObject);
            if (view.Texture != null) _retired.Add(view.Texture);
        }
        _views.Clear(); _orbits.Clear(); CollectRetiredTextures(keep ?? (_ => false));
        _tileVolume.Dispose();
        SplitScreenInteriorVisibility.Clear();
        SplitScreenCameraContext.Clear();
        _environment.Restore();
        _body.Restore();
        _primaryCamera = null; _primaryTarget = null; _primary = null;
        _active = _preview = _focused = false;
    }

    public void Dispose() => Restore();

    private void FreezePrimaryTexture()
    {
        if (_primary.HasValue) BeginViewTransition(_primary.Value.Key);
    }

    // HDRP renders into its own buffers and only blits color here. A depth target would add its
    // full-screen "Copy Depth In Target Texture" pass on every render (HDRenderPipeline.SetFinalTarget).
    private static RenderTexture CreateTexture(int width, int height, string name)
    {
        var texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
        { name = "Enhanced Spectator " + name, hideFlags = HideFlags.HideAndDontSave, useMipMap = false, autoGenerateMips = false, antiAliasing = 1 };
        texture.Create();
        return texture;
    }

    private static void DisableTileEffects(HDAdditionalCameraData data)
    {
        data.customRenderingSettings = true;
        foreach (var field in TileDisabled)
        {
            data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
            data.renderingPathCustomFrameSettings.SetEnabled(field, false);
        }
    }

    private static void ConfigureTile(HDAdditionalCameraData data)
    {
        DisableTileEffects(data);
        ref var settings = ref data.renderingPathCustomFrameSettings;
        data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.LODBiasMode] = true;
        data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.LODBias] = true;
        settings.lodBiasMode = LODBiasMode.ScaleQualitySettings;
        settings.lodBias = 1;
    }

    private static Camera CreateCamera(Camera source, string name, out HDAdditionalCameraData data)
    {
        var root = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
        var camera = root.AddComponent<Camera>();
        camera.CopyFrom(source); camera.enabled = false;
        camera.targetTexture = null; camera.rect = new Rect(0, 0, 1, 1);
        data = root.AddComponent<HDAdditionalCameraData>();
        if (source.TryGetComponent<HDAdditionalCameraData>(out var original)) original.CopyTo(data);
        data.volumeAnchorOverride = camera.transform;
        data.exposureTarget = null;
        data.hasPersistentHistory = true;
        data.allowDynamicResolution = false;
        data.allowDeepLearningSuperSampling = false;
        data.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
        return camera;
    }

    public bool IsIndoors(SplitScreenParticipant participant) => Resolve(participant.Source)?.isInsideFactory == true;

    private static PlayerControllerB? Resolve(SplitScreenKey key)
    {
        var round = StartOfRound.Instance;
        if (round == null || round.allPlayerScripts == null || key.SlotId >= (ulong)round.allPlayerScripts.Length) return null;
        var target = round.allPlayerScripts[(int)key.SlotId];
        return target != null && target.actualClientId == key.ClientId && target.isPlayerControlled && !target.isPlayerDead
            && !target.disconnectedMidGame ? target : null;
    }

    // The enlarged view turns with the local spectate pivot (the player's mouse); tiled, the audio view orbits its
    // player like the other small views.
    private readonly ChaseOrbit _primaryOrbit = new();
    // Each view's small-view angle, kept while it is drawn by the main camera too.
    private readonly Dictionary<SplitScreenKey, ChaseOrbit> _orbits = new();
    private ChaseOrbit OrbitOf(SplitScreenKey key)
    {
        if (!_orbits.TryGetValue(key, out var orbit)) _orbits.Add(key, orbit = new ChaseOrbit());
        return orbit;
    }
    private Quaternion PrimaryOrbit() => _focused ? StartOfRound.Instance.localPlayerController.spectateCameraPivot.rotation : _primaryOrbit.Rotation(_primaryTarget!);

    // The local spectate pivot (the mouse's orbit of the large view) at an angle. The game turns it from its own pitch
    // (cameraUp) at the next mouse movement, so that is set alike.
    private void FacePivot(Quaternion rotation)
    {
        if (_preview) return;
        var local = StartOfRound.Instance.localPlayerController;
        local.cameraUp = ChaseOrbit.PitchOf(rotation);
        local.spectateCameraPivot.rotation = Quaternion.Euler(local.cameraUp, rotation.eulerAngles.y, 0);
    }

    /// <summary>
    /// A small view's own orbit, as vanilla spectating left alone: fixed in the world, turning neither with its player
    /// nor with the large view's camera. It carries the angle the view was last seen from (as the large view, or the
    /// heard one among the tiles); a view not seen yet starts behind its player and a little above.
    /// </summary>
    internal sealed class ChaseOrbit
    {
        internal const float Pitch = 15;
        private Quaternion _rotation;
        private bool _set;
        internal Quaternion? Current => _set ? _rotation : null;
        internal void Reset() => _set = false;
        /// <summary>The direction of a view seen through another camera: its yaw and pitch, at vanilla's pitch limits.</summary>
        internal void Set(Quaternion rotation) { _rotation = Quaternion.Euler(PitchOf(rotation), rotation.eulerAngles.y, 0); _set = true; }
        internal Quaternion Rotation(PlayerControllerB target)
        {
            if (!_set) { _rotation = Quaternion.Euler(Pitch, target.transform.eulerAngles.y, 0); _set = true; }
            return _rotation;
        }
        internal static float PitchOf(Quaternion rotation) => Mathf.Clamp(Mathf.DeltaAngle(0, rotation.eulerAngles.x), -80, 80);
    }

    // Confirmed V81 SetWhoToSpectate/RaycastSpectateCameraAroundPivot semantics, around the given orbit.
    private static void ApplyVanillaPose(Camera camera, PlayerControllerB target, Quaternion orbit, float distance = 1.3f)
    {
        var local = StartOfRound.Instance.localPlayerController;
        Vector3 center = target.lowerSpine.position + Vector3.up * .7f;
        var ray = new Ray(center, -(orbit * Vector3.forward));
        // Vanilla's 0.1 m ray extension and 0.25 m wall margin.
        distance = Physics.Raycast(ray, out var hit, distance + .1f, local.walkableSurfacesNoPlayersMask, QueryTriggerInteraction.Ignore)
            ? Mathf.Max(0, hit.distance - .25f) : distance;
        camera.transform.position = ray.GetPoint(distance);
        camera.transform.LookAt(center);
        camera.fieldOfView = 66;
    }
}
