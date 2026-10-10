using System;
using System.Collections.Generic;
using System.IO;
using EnhancedSpectator.Logging;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace EnhancedSpectator.GameInterop;

// Owns only private targets/materials. Original alpha-tested depth passes preserve native silhouettes.
internal sealed class SpectatorThermalPass : CustomPass
{
    private static SpectatorThermalPass? _instance;
    private static GameObject? _host;
    private static bool _failed;
    private static float _unwantedSince=-1;
    private AssetBundle? _bundle;
    private Material? _composite;
    private Shader? _surfaceShader;
    private RenderTexture? _scene, _heat, _cover, _blur, _glow;
    private string? _bufferProblem;
    private readonly SpectatorThermalSources _sources=new SpectatorThermalSources();
    private readonly List<Material> _materials=new List<Material>();
    private readonly Dictionary<Material,Material> _fallbacks=new Dictionary<Material,Material>();
    private readonly Dictionary<Material,int> _depthPasses=new Dictionary<Material,int>();
    private readonly Plane[] _planes=new Plane[6];
    internal static bool Owns(CustomPass pass) => pass==_instance;
    internal static bool Requested(Camera camera) => !_failed && _instance!=null && LethalCompanySpectatorPresentation.ThermalRequested(camera);
    internal static void Tick()
    {
        // Only switching thermal off tears the pass down. A moment without an eligible view (a target change,
        // a mode or room switch) keeps shaders and the body registry, so bodies never vanish while it refills.
        if(!LethalCompanySpectatorPresentation.ThermalEnabled)
        {
            if(_instance!=null) { _instance.Release(); _instance=null; if(_host!=null) UnityEngine.Object.Destroy(_host); _host=null; }
            return;
        }
        var camera=SplitScreenCameraContext.PreviewView ?? StartOfRound.Instance?.activeCamera;
        bool wanted=camera!=null && LethalCompanySpectatorPresentation.ThermalRequested(camera) || LethalCompanySpectatorPresentation.ThermalAllViews;
        if(!wanted)
        {
            // Large targets are only returned after a sustained absence, never for a one-frame gap.
            if(_unwantedSince<0) _unwantedSince=Time.unscaledTime;
            else if(_instance?._scene!=null && Time.unscaledTime-_unwantedSince>2) { _instance.ReleaseTargets(); _instance._sources.Clear(); }
            return;
        }
        _unwantedSince=-1;
        if(_instance==null && !_failed)
        {
            try
            {
                var pass=new SpectatorThermalPass { name="Enhanced Spectator thermal imaging" }; _instance=pass;
                using var stream=typeof(SpectatorThermalPass).Assembly.GetManifestResourceStream("EnhancedSpectator.Resources.thermal.bundle")
                    ?? throw new FileNotFoundException("thermal.bundle");
                using var data=new MemoryStream(); stream.CopyTo(data);
                pass._bundle=AssetBundle.LoadFromMemory(data.ToArray());
                var shader=pass._bundle?.LoadAsset<Shader>("Assets/ThermalComposite.shader");
                pass._surfaceShader=pass._bundle?.LoadAsset<Shader>("Assets/ThermalSurface.shader");
                if(shader==null || !shader.isSupported || pass._surfaceShader==null || !pass._surfaceShader.isSupported)
                    throw new InvalidOperationException("Thermal shaders unavailable");
                pass._composite=new Material(shader) { hideFlags=HideFlags.HideAndDontSave };
                _host=new GameObject("EnhancedSpectator.Thermal") { hideFlags=HideFlags.HideAndDontSave };
                UnityEngine.Object.DontDestroyOnLoad(_host);
                var volume=_host.AddComponent<CustomPassVolume>(); volume.isGlobal=true;
                // After scene post-processing and fade, before the overlay HUD. Fixed palette is not regraded.
                volume.injectionPoint=CustomPassInjectionPoint.AfterPostProcess; volume.customPasses.Add(pass);
            }
            catch(Exception ex)
            { _failed=true; _instance?.Release(); _instance=null; if(_host!=null) UnityEngine.Object.Destroy(_host); _host=null; ModLog.Warning("Thermal imaging unavailable; normal image retained: "+ex.Message); }
        }
        _instance?._sources.Refresh();
    }
    public override bool executeInSceneView => false;
    public override void Execute(CustomPassContext ctx)
    {
        if(!Requested(ctx.hdCamera.camera) || _composite==null) return;
        var color=ctx.cameraColorBuffer.rt; var depth=ctx.cameraDepthBuffer.rt;
        int width=ctx.hdCamera.actualWidth,height=ctx.hdCamera.actualHeight;
        string? problem=ctx.hdCamera.camera.stereoEnabled ? "stereo-not-supported" : NativeFadeBuffers.Validate(color,depth,width,height);
        if(problem!=null)
        {
            if(_bufferProblem!=problem) { _bufferProblem=problem; ModLog.Debug("Thermal buffers unavailable; normal image retained: "+problem); }
            return;
        }
        _bufferProblem=null;
        try
        {
            if(_scene==null || _scene.width!=color!.width || _scene.height!=color.height)
            {
                ReleaseTargets(); _scene=NativeFadeBuffers.CreateModelBuffer(color!.width,color.height); _heat=NativeFadeBuffers.CreateModelBuffer(color.width,color.height);
                int halfWidth=SpectatorThermalGlow.Half(color.width),halfHeight=SpectatorThermalGlow.Half(color.height);
                _cover=SpectatorThermalGlow.Create(halfWidth,halfHeight); _blur=SpectatorThermalGlow.Create(halfWidth,halfHeight); _glow=SpectatorThermalGlow.Create(halfWidth,halfHeight);
            }
            var cmd=ctx.cmd; var camera=ctx.hdCamera.camera;
            NativeFadeBuffers.Bind(cmd,_composite,color!,depth!,ctx.cameraColorBuffer.nameID,ctx.cameraDepthBuffer.nameID);
            var viewport=new Rect(0,0,width,height);
            cmd.SetRenderTarget(_scene!); cmd.SetViewport(viewport);
            cmd.DrawProcedural(Matrix4x4.identity,_composite,0,MeshTopology.Triangles,3);
            cmd.SetRenderTarget(_heat!); cmd.SetViewport(viewport);
            cmd.ClearRenderTarget(true,true,Color.clear,1);
            GeometryUtility.CalculateFrustumPlanes(camera,_planes);
            cmd.SetGlobalMatrix("_ESThermalVP",GL.GetGPUProjectionMatrix(camera.projectionMatrix,true)*camera.worldToCameraMatrix);
            foreach(var actor in _sources.Actors)
            {
                if(!actor.Alive) continue;
                foreach(var renderer in actor.Renderers) DrawBody(cmd,camera,renderer);
                foreach(var group in actor.Lods)
                    foreach(var level in group)
                        if(SpectatorThermalSources.AnyVisible(level,camera)) { foreach(var renderer in level) DrawBody(cmd,camera,renderer); break; }
            }
            float near=camera.nearClipPlane,far=camera.farClipPlane;
            float x=SystemInfo.usesReversedZBuffer ? far/near-1 : 1-far/near;
            float y=SystemInfo.usesReversedZBuffer ? 1 : far/near;
            cmd.SetGlobalVector("_ESThermalZ",new Vector4(x,y,x/far,y/far));
            cmd.SetGlobalVector("_ESThermalViewport",new Vector4(width,height,0,0));
            cmd.SetGlobalInt("_ESThermalPalette",LethalCompanySpectatorPresentation.ThermalPalette);
            cmd.SetGlobalFloat("_ESThermalStrength",LethalCompanySpectatorPresentation.ThermalStrength);
            cmd.SetGlobalTexture("_ESThermalScene",_scene!);
            cmd.SetGlobalTexture("_ESThermalDepth",_heat!,RenderTextureSubElement.Depth);
            cmd.SetGlobalTexture("_ESThermalSceneDepth",_scene!,RenderTextureSubElement.Depth);
            SpectatorThermalGlow.Record(cmd,_composite,_cover!,_blur!,_glow!,width,height);
            cmd.SetRenderTarget(ctx.cameraColorBuffer.nameID,0,CubemapFace.Unknown,0); cmd.SetViewport(viewport);
            cmd.DrawProcedural(Matrix4x4.identity,_composite,1,MeshTopology.Triangles,3);
        }
        catch(Exception ex) { _failed=true; ModLog.Warning("Thermal rendering stopped; normal image retained: "+ex.Message); }
        finally { CoreUtils.SetRenderTarget(ctx.cmd,ctx.cameraColorBuffer); }
    }
    private void DrawBody(CommandBuffer cmd,Camera camera,Renderer renderer)
    {
        if(!SpectatorThermalSources.Visible(renderer,camera) || !GeometryUtility.TestPlanesAABB(_planes,renderer.bounds)) return;
        renderer.GetSharedMaterials(_materials);
        for(int i=0;i<_materials.Count;i++)
        {
            var material=_materials[i]; if(material==null) continue;
            if(!_depthPasses.TryGetValue(material,out int pass))
            {
                pass=material.FindPass("DepthOnly"); if(pass<0) pass=material.FindPass("DepthForwardOnly");
                if(pass>=0 && !material.GetShaderPassEnabled(material.GetPassName(pass))) pass=-1;
                _depthPasses.Add(material,pass);
            }
            if(pass>=0) cmd.DrawRenderer(renderer,material,i,pass);
            else cmd.DrawRenderer(renderer,Fallback(material),i,0);
        }
    }
    private Material Fallback(Material source)
    {
        if(_fallbacks.TryGetValue(source,out var material)) return material;
        material=new Material(_surfaceShader!) { hideFlags=HideFlags.HideAndDontSave };
        foreach(string property in new[]{"_BaseColorMap","_BaseMap","_MainTex"})
            if(source.HasProperty(property) && source.GetTexture(property)!=null)
            { material.SetTexture("_MainTex",source.GetTexture(property)); material.SetTextureScale("_MainTex",source.GetTextureScale(property)); material.SetTextureOffset("_MainTex",source.GetTextureOffset(property)); break; }
        float cutoff=source.HasProperty("_AlphaCutoff")?source.GetFloat("_AlphaCutoff"):source.HasProperty("_Cutoff")?source.GetFloat("_Cutoff"):.05f;
        bool clip=source.IsKeywordEnabled("_ALPHATEST_ON") || source.renderQueue>=2450;
        material.SetFloat("_Cutoff",clip?Mathf.Max(.01f,cutoff):-1);
        material.SetFloat("_Opacity",source.HasProperty("_BaseColor")?source.GetColor("_BaseColor").a:source.HasProperty("_Color")?source.GetColor("_Color").a:1);
        _fallbacks.Add(source,material); return material;
    }
    private void ReleaseTargets()
    {
        foreach(var texture in new[]{_scene,_heat,_cover,_blur,_glow}) if(texture!=null) { texture.Release(); UnityEngine.Object.Destroy(texture); }
        _scene=_heat=_cover=_blur=_glow=null;
    }
    private void Release()
    { ReleaseTargets(); _sources.Clear(); foreach(var m in _fallbacks.Values) UnityEngine.Object.Destroy(m); _fallbacks.Clear(); _depthPasses.Clear(); if(_composite!=null) UnityEngine.Object.Destroy(_composite); if(_bundle!=null) _bundle.Unload(true); }
}
