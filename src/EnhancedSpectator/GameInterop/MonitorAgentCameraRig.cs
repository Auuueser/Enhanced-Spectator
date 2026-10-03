using System;
using EnhancedSpectator.CameraRuntime.Cinemachine;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>Private Cinemachine photography pipeline; never writes the game's camera.</summary>
internal sealed class MonitorAgentCameraRig : IDisposable
{
    private GameObject? _root;
    private CinemachineBrain? _brain;
    private CinemachineVirtualCamera? _camera;
    private Transform? _focus;
    private int _tick;
    private float _yawVelocity, _pitchVelocity;

    internal void Seed(Vector3 position, Quaternion rotation)
    {
        if (_root == null) Create();
        _camera!.transform.SetPositionAndRotation(position, rotation);
        _camera.ForceCameraPosition(position, rotation);
        _camera.PreviousStateIsValid = false;
        _yawVelocity = _pitchVelocity = 0;
    }

    internal Quaternion Evaluate(Vector3 position, Vector3 focus, Quaternion previous, float dt, bool cut = false)
    {
        if (_root == null) Seed(position, previous);
        _camera!.transform.SetPositionAndRotation(position, previous);
        _focus!.position = focus;
        if (cut)
        {
            _camera.ForceCameraPosition(position, previous);
            _camera.PreviousStateIsValid = false;
        }

        // This Core belongs to our private assembly/namespace, not other mods.
        // ManualUpdate is the supported 2.9 API and also advances its update clock.
        CinemachineCore.UniformDeltaTimeOverride = dt;
        CinemachineCore.FrameCountOverride = ++_tick;
        try { _brain!.ManualUpdate(); }
        finally
        {
            CinemachineCore.UniformDeltaTimeOverride = -1;
            CinemachineCore.FrameCountOverride = -1;
        }
        Quaternion aimed = _brain.CurrentCameraState.FinalOrientation;
        Vector3 euler = aimed.eulerAngles;
        float pitch = Mathf.Clamp(Mathf.DeltaAngle(0, euler.x), -75f, 75f);
        Quaternion level = Quaternion.Euler(pitch, euler.y, 0);
        if (cut)
        {
            _yawVelocity = _pitchVelocity = 0;
            return level;
        }
        // Pan and tilt separately so the horizon stays level. A critically
        // damped follow keeps angular velocity continuous: framing corrections
        // ease in and out instead of snapping at a fixed rate cap.
        Vector3 from = previous.eulerAngles;
        float fromPitch = Mathf.DeltaAngle(0, from.x);
        if (dt <= 0) return Quaternion.Euler(fromPitch, from.y, 0);
        float yaw = Mathf.SmoothDampAngle(from.y, euler.y, ref _yawVelocity, .14f, 300f, dt);
        float tilt = Mathf.SmoothDampAngle(fromPitch, pitch, ref _pitchVelocity, .2f, 180f, dt);
        return Quaternion.Euler(Mathf.Clamp(tilt, -75f, 75f), yaw, 0);
    }

    internal void Clear()
    {
        if (_camera != null) _camera.PreviousStateIsValid = false;
        _yawVelocity = _pitchVelocity = 0;
    }

    public void Dispose()
    {
        if (_root == null) return;
        // Unregister before deferred destruction so an old rig cannot win selection.
        _root.SetActive(false);
        if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
        else UnityEngine.Object.DestroyImmediate(_root);
        _root = null; _brain = null; _camera = null; _focus = null;
    }

    private void Create()
    {
        _root = new GameObject("Enhanced Spectator Camera Agent") { hideFlags = HideFlags.HideAndDontSave };
        _root.SetActive(false);
        var output = Child("Photography output");
        var outputCamera = output.AddComponent<Camera>();
        outputCamera.enabled = false;
        outputCamera.fieldOfView = 60f;
        outputCamera.aspect = 16f / 9f;
        _brain = output.AddComponent<CinemachineBrain>();
        _brain.m_UpdateMethod = CinemachineBrain.UpdateMethod.ManualUpdate;
        _brain.m_BlendUpdateMethod = CinemachineBrain.BrainUpdateMethod.LateUpdate;
        _brain.m_IgnoreTimeScale = true;
        _brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0);
        _focus = Child("Subject focus").transform;
        _camera = Child("Travelling photograph").AddComponent<CinemachineVirtualCamera>();
        _camera.LookAt = _focus;
        _camera.m_Lens = LensSettings.FromCamera(outputCamera);
        // No Body component: the navigation motor already owns position and damping.
        var composer = _camera.AddCinemachineComponent<CinemachineComposer>();
        // Temporal smoothing happens once, in the damped pan/tilt above.
        composer.m_HorizontalDamping = 0;
        composer.m_VerticalDamping = 0;
        composer.m_DeadZoneWidth = .035f;
        composer.m_DeadZoneHeight = .075f;
        composer.m_SoftZoneWidth = .75f;
        composer.m_SoftZoneHeight = .75f;
        composer.m_LookaheadTime = 0;
        composer.m_LookaheadIgnoreY = true;
        _root.SetActive(true);
    }

    private GameObject Child(string name)
    {
        var child = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
        child.transform.SetParent(_root!.transform, false);
        return child;
    }
}
