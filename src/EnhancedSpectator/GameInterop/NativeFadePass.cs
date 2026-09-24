using System;
using System.Collections.Generic;
using System.IO;
using EnhancedSpectator.Logging;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.RendererUtils;

namespace EnhancedSpectator.GameInterop;

/// <summary>Draws original surfaces into a private target before fading the complete model.</summary>
internal sealed class NativeFadePass : CustomPass
{
    internal const int IsolationLayer = 31;
    internal const string ImplementationId = "whole-model-r6-range-region-20260924";
    internal static readonly string[] ForwardPassNames = { "Forward", "ForwardOnly", "SRPDefaultUnlit" };
    private static readonly ShaderTagId[] ForwardTags = { new ShaderTagId("Forward"), new ShaderTagId("ForwardOnly"), new ShaderTagId("SRPDefaultUnlit") };
    internal static readonly string[] DepthPassNames = { "DepthOnly", "DepthForwardOnly" };
    private static readonly ShaderTagId[] DepthTags = { new ShaderTagId("DepthOnly"), new ShaderTagId("DepthForwardOnly") };
    private static readonly List<LethalCompanyNativeFade> Owners = new List<LethalCompanyNativeFade>();
    internal static readonly NativeFadeSlots Slots = new NativeFadeSlots();
    private static NativeFadePass? _instance;
    private static GameObject? _host;
    private static CustomPassVolume? _volume;
    private static bool _failed;
    private static readonly NativeFadeResourceLease ResourceLease = new NativeFadeResourceLease();
    private static Config.SpectatorCameraConfig? _config;
    private static readonly NativeFadeCameraGate CameraGate = new NativeFadeCameraGate();
    private static readonly HashSet<int> ReportedBorrowedCameras = new HashSet<int>();
    internal static float FadeRadius => _config?.FadeRadius.Value ?? 2.5f;
    internal static void Configure(Config.SpectatorCameraConfig config) => _config = config;
    internal static void PrepareCamera(HDCamera camera, ref FrameSettings settings)
    {
        bool requested = !_failed && Owners.Count > 0 && _config != null
            && LethalCompanyFearViewCamera.IsActiveView(camera.camera)
            && LethalCompanyFearViewCamera.ShouldFade(_config);
        bool original = settings.IsEnabled(FrameSettingsField.CustomPass);
        bool enabled = CameraGate.Prepare(Time.frameCount, camera.camera.GetInstanceID(), requested, original);
        if (requested && _volume != null) _volume.targetCamera = camera.camera;
        if (enabled == original) return;
        settings.SetEnabled(FrameSettingsField.CustomPass, enabled);
        if (ReportedBorrowedCameras.Add(camera.camera.GetInstanceID()))
            ModLog.Info($"NativeFade camera compatibility: camera={camera.camera.name}; custom passes disabled by camera settings; enabling only the owned fade pass in the working frame. Saved quality settings and other disabled passes remain unchanged.");
    }
    internal static bool SuppressOtherPass(CustomPass pass, HDCamera camera) =>
        CameraGate.SuppressOtherPass(Time.frameCount, camera.camera.GetInstanceID(), pass == _instance);
    private static int _leaseFrame = -1;
    private readonly List<LethalCompanyNativeFade> _jobs = new List<LethalCompanyNativeFade>(24);
    private Material? _composite;
    private AssetBundle? _bundle;
    private RenderTexture? _model;
    private Camera? _readyCamera;
    private int _readyFrame = -100;
    private int _width, _height;
    private float _lastDiagnostic;
    private float _nextBufferDiagnostic;
    private bool _bufferDiagnosticPending;
    private string? _lastFailure;
    private readonly HashSet<string> _submittedModels = new HashSet<string>();
    private (int camera, RenderTextureDescriptor color, RenderTextureDescriptor depth, int width, int height, Vector4 colorScale, Vector4 depthScale)? _descriptor;
    private static readonly int ModelColor = Shader.PropertyToID("_ESFadeModelColor"), FadeOpacity = Shader.PropertyToID("_ESFadeOpacity");

