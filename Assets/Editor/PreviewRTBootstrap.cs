using UnityEngine;
using UnityEditor;
using UnityEngine.Experimental.Rendering;

public static class PreviewRTBootstrap
{
    [MenuItem("Tools/Preview/Create RenderTexture")]
    public static void CreatePreviewRT()
    {
        const string folder = "Assets/RenderTextures";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "RenderTextures");

        const string path = "Assets/RenderTextures/PreviewRT.renderTexture";

        if (AssetDatabase.LoadAssetAtPath<RenderTexture>(path) != null)
            AssetDatabase.DeleteAsset(path);

        var desc = new RenderTextureDescriptor(512, 512, RenderTextureFormat.ARGB32, 16)
        {
            depthStencilFormat = GraphicsFormat.D24_UNorm_S8_UInt,
            useMipMap = false
        };
        var rt = new RenderTexture(desc)
        {
            name = "PreviewRT",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            antiAliasing = 1
        };
        AssetDatabase.CreateAsset(rt, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[PreviewRTBootstrap] Created {path} with depth buffer");
    }
}
