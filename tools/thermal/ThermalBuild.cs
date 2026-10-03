using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.Rendering;
using EnhancedSpectator.GameInterop;

public static class ThermalBuild
{
    [Serializable] private sealed class Result { public bool passed; public int checks; public string limitation; }
    private static int checks;
    public static void Run()
    {
        try
        {
            AssetDatabase.Refresh();
            foreach(string name in new[]{"ThermalComposite","ThermalSurface","ThermalArchitectureTest"})
            {
                var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/"+name+".shader");
                Check(shader!=null && shader.isSupported,"Shader supported: "+name);
                foreach(var message in ShaderUtil.GetShaderMessages(shader))
                    Check(message.severity!=ShaderCompilerMessageSeverity.Error,message.message);
            }
            Validate(AssetDatabase.LoadAssetAtPath<Shader>("Assets/ThermalComposite.shader"));
            Directory.CreateDirectory("Output");
            Check(BuildPipeline.BuildAssetBundles("Output",new[]{new AssetBundleBuild { assetBundleName="thermal.bundle",
                assetNames=new[]{"Assets/ThermalComposite.shader","Assets/ThermalSurface.shader"} }},
                BuildAssetBundleOptions.ForceRebuildAssetBundle|BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64)!=null,"bundle built");
            var bundle=AssetBundle.LoadFromFile("Output/thermal.bundle");
            Check(bundle!=null && bundle.GetAllAssetNames().Length==2,"authored shaders only");
            Validate(bundle.LoadAsset<Shader>("Assets/ThermalComposite.shader")); bundle.Unload(true);
            File.WriteAllText("gpu-results.json",JsonUtility.ToJson(new Result { passed=true,checks=checks,
                limitation="D3D11 shipping shader: gray architectural contrast (unlit/weak/bright walls, doorway, stair depth), stable background through actor entry, scene/actor occlusion and cutouts, 2D/array buffers. Synthetic GPU fixtures; live HDRP/native enemy material coverage and perceptual acceptance need gameplay validation." },true));
        }
        catch(Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }
    private static void Validate(Shader shader)
    {
        var composite=new Material(shader); var fixture=new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/NativeFadeGpuTest.shader"));
        foreach(bool array in new[]{false,true}) foreach(int size in new[]{32,64}) foreach(float light in new[]{0f,.3f,20f})
        foreach(bool behind in new[]{false,true}) foreach(bool cutout in new[]{false,true}) foreach(bool fallback in new[]{false,true})
        {
            var source=NativeFadeBuffers.CreateModelBuffer(size,size);
            if(array) { source.Release(); source.dimension=TextureDimension.Tex2DArray; source.volumeDepth=1; source.Create(); }
            var copied=NativeFadeBuffers.CreateModelBuffer(size,size); var heat=NativeFadeBuffers.CreateModelBuffer(size,size); var output=NativeFadeBuffers.CreateModelBuffer(size,size);
            bool reversed=SystemInfo.usesReversedZBuffer;
            float background=reversed?.4f:.6f,actor=behind?(reversed?.2f:.8f):(reversed?.6f:.4f);
            var cmd=new CommandBuffer();
            cmd.SetGlobalVector("_ESThermalViewport",new Vector4(size,size,0,0));
            cmd.SetRenderTarget(source,0,CubemapFace.Unknown,0); cmd.SetViewport(new Rect(0,0,size,size));
            cmd.SetGlobalColor("_Color",new Color(light,light,light,1)); cmd.SetGlobalFloat("_Depth",background);
            cmd.DrawProcedural(Matrix4x4.identity,fixture,0,MeshTopology.Triangles,3);
            NativeFadeBuffers.Bind(cmd,composite,source,source,source,source);
            cmd.SetRenderTarget(copied); cmd.DrawProcedural(Matrix4x4.identity,composite,0,MeshTopology.Triangles,3);
            cmd.SetRenderTarget(heat); cmd.ClearRenderTarget(true,true,Color.clear,1);
            var surface=new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/ThermalSurface.shader"));
            var alpha=new Texture2D(2,1,TextureFormat.RGBA32,false,true) { filterMode=FilterMode.Point };
            alpha.SetPixels(new[]{Color.white,cutout?Color.clear:Color.white}); alpha.Apply();
            surface.SetTexture("_MainTex",alpha); surface.SetFloat("_Cutoff",.5f);
            var mesh=new Mesh { vertices=new[]{new Vector3(-1,-1,actor),new Vector3(1,-1,actor),new Vector3(1,1,actor),new Vector3(-1,1,actor)},
                uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up},triangles=new[]{0,1,2,0,2,3} };
            cmd.SetGlobalMatrix("_ESThermalVP",Matrix4x4.identity);
            if(fallback) cmd.DrawMesh(mesh,Matrix4x4.identity,surface,0,0);
            else { cmd.SetGlobalFloat("_Depth",actor); cmd.DrawProcedural(Matrix4x4.identity,fixture,cutout?4:7,MeshTopology.Triangles,3); }
            cmd.SetGlobalTexture("_ESThermalScene",copied); cmd.SetGlobalTexture("_ESThermalDepth",heat,RenderTextureSubElement.Depth);
            cmd.SetGlobalTexture("_ESThermalSceneDepth",copied,RenderTextureSubElement.Depth);
            cmd.SetGlobalVector("_ESThermalZ",reversed?new Vector4(99,1,.99f,.01f):new Vector4(-99,100,-.99f,1));
            cmd.SetRenderTarget(output); cmd.DrawProcedural(Matrix4x4.identity,composite,1,MeshTopology.Triangles,3);
            Graphics.ExecuteCommandBuffer(cmd); cmd.Release();
            var old=RenderTexture.active; RenderTexture.active=output;
            var pixels=new Texture2D(size,size,TextureFormat.RGBAFloat,false,true); pixels.ReadPixels(new Rect(0,0,size,size),0,0); pixels.Apply(); RenderTexture.active=old;
            Color left=pixels.GetPixel(8,size/2),right=pixels.GetPixel(size-4,size/2);
            Check(!float.IsNaN(left.r),"finite palette");
            Check(behind?left.r<.6f:left.r>.9f && left.g>.5f,"depth occludes heat and visible actor is hot");
            if(cutout) Check(right.r<.6f,"cutout holes remain cool");
            else Check(Mathf.Abs(left.r-right.r)<.01f,"uniform temperature has no screen-side bias");
            UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(surface); UnityEngine.Object.DestroyImmediate(alpha); UnityEngine.Object.DestroyImmediate(mesh);
            foreach(var rt in new[]{source,copied,heat,output}) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
        }
        Architecture(composite,fixture);
        UnityEngine.Object.DestroyImmediate(composite); UnityEngine.Object.DestroyImmediate(fixture);
    }
    private static void Architecture(Material composite,Material actor)
    {
        var sceneMaterial=new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/ThermalArchitectureTest.shader"));
        foreach(int size in new[]{64,128}) foreach(float light in new[]{0f,.15f,20f})
        {
            var scene=NativeFadeBuffers.CreateModelBuffer(size,size);
            var heat=NativeFadeBuffers.CreateModelBuffer(size,size);
            var output=NativeFadeBuffers.CreateModelBuffer(size,size);
            var samples=new Color[2][];
            for(int present=0;present<2;present++)
            {
                bool reversed=SystemInfo.usesReversedZBuffer;
                var z=reversed?new Vector4(99,1,.99f,.01f):new Vector4(-99,100,-.99f,1);
                var cmd=new CommandBuffer();
                cmd.SetGlobalVector("_ESThermalViewport",new Vector4(size,size,0,0));
                cmd.SetGlobalVector("_ESThermalZ",z); cmd.SetGlobalFloat("_FixtureLight",light);
                cmd.SetRenderTarget(scene); cmd.SetViewport(new Rect(0,0,size,size));
                cmd.DrawProcedural(Matrix4x4.identity,sceneMaterial,0,MeshTopology.Triangles,3);
                cmd.SetRenderTarget(heat); cmd.ClearRenderTarget(true,true,Color.clear,1);
                if(present!=0)
                {
                    cmd.SetViewport(new Rect(size*.7f,size*.6f,size*.1f,size*.25f));
                    cmd.SetGlobalFloat("_Depth",(.5f-z.w)/z.z);
                    cmd.DrawProcedural(Matrix4x4.identity,actor,7,MeshTopology.Triangles,3);
                }
                cmd.SetGlobalInt("_ESFadeReversedZ",reversed?1:0);
                cmd.SetGlobalTexture("_ESThermalScene",scene);
                cmd.SetGlobalTexture("_ESThermalSceneDepth",scene,RenderTextureSubElement.Depth);
                cmd.SetGlobalTexture("_ESThermalDepth",heat,RenderTextureSubElement.Depth);
                cmd.SetRenderTarget(output); cmd.SetViewport(new Rect(0,0,size,size));
                cmd.DrawProcedural(Matrix4x4.identity,composite,1,MeshTopology.Triangles,3);
                Graphics.ExecuteCommandBuffer(cmd); cmd.Release();
                var old=RenderTexture.active; RenderTexture.active=output;
                var pixels=new Texture2D(size,size,TextureFormat.RGBAFloat,false,true);
                pixels.ReadPixels(new Rect(0,0,size,size),0,0); pixels.Apply(); RenderTexture.active=old;
                samples[present]=pixels.GetPixels();
                Color wall=pixels.GetPixel(size/5,size*3/4),door=pixels.GetPixel(size*2/5,size*3/4);
                Check(wall.r>.065f && wall.r<.6f && Mathf.Abs(wall.r-wall.b)<.025f,"architecture remains readable neutral gray at fixed exposure");
                if(light==0) Check(wall.r<.11f,"unlit architecture retains a dark black level instead of a gray veil");
                Check(wall.r-door.r>.008f,"wall and recessed doorway distinguishable even without scene light");
                for(int stair=0;stair<3;stair++)
                {
                    float a=pixels.GetPixel(size/5,(int)(size*(.05f+stair*.1f))).r;
                    float b=pixels.GetPixel(size/5,(int)(size*(.15f+stair*.1f))).r;
                    Check(a-b>.002f,"unlit stair treads retain depth separation");
                }
                if(present!=0) Check(pixels.GetPixel(size*3/4,size*3/4).r>.9f,"hot subject separates from gray architecture");
                if(size==128 && present==1)
                {
                    var preview=new Texture2D(size,size,TextureFormat.RGB24,false);
                    preview.SetPixels(pixels.GetPixels()); preview.Apply();
                    File.WriteAllBytes("architecture-"+light.ToString(System.Globalization.CultureInfo.InvariantCulture)+".png",preview.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(preview);
                }
                UnityEngine.Object.DestroyImmediate(pixels);
            }
            for(int y=0;y<size;y++) for(int x=0;x<size*3/5;x++)
                Check(Mathf.Abs(samples[0][y*size+x].r-samples[1][y*size+x].r)<.0001f,"actor entry does not remap the environment");
            foreach(var rt in new[]{scene,heat,output}) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
        }
        UnityEngine.Object.DestroyImmediate(sceneMaterial);
    }
    private static void Check(bool ok,string message) { checks++; if(!ok) throw new Exception(message); }
}
