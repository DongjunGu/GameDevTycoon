using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using TMPro;

// 일회성 툴 — "-비용G" 등 돈 관련 TMP 텍스트에 인라인 아이콘(<sprite name="coin">)을 넣기 위한
// TMP_SpriteAsset 을 icon_coin.png 에서 생성한다. 실행 후 삭제해도 무방.
public static class CoinSpriteAssetCreator
{
    const string TexturePath = "Assets/Sprites/UI/DefaultUI/icon_coin.png";
    const string OutputDir   = "Assets/TextMesh Pro/Resources/Sprite Assets";
    const string OutputPath  = OutputDir + "/CoinSpriteAsset.asset";

    [MenuItem("Tools/GameDevTycoon/Create Coin Sprite Asset")]
    public static void Create()
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (texture == null) { Debug.LogError($"[CoinSpriteAssetCreator] 텍스처를 찾을 수 없음: {TexturePath}"); return; }

        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        bool reimport = false;
        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; reimport = true; }
        if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; reimport = true; }
        if (reimport) importer.SaveAndReimport();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturePath);
        if (sprite == null) { Debug.LogError($"[CoinSpriteAssetCreator] 스프라이트 로드 실패: {TexturePath}"); return; }

        var spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
        spriteAsset.name = "CoinSpriteAsset";
        spriteAsset.spriteSheet = texture;

        var glyph = new TMP_SpriteGlyph
        {
            index     = 0,
            metrics   = new GlyphMetrics(texture.width, texture.height, 0, texture.height * 0.8f, texture.width),
            glyphRect = new GlyphRect(0, 0, texture.width, texture.height),
            scale     = 1f,
            sprite    = sprite
        };

        var character = new TMP_SpriteCharacter(0xF8FF, glyph)
        {
            name  = "coin",
            scale = 1f
        };

        spriteAsset.spriteGlyphTable.Add(glyph);
        spriteAsset.spriteCharacterTable.Add(character);
        spriteAsset.UpdateLookupTables();

        // m_Version 을 안 채우면 다음 로드 시 TMP 가 "구버전(spriteInfoList) → 신버전 업그레이드"를 돌리면서
        // 방금 채운 spriteCharacterTable/spriteGlyphTable 을 비워버린다. private field라 SerializedObject로 직접 설정.
        var so = new SerializedObject(spriteAsset);
        so.FindProperty("m_Version").stringValue = "1.1.0";
        so.ApplyModifiedPropertiesWithoutUndo();

        var material = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "CoinSpriteAsset Material" };
        material.mainTexture = texture;
        spriteAsset.material = material;

        if (!Directory.Exists(OutputDir)) Directory.CreateDirectory(OutputDir);
        if (AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(OutputPath) != null)
            AssetDatabase.DeleteAsset(OutputPath); // 재실행 대비 — 기존(잘못 만들어졌을 수 있는) 에셋 제거 후 재생성
        AssetDatabase.CreateAsset(spriteAsset, OutputPath);
        AssetDatabase.AddObjectToAsset(material, spriteAsset);

        EditorUtility.SetDirty(spriteAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[CoinSpriteAssetCreator] 생성 완료: {OutputPath}");
    }

    [MenuItem("Tools/GameDevTycoon/Test Coin Sprite Asset Load")]
    public static void TestLoad()
    {
        var viaResources = Resources.Load<TMP_SpriteAsset>("Sprite Assets/CoinSpriteAsset");
        Debug.Log(viaResources != null
            ? $"[CoinSpriteAssetCreator] Resources.Load 성공: {viaResources.name}, chars={viaResources.spriteCharacterTable.Count}"
            : "[CoinSpriteAssetCreator] Resources.Load 실패 — null");

        var viaAssetDb = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(OutputPath);
        Debug.Log(viaAssetDb != null
            ? $"[CoinSpriteAssetCreator] AssetDatabase 로드 성공: {viaAssetDb.name}, material={(viaAssetDb.material != null ? viaAssetDb.material.name : "null")}"
            : "[CoinSpriteAssetCreator] AssetDatabase 로드 실패 — null");

        Debug.Log($"[CoinSpriteAssetCreator] TMP_Settings.defaultSpriteAssetPath = '{TMP_Settings.defaultSpriteAssetPath}'");
    }

    [MenuItem("Tools/GameDevTycoon/Test AlertUI4 Coin Icon (Play Mode)")]
    public static void TestAlertUI4()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[CoinSpriteAssetCreator] Play 모드에서만 실행 가능"); return; }
        if (AlertUI.Instance == null) { Debug.LogWarning("[CoinSpriteAssetCreator] AlertUI.Instance 없음"); return; }
        AlertUI.Instance.ShowResult4("테스트", "<sprite=\"CoinSpriteAsset\" name=\"coin\"> <color=#F3C01D>-3000G</color>", "", "");
    }

    // 임시 검증용 — 서로 다른 pill 카테고리(개발기간+돈) 2개가 한 메시지에 섞였을 때 fallbackSpriteAssets
    // 체인이 제대로 동작하는지(다른 텍스처를 가리키는 두 번째 아이콘이 깨지지 않는지) 확인. 확인 후 삭제해도 무방.
    [MenuItem("Tools/GameDevTycoon/Test AlertUI Pill Mix (Play Mode)")]
    public static void TestAlertPillMix()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[CoinSpriteAssetCreator] Play 모드에서만 실행 가능"); return; }
        if (AlertUI.Instance == null) { Debug.LogWarning("[CoinSpriteAssetCreator] AlertUI.Instance 없음"); return; }
        AlertUI.Instance.ShowResult6("의문의 투자 제안", "{개발기간} +2주 연장됐습니다.", "{돈}+총 연봉 *10% G 증가했습니다");
    }
}
