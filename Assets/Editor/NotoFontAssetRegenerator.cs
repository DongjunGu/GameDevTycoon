using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// NotoSansKR SDF 폰트 에셋을 올바른 설정으로 재생성한다.
/// 기존 에셋 파일을 그대로 덮어쓰기 때문에 에셋 GUID와 머티리얼 fileID가 유지되어
/// 씬/프리팹의 참조가 끊기지 않는다.
///
/// 기존 에셋은 13,000자를 4096x4096 한 장에 Static으로 굽다가 Auto Sizing이
/// 샘플링 크기를 1px까지 떨어뜨려 글리프 렉트가 1x1이 되었고, 그 결과 글자가
/// 흰 사각형으로 렌더링되었다.
///
/// 한글 음절 11,172자를 전부 구우면 2048x2048 아틀라스가 17장(약 68MB) 필요하므로
/// 프로젝트 텍스트에서 실제로 쓰이는 글자만 굽고, 나머지는 Dynamic 모드로
/// 런타임에 채운다. 텍스트를 추가한 뒤 다시 구우려면 메뉴를 한 번 더 실행하면 된다.
/// </summary>
public static class NotoFontAssetRegenerator
{
    const int SamplingPointSize = 48;
    const int AtlasPadding = 5;
    const int AtlasSize = 2048;

    [MenuItem("Tools/TMP/Regenerate NotoSansKR Font Assets")]
    public static void RegenerateAll()
    {
        string characterSet = BuildCharacterSet();

        Rebuild("Assets/TextMesh Pro/Fonts/NotoSansKR-Regular.ttf",
                "Assets/TextMesh Pro/Resources/Fonts & Materials/NotoSansKR-Regular SDF.asset",
                characterSet);
        Rebuild("Assets/TextMesh Pro/Fonts/NotoSansKR-Bold.ttf",
                "Assets/TextMesh Pro/Resources/Fonts & Materials/NotoSansKR-Bold SDF.asset",
                characterSet);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 아스키 + 한글 자모에 더해, Assets 안의 텍스트에서 실제로 쓰이는
    /// 한글 음절과 기호를 모아 구울 글자 집합을 만든다.
    /// </summary>
    static string BuildCharacterSet()
    {
        var chars = new SortedSet<char>();

        for (char c = (char)32; c <= (char)126; c++) chars.Add(c);   // ASCII
        for (char c = (char)0x3131; c <= (char)0x318E; c++) chars.Add(c); // 한글 자모

        string[] extensions = { ".cs", ".csv", ".json", ".txt", ".asset", ".unity", ".prefab" };

        foreach (string path in Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories))
        {
            if (System.Array.IndexOf(extensions, Path.GetExtension(path).ToLowerInvariant()) < 0) continue;
            if (path.Replace('\\', '/').Contains("Assets/TextMesh Pro/")) continue;
            if (new FileInfo(path).Length > 8 * 1024 * 1024) continue;

            string text;
            try { text = File.ReadAllText(path, Encoding.UTF8); }
            catch { continue; }

            foreach (char c in text)
            {
                if (c >= 0xAC00 && c <= 0xD7A3) chars.Add(c);       // 한글 음절
                else if (c >= 0x2000 && c <= 0x26FF) chars.Add(c);  // 문장부호/화살표/기호
                else if (c >= 0x00A0 && c <= 0x00FF) chars.Add(c);  // 라틴 보충 (° × ÷ 등)
            }
        }

        var sb = new StringBuilder(chars.Count);
        foreach (char c in chars) sb.Append(c);
        return sb.ToString();
    }

    static void Rebuild(string ttfPath, string assetPath, string characterSet)
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (font == null)
        {
            Debug.LogError($"[NotoFontAssetRegenerator] 소스 폰트를 찾을 수 없음: {ttfPath}");
            return;
        }

        var target = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (target == null)
        {
            Debug.LogError($"[NotoFontAssetRegenerator] 폰트 에셋을 찾을 수 없음: {assetPath}");
            return;
        }

        var generated = TMP_FontAsset.CreateFontAsset(
            font, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA,
            AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);

        if (generated == null)
        {
            Debug.LogError($"[NotoFontAssetRegenerator] 생성 실패: {ttfPath} (ttf 임포터의 Include Font Data 확인)");
            return;
        }