    internal static bool Register(LethalCompanyNativeFade owner)
    {
        if (_failed) return false;
        if (_instance == null)
        {
            try
            {
                var pass = new NativeFadePass { name = "Enhanced Spectator whole-model fade" };
                _instance = pass;
                pass.LoadShader();
                _host = new GameObject("EnhancedSpectator.NativeFade") { hideFlags = HideFlags.HideAndDontSave };
                UnityEngine.Object.DontDestroyOnLoad(_host);
                _volume = _host.AddComponent<CustomPassVolume>();
                _volume.isGlobal = true;
                _volume.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;
                _volume.customPasses.Add(pass);
                RenderPipelineManager.endFrameRendering += EndFrame;
                ModLog.Info($"NativeFade resources loaded: build={ImplementationId}; awaiting camera buffers.");
            }
            catch (Exception ex)
            {
                _instance?.Release(); _instance = null;
                if (_host != null) UnityEngine.Object.Destroy(_host);
                _host = null; _volume = null; _failed = true;
                ModLog.Warning("Native fade preparation failed; originals preserved: " + ex.Message);
                return false;
            }
        }
        ResourceLease.Used(); Owners.Add(owner); return true;
    }
    internal static void Unregister(LethalCompanyNativeFade owner)
    {
        Owners.Remove(owner);
        if (Owners.Count == 0) ResourceLease.Idle(Time.unscaledTime);
    }
    private static void EndFrame(ScriptableRenderContext context, Camera[] cameras)
    {
        if (ResourceLease.Expired(Time.unscaledTime, Owners.Count)) Shutdown();
    }
    internal static void Shutdown()
    {
        RenderPipelineManager.endFrameRendering -= EndFrame;
        _instance?.Release(); _instance = null;
        if (_host != null) UnityEngine.Object.Destroy(_host);
        _host = null; _volume = null;
        CameraGate.Clear(); ReportedBorrowedCameras.Clear();
    }
    internal static void ExpireRequests()
    {
        if (_leaseFrame == Time.frameCount) return;
        _leaseFrame = Time.frameCount;
        foreach (var owner in Owners) if (owner.Frame != Time.frameCount) owner.ClearRequest();
    }
    internal static bool CanTakeCamera(Camera? camera) => CameraBlockReason(camera) == null;
    internal static string? CameraBlockReason(Camera? camera)
    {
        if (_failed) return "backend-failed";
        if (camera == null) return "no-active-camera";
        if (_instance == null || _volume == null || !_volume.isActiveAndEnabled || !_instance.enabled) return "volume-not-ready";
        if (camera.stereoEnabled) return "stereo-not-supported";
        if ((camera.cullingMask & (1 << IsolationLayer)) != 0) return "isolation-layer-in-camera-mask";
        if (!HDCamera.GetOrCreate(camera).frameSettings.IsEnabled(FrameSettingsField.CustomPass)) return "camera-custom-pass-disabled";
        if (_instance._readyCamera != camera || Time.frameCount - _instance._readyFrame > 1) return "waiting-for-camera-buffers";
        return null;
    }
    private void LoadShader()
    {
        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11)
            throw new NotSupportedException("Whole-model fade currently supports Direct3D11.");
        using var stream = typeof(NativeFadePass).Assembly.GetManifestResourceStream("EnhancedSpectator.Resources.nativefade.bundle")
            ?? throw new FileNotFoundException("Embedded nativefade.bundle missing.");
        using var data = new MemoryStream(); stream.CopyTo(data);
        _bundle = AssetBundle.LoadFromMemory(data.ToArray());
        if (_bundle == null) throw new InvalidOperationException("Native fade shader bundle could not be loaded.");
        var shader = _bundle.LoadAsset<Shader>("Assets/NativeFadeComposite.shader");
        if (shader == null || !shader.isSupported) throw new InvalidOperationException("Native fade composite shader unsupported.");
        _composite = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
    }
    protected override bool executeInSceneView => false;
    protected override void AggregateCullingParameters(ref ScriptableCullingParameters parameters, HDCamera camera)
    {
        if (LethalCompanyFearViewCamera.IsActiveView(camera.camera)) parameters.cullingMask |= 1u << IsolationLayer;
    }
    protected override void Execute(CustomPassContext ctx)
    {
        if (!LethalCompanyFearViewCamera.IsActiveView(ctx.hdCamera.camera) || _failed) return;
        _jobs.Clear();
        foreach (var owner in Owners)
            if (owner.Tag != 0 && owner.Camera == ctx.hdCamera.camera && owner.Frame == Time.frameCount) _jobs.Add(owner);
        try
        {
            var color = ctx.cameraColorBuffer.rt; var depth = ctx.cameraDepthBuffer.rt;
            DescribeBuffers(ctx, color, depth);
            string? problem = NativeFadeBuffers.Validate(color, depth, ctx.hdCamera.actualWidth, ctx.hdCamera.actualHeight);
            if (problem != null) { Unavailable(ctx, problem); return; }
            if (ctx.hdCamera.camera.stereoEnabled || (ctx.hdCamera.camera.cullingMask & (1 << IsolationLayer)) != 0)
            { Unavailable(ctx, "Camera includes isolation layer 31, or stereo is active."); return; }
            bool allocated = EnsureBuffer(color!.width, color.height);
            bool variantChanged = NativeFadeBuffers.Bind(ctx.cmd, _composite!, color, depth!, ctx.cameraColorBuffer.nameID, ctx.cameraDepthBuffer.nameID);
            var viewport = new Rect(0, 0, ctx.hdCamera.actualWidth, ctx.hdCamera.actualHeight);
            if (allocated || variantChanged || _readyCamera != ctx.hdCamera.camera)
            {
                // Prepare the selected copy/composite variants before accepting models.
                // This is camera preparation, independent of crossing the fade boundary.
                CopyScene(ctx, viewport);
                Composite(ctx, viewport, 0f);
            }
            if (_readyCamera != ctx.hdCamera.camera || _lastFailure != null)
                ModLog.Info($"NativeFade camera prepared: camera={ctx.hdCamera.camera.name}, build={ImplementationId}, viewport={ctx.hdCamera.actualWidth}x{ctx.hdCamera.actualHeight}. Waiting for model draw requests.");
            _lastFailure = null;
            _readyCamera = ctx.hdCamera.camera; _readyFrame = Time.frameCount;
            _jobs.Sort(FarToNear);
            foreach (var owner in _jobs)
            {
                ctx.cmd.BeginSample("ES whole-model fade");
                try
                {
                    // Fully invisible and offscreen models remain isolated but need no draw/copy.
                    if (owner.Opacity <= 0f) continue;
                    var region = owner.RenderRegion(ctx.hdCamera.camera, ctx.hdCamera.actualWidth, ctx.hdCamera.actualHeight);
                    if (region.width <= 0f || region.height <= 0f) continue;
                    owner.ReapplyForDraw();
                    CopyScene(ctx, viewport, region);
                    DrawNative(ctx, owner.Tag, true);
                    DrawNative(ctx, owner.Tag, false);
                    Composite(ctx, viewport, owner.Opacity, region);
                    if (_submittedModels.Add(owner.Key))
                        ModLog.Info($"NativeFade draw submitted: model={owner.Key}, camera={owner.Camera?.name}, alpha={owner.Opacity:F5}, tag={owner.Tag:X8}, build={ImplementationId}. Submission is not visual acceptance.");
                }
                finally { ctx.cmd.DisableScissorRect(); owner.Restore(); ctx.cmd.EndSample("ES whole-model fade"); }
            }
            if (ModLog.IsDebugEnabled && Time.unscaledTime - _lastDiagnostic > 5f)
            {
                _lastDiagnostic = Time.unscaledTime;
                foreach (var owner in _jobs)
                    ModLog.Debug($"NativeFade draw: model={owner.Key}, camera={owner.Camera?.name}, alpha={owner.Opacity:F5}, tag={owner.Tag:X8}, pass=original-forward, size={_width}x{_height}");
            }
        }
        catch (Exception ex)
        {
            _failed = true;
            Unavailable(ctx, ex.Message);
        }
        finally
        {
            ctx.cmd.DisableScissorRect();
            foreach (var owner in _jobs) owner.Restore();
            CoreUtils.SetRenderTarget(ctx.cmd, ctx.cameraColorBuffer);
        }
    }
    private void CopyScene(CustomPassContext ctx, Rect viewport, Rect? region = null)
    {
        ctx.cmd.SetRenderTarget(_model!); ctx.cmd.SetViewport(viewport);
        if (region.HasValue) ctx.cmd.EnableScissorRect(region.Value);
        ctx.cmd.DrawProcedural(Matrix4x4.identity, _composite!, 0, MeshTopology.Triangles, 3);
    }
    private void Composite(CustomPassContext ctx, Rect viewport, float opacity, Rect? region = null)
    {
        ctx.cmd.SetGlobalTexture(ModelColor, _model!);
        ctx.cmd.SetGlobalFloat(FadeOpacity, opacity);
        // No depth attachment: resolved color and MS depth cannot be bound together.
        ctx.cmd.SetRenderTarget(ctx.cameraColorBuffer.nameID, 0, CubemapFace.Unknown, 0);
        ctx.cmd.SetViewport(viewport);
        if (region.HasValue) ctx.cmd.EnableScissorRect(region.Value);
        ctx.cmd.DrawProcedural(Matrix4x4.identity, _composite!, 1, MeshTopology.Triangles, 3);
    }
    private void DescribeBuffers(CustomPassContext ctx, RenderTexture? color, RenderTexture? depth)
    {
        var signature = (ctx.hdCamera.camera.GetInstanceID(), color == null ? default : color.descriptor,
            depth == null ? default : depth.descriptor, ctx.hdCamera.actualWidth, ctx.hdCamera.actualHeight,
            ctx.cameraColorBuffer.rtHandleProperties.rtHandleScale, ctx.cameraDepthBuffer.rtHandleProperties.rtHandleScale);
        if (!_descriptor.HasValue || !_descriptor.Value.Equals(signature)) _bufferDiagnosticPending = true;
        _descriptor = signature;
        if (!_bufferDiagnosticPending || Time.unscaledTime < _nextBufferDiagnostic) return;
        _nextBufferDiagnostic = Time.unscaledTime + 5f; _bufferDiagnosticPending = false;
        ModLog.Info($"NativeFade buffers: camera={ctx.hdCamera.camera.name}, injection=BeforePostProcess, viewport={ctx.hdCamera.actualWidth}x{ctx.hdCamera.actualHeight}, stereo={ctx.hdCamera.camera.stereoEnabled}, hardwareScale={ScalableBufferManager.widthScaleFactor:F3}/{ScalableBufferManager.heightScaleFactor:F3}; color=[{Describe(color)}], scale={signature.Item6}; depth=[{Describe(depth)}], scale={signature.Item7}.");
    }
    private static string Describe(RenderTexture? texture) => texture == null ? "null RenderTexture" :
        $"{texture.name}, created={texture.IsCreated()}, dimension={texture.dimension}, allocation={texture.width}x{texture.height}, slices={texture.volumeDepth}, samples={texture.antiAliasing}, bindMS={texture.bindTextureMS}, format={texture.graphicsFormat}, depth={texture.descriptor.depthStencilFormat}, dynamicScale={texture.useDynamicScale}";
    private void Unavailable(CustomPassContext ctx, string reason)
    {
        ctx.cmd.DisableScissorRect();
        _readyCamera = null; _readyFrame = -100;
        if (_lastFailure != reason) { _lastFailure = reason; ModLog.Warning("Native fade unavailable; preserving native surfaces: " + reason); }
        // Matching attachments are necessary for depth-tested fallback draws.
        // If descriptors are incompatible, release ownership for the next frame.
        var color = ctx.cameraColorBuffer.rt; var depth = ctx.cameraDepthBuffer.rt;
        if (color == null || depth == null || color.antiAliasing != depth.antiAliasing || color.dimension != depth.dimension) return;
        CoreUtils.SetRenderTarget(ctx.cmd, ctx.cameraColorBuffer, ctx.cameraDepthBuffer);
        foreach (var owner in _jobs)
        {
            try { owner.ReapplyForDraw(); DrawNative(ctx, owner.Tag, true); DrawNative(ctx, owner.Tag, false); }
            finally { owner.Restore(); }
        }
    }
    private static int FarToNear(LethalCompanyNativeFade left, LethalCompanyNativeFade right) => right.SortDistance.CompareTo(left.SortDistance);
    private static void DrawNative(CustomPassContext ctx, uint tag, bool opaque)
    {
        if (opaque)
        {
            // Native opaque Forward can bypass alpha clipping and requires Equal
            // against its own clipped depth prepass. LessEqual alone exposes the
            // cutout card (clock hands, apparatus label, Maneater appendages).
            var depth = new RendererListDesc(DepthTags, ctx.cullingResults, ctx.hdCamera.camera)
            {
                layerMask = 1 << IsolationLayer, renderingLayerMask = tag,
                renderQueueRange = RenderQueueRange.opaque, sortingCriteria = SortingCriteria.CommonOpaque,
                stateBlock = new RenderStateBlock(RenderStateMask.Depth | RenderStateMask.Blend)
                {
                    depthState = new DepthState(true, CompareFunction.LessEqual),
                    blendState = new BlendState { blendState0 = new RenderTargetBlendState((ColorWriteMask)0) }
                }
            };
            var depthList = ctx.renderContext.CreateRendererList(depth);
            CoreUtils.DrawRendererList(ctx.renderContext, ctx.cmd, depthList);
        }
        var desc = new RendererListDesc(ForwardTags, ctx.cullingResults, ctx.hdCamera.camera)
        {
            layerMask = 1 << IsolationLayer,
            renderingLayerMask = tag,
            renderQueueRange = opaque ? RenderQueueRange.opaque : RenderQueueRange.transparent,
            sortingCriteria = opaque ? SortingCriteria.CommonOpaque : SortingCriteria.CommonTransparent,
            rendererConfiguration = HDUtils.GetRendererConfiguration(ctx.hdCamera.frameSettings.IsEnabled(FrameSettingsField.ProbeVolume), ctx.hdCamera.frameSettings.IsEnabled(FrameSettingsField.Shadowmask)),
            // Preserve each source pass's original Equal/LEqual and write policy.
            stateBlock = new RenderStateBlock(RenderStateMask.Nothing)
        };
        // Same light-list selection as installed HDRP's RenderForwardRendererList.
        bool tiled = opaque && ctx.hdCamera.frameSettings.IsEnabled(FrameSettingsField.FPTLForForwardOpaque);
        CoreUtils.SetKeyword(ctx.cmd, "USE_FPTL_LIGHTLIST", tiled);
        CoreUtils.SetKeyword(ctx.cmd, "USE_CLUSTERED_LIGHTLIST", !tiled);
        var list = ctx.renderContext.CreateRendererList(desc);
        CoreUtils.DrawRendererList(ctx.renderContext, ctx.cmd, list);
    }
    private bool EnsureBuffer(int width, int height)
    {
        if (_model != null && _model.IsCreated() && _width == width && _height == height) return false;
        if (_model != null) { _model.Release(); UnityEngine.Object.Destroy(_model); }
        _model = NativeFadeBuffers.CreateModelBuffer(width, height);
        _width = width; _height = height;
        return true;
    }
    protected override void Cleanup() => Release();
    private void Release()
    {
        _readyCamera = null;
        if (_model != null) { _model.Release(); UnityEngine.Object.Destroy(_model); _model = null; }
        if (_composite != null) { UnityEngine.Object.Destroy(_composite); _composite = null; }
        if (_bundle != null) { _bundle.Unload(true); _bundle = null; }
    }
}
