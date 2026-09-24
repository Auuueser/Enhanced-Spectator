using System;
using EnhancedSpectator.Features;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

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
        LethalCompanySpectatorUiVisibility.RestoreRenderers();
        LethalCompanyFirstPersonVisibility.Restore();
        _featureBootstrapper?.Tick();
    }

    private void LateUpdate()
    {
        _featureBootstrapper?.LateTick();
    }

    private void OnGUI()
    {
        _featureBootstrapper?.GuiTick();
    }

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
        LethalCompanySpectatorUiVisibility.Clear();
        _ = scene;
        _ = mode;
        LethalCompanyFirstPersonVisibility.Clear();
        RuntimeConnectionState.MarkSceneTransition();
        EnsureInstalled();
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        LethalCompanySpectatorUiVisibility.Clear();
        _ = scene;
        LethalCompanyFirstPersonVisibility.Clear();
        RuntimeConnectionState.MarkSceneTransition();
    }

    private static void OnCameraPostRender(Camera camera) => LethalCompanyFirstPersonVisibility.Restore();

    private static void OnEndCameraRendering(ScriptableRenderContext context, Camera camera) => LethalCompanyFirstPersonVisibility.Restore();

    private static void OnCameraPreCull(Camera camera)
    {
        TryCameraPreCullTick(camera);
    }

    private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
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
