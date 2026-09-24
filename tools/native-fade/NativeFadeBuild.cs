using System;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

// Build only the authored shader resource used by the plugin.
public static class NativeFadeBuild
{
    public static void Run()
    {
        AssetBundle bundle = null;
        try
        {
            const string asset = "Assets/NativeFadeComposite.shader";
            AssetDatabase.Refresh();
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(asset);
            if (shader == null) throw new Exception("Composite shader asset is missing.");
            foreach (var error in ShaderUtil.GetShaderMessages(shader))
                if (error.severity == ShaderCompilerMessageSeverity.Error)
                    throw new Exception(error.message);
            Directory.CreateDirectory("Output");
            var manifest = BuildPipeline.BuildAssetBundles("Output", new[] {
                new AssetBundleBuild { assetBundleName = "nativefade.bundle", assetNames = new[] { asset } }
            }, BuildAssetBundleOptions.ForceRebuildAssetBundle | BuildAssetBundleOptions.ChunkBasedCompression,
                BuildTarget.StandaloneWindows64);
            if (manifest == null) throw new Exception("Asset bundle build failed.");
            bundle = AssetBundle.LoadFromFile("Output/nativefade.bundle");
            if (bundle == null || bundle.GetAllAssetNames().Length != 1 || bundle.LoadAsset<Shader>(asset) == null)
                throw new Exception("Built shader bundle could not be verified.");
            Debug.Log("Native fade shader bundle built and verified.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
        finally
        {
            if (bundle != null) bundle.Unload(true);
        }
    }
}
