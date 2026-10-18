using System;
using EnhancedSpectator.Features;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using LowLevelPlayerLoop = UnityEngine.LowLevel.PlayerLoop;

namespace EnhancedSpectator.Runtime;

/// <summary>
/// Owns Unity runtime ticks independently from the BepInEx plugin component lifetime.
/// </summary>
public sealed class EnhancedSpectatorRuntimeDriver : MonoBehaviour
{
    private static FeatureBootstrapper? _featureBootstrapper;
    private static Action? _shutdown;
    private static EnhancedSpectatorRuntimeDriver? _instance;
    private static bool _applicationQuitting;
    private static bool _sceneHookRegistered;
    private static int _lastCameraTickFrame = -1;
    private static int _lastCameraTickInstanceId = int.MinValue;

    private bool _intentionalDestroy;
    private EnhancedSpectatorGuiDriver? _gui;

    /// <summary>
    /// Installs or refreshes the runtime driver.
    /// </summary>
    public static void Install(FeatureBootstrapper featureBootstrapper, Action shutdown)
    {
        _featureBootstrapper = featureBootstrapper ?? throw new ArgumentNullException(nameof(featureBootstrapper));
        _shutdown = shutdown ?? throw new ArgumentNullException(nameof(shutdown));
        _applicationQuitting = false;

        if (!_sceneHookRegistered)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            Camera.onPreCull += OnCameraPreCull;
            Camera.onPostRender += OnCameraPostRender;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            PlaceAfterLateUpdate(true);
            _sceneHookRegistered = true;
        }

        EnsureInstalled();
    }

    /// <summary>
    /// Ensures a runtime driver exists after early plugin component destruction.
    /// </summary>
    public static void EnsureInstalled()
    {
        if (_applicationQuitting || _featureBootstrapper == null || _instance != null)
        {
            return;
        }

        GameObject driverObject = new GameObject($"{PluginMetadata.Name} Runtime");
        DontDestroyOnLoad(driverObject);
        _instance = driverObject.AddComponent<EnhancedSpectatorRuntimeDriver>();
    }

    /// <summary>
    /// Marks the driver as intentionally shutting down.
    /// </summary>
    public static void BeginShutdown()
    {
        LethalCompanySpectatorUiVisibility.Clear();
        LethalCompanySpectatorPresentation.Clear();
        _applicationQuitting = true;
        LethalCompanyFirstPersonVisibility.Clear();
        RuntimeConnectionState.MarkPluginShuttingDown();

        if (_sceneHookRegistered)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            Camera.onPreCull -= OnCameraPreCull;
            Camera.onPostRender -= OnCameraPostRender;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            PlaceAfterLateUpdate(false);
            _sceneHookRegistered = false;
        }

        if (_instance != null)
        {
            _instance._intentionalDestroy = true;
            Destroy(_instance.gameObject);
            _instance = null;
        }

        _featureBootstrapper = null;
        _shutdown = null;
        _lastCameraTickFrame = -1;
        _lastCameraTickInstanceId = int.MinValue;
    }

    private void Update()
    {
        bool wantsGui = _featureBootstrapper?.WantsGui == true;
        if (wantsGui && _gui == null) _gui = gameObject.AddComponent<EnhancedSpectatorGuiDriver>();
        else if (!wantsGui && _gui != null) { Destroy(_gui); _gui = null; }
        LethalCompanySpectatorUiVisibility.RestoreRenderers();
        LethalCompanyFirstPersonVisibility.Restore();
        _featureBootstrapper?.Tick();
        LethalCompanySpectatorPresentation.Tick();
    }

    private void LateUpdate()
    {
        _featureBootstrapper?.LateTick();
    }

    // A player-loop step right after every script's LateUpdate, before anything renders: the game moves things there
    // (a held item onto its holder's hand, the ship's furniture with the ship), and cameras drawn by hand must see
    // them where the game's own camera will. A LateUpdate of ours may run before theirs.
    private struct AfterLateUpdateStep { }

    private static void PlaceAfterLateUpdate(bool place)
    {
        var root = LowLevelPlayerLoop.GetCurrentPlayerLoop();
        var stages = root.subSystemList;
        for (int i = 0; i < stages.Length; i++)
        {
            if (stages[i].type != typeof(PreLateUpdate)) continue;
            var steps = new List<PlayerLoopSystem>(stages[i].subSystemList);
            steps.RemoveAll(step => step.type == typeof(AfterLateUpdateStep));
            if (place)
                steps.Insert(steps.FindIndex(step => step.type == typeof(PreLateUpdate.ScriptRunBehaviourLateUpdate)) + 1,
                    new PlayerLoopSystem { type = typeof(AfterLateUpdateStep), updateDelegate = AfterLateUpdate });
            stages[i].subSystemList = steps.ToArray();
        }
        LowLevelPlayerLoop.SetPlayerLoop(root);
    }

    private static void AfterLateUpdate()
    {
        if (_instance != null) _featureBootstrapper?.AfterLateTick();
    }

    internal static void GuiTick() => _featureBootstrapper?.GuiTick();

    private void OnApplicationQuit()
    {
        _applicationQuitting = true;
        RuntimeConnectionState.MarkApplicationQuitting();
        _shutdown?.Invoke();
        _shutdown = null;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }

        if (!_applicationQuitting && !_intentionalDestroy)
        {
            ModLog.Warning("Runtime driver destroyed before application quit; it will be recreated on the next plugin or scene lifecycle event.");
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if(MonitorDoorGeometry.OwnsScene(scene)) return;
        LethalCompanySpectatorUiVisibility.Clear();
        LethalCompanySpectatorPresentation.Clear();
        _ = scene;
        _ = mode;
        LethalCompanyFirstPersonVisibility.Clear();
        RuntimeConnectionState.MarkSceneTransition();
        EnsureInstalled();
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        if(MonitorDoorGeometry.OwnsScene(scene)) return;
        LethalCompanySpectatorUiVisibility.Clear();
        LethalCompanySpectatorPresentation.Clear();
        _ = scene;
        LethalCompanyFirstPersonVisibility.Clear();
        RuntimeConnectionState.MarkSceneTransition();
    }

    private static void OnCameraPostRender(Camera camera) => LethalCompanyFirstPersonVisibility.Restore();

    private static void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    { LethalCompanyFirstPersonVisibility.Restore(); LethalCompanySpectatorPresentation.EndCamera(camera); }

    private static void OnCameraPreCull(Camera camera)
    {
        NativeFadeDiagnostics.CameraEvent("builtin-pre-cull", camera);
        TryCameraPreCullTick(camera);
    }

    private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        NativeFadeDiagnostics.CameraEvent("SRP-begin", camera);
        _ = context;
        TryCameraPreCullTick(camera);
    }

    private static void TryCameraPreCullTick(Camera camera)
    {
        if (_featureBootstrapper == null || camera == null)
        {
            return;
        }

        int frame = Time.frameCount;
        int cameraId = camera.GetInstanceID();
        if (_lastCameraTickFrame == frame && _lastCameraTickInstanceId == cameraId)
        {
            return;
        }

        _lastCameraTickFrame = frame;
        _lastCameraTickInstanceId = cameraId;
        LethalCompanyFirstPersonVisibility.Restore();
        _featureBootstrapper.CameraPreCullTick(camera);
    }
}