        // 새로 만든 아틀라스/머티리얼은 CopySerialized 전에 잡아둔다.
        // (복사 후 target 쪽 참조는 신뢰할 수 없다)
        var atlas = generated.atlasTextures[0];
        var generatedMaterial = generated.material;

        // 씬에서 직접 참조 중일 수 있으므로 기존 머티리얼 인스턴스는 그대로 재사용한다.
        // 기존 머티리얼이 사라진 경우에만 새로 만든 것을 서브에셋으로 승격시킨다.
        var keptMaterial = target.material;
        bool promoteGeneratedMaterial = keptMaterial == null;
        if (promoteGeneratedMaterial)
            keptMaterial = generatedMaterial;

        // 기존 아틀라스 텍스처 서브에셋 제거 (머티리얼은 남긴다)
        foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (sub == null || sub == target || sub == keptMaterial) continue;
            Object.DestroyImmediate(sub, true);
        }

        EditorUtility.CopySerialized(generated, target);
        target.name = Path.GetFileNameWithoutExtension(assetPath);

        atlas.name = target.name + " Atlas";
        AssetDatabase.AddObjectToAsset(atlas, target);
        target.atlasTextures = new[] { atlas };

        if (promoteGeneratedMaterial)
        {
            keptMaterial.name = target.name + " Material";
            AssetDatabase.AddObjectToAsset(keptMaterial, target);
        }

        // 기존 머티리얼을 새 아틀라스로 재조준하고, 복사로 딸려온 머티리얼은 버린다.
        keptMaterial.SetTexture(ShaderUtilities.ID_MainTex, atlas);
        keptMaterial.SetFloat(ShaderUtilities.ID_TextureWidth, AtlasSize);
        keptMaterial.SetFloat(ShaderUtilities.ID_TextureHeight, AtlasSize);
        keptMaterial.SetFloat(ShaderUtilities.ID_GradientScale, AtlasPadding + 1);
        ShaderUtilities.UpdateShaderRatios(keptMaterial);
        target.material = keptMaterial;

        target.ReadFontAssetDefinition();

        // 프로젝트에서 쓰는 글자를 미리 구워둔다. 못 구운 글자는 Dynamic으로 런타임 처리.
        target.TryAddCharacters(characterSet, out string missing);

        // 빌드 시 구워둔 글리프가 지워지지 않도록 한다.
        var so = new SerializedObject(target);
        so.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Font Asset Creator를 열었을 때 설정이 비어 보이지 않도록 복원
        var settings = target.creationSettings;
        settings.sourceFontFileName = Path.GetFileName(ttfPath);
        settings.pointSizeSamplingMode = 1;
        settings.pointSize = SamplingPointSize;
        settings.padding = AtlasPadding;
        settings.packingMode = 4;
        settings.atlasWidth = AtlasSize;
        settings.atlasHeight = AtlasSize;
        settings.characterSetSelectionMode = 8; // Characters from File / custom
        settings.renderMode = (int)GlyphRenderMode.SDFAA;
        target.creationSettings = settings;

        // 새로 늘어난 아틀라스도 서브에셋으로 등록
        foreach (var tex in target.atlasTextures)
        {
            if (tex == null || tex == atlas) continue;
            if (AssetDatabase.GetAssetPath(tex) == assetPath) continue;
            tex.name = target.name + " Atlas " + System.Array.IndexOf(target.atlasTextures, tex);
            AssetDatabase.AddObjectToAsset(tex, target);
            EditorUtility.SetDirty(tex);
        }

        EditorUtility.SetDirty(atlas);
        EditorUtility.SetDirty(keptMaterial);
        EditorUtility.SetDirty(target);

        Object.DestroyImmediate(generated);
        if (generatedMaterial != null && generatedMaterial != keptMaterial)
            Object.DestroyImmediate(generatedMaterial);

        Debug.Log($"[NotoFontAssetRegenerator] 재생성 완료: {target.name} " +
                  $"(pointSize={target.faceInfo.pointSize}, padding={AtlasPadding}, " +
                  $"굽힌 글자={target.characterTable.Count}, 아틀라스={target.atlasTextureCount}장 {AtlasSize}x{AtlasSize}, " +
                  $"실패={(string.IsNullOrEmpty(missing) ? 0 : missing.Length)}자, Dynamic 폴백 유지)");
    }
}
