using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using TMPro;

// 튜토리얼 대사([[기획]]/[[개발]]/[[아트]] 등) 인라인 아이콘용 TMP_SpriteAsset 3종을 생성하는 툴.
// 어떤 스프라이트를 아이콘으로 쓸지는 창에서 직접 골라서 지정(기본값은 RoleIconSet과 같은 소스로 미리 채워둠).
// 사용: Tools/GameDevTycoon/Role Icon Sprite Asset Creator 로 창을 연 뒤 스프라이트 지정 → 생성 버튼.
// 텍스트에서는 <sprite="PlanIconSpriteAsset" name="plan"> 등으로 사용.
public class RoleIconSpriteAssetCreator : EditorWindow
{
    const string OutputDir = "Assets/TextMesh Pro/Resources/Sprite Assets";

    Sprite _planSprite;
    Sprite _devSprite;
    Sprite _artSprite;
    float  _iconSize = 30f;

    [MenuItem("Tools/GameDevTycoon/Role Icon Sprite Asset Creator")]
    public static void Open()
    {
        var win = GetWindow<RoleIconSpriteAssetCreator>("역할 아이콘 스프라이트 생성");
        if (win._planSprite == null)
            win._planSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/DefaultUI/Job_Plan_s.png");
        if (win._devSprite == null)
            win._devSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/DefaultUI/Job_Dev_s.png");
        if (win._artSprite == null)
            win._artSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/DefaultUI/Job_Art_s.png");
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "TutorialDialog 3-2 대사에서 [[기획]]/[[개발]]/[[아트]] 앞에 붙는 인라인 아이콘 스프라이트를 직접 지정하세요.\n" +
            "생성 버튼을 누르면 PlanIconSpriteAsset / DevIconSpriteAsset / ArtIconSpriteAsset (TMP_SpriteAsset)이\n" +
            OutputDir + " 에 새로 만들어집니다(기존 것은 덮어씀).",
            MessageType.Info);

        EditorGUILayout.Space();
        _planSprite = (Sprite)EditorGUILayout.ObjectField("기획 아이콘", _planSprite, typeof(Sprite), false);
        _devSprite  = (Sprite)EditorGUILayout.ObjectField("개발 아이콘", _devSprite, typeof(Sprite), false);
        _artSprite  = (Sprite)EditorGUILayout.ObjectField("아트 아이콘", _artSprite, typeof(Sprite), false);
        _iconSize   = EditorGUILayout.FloatField("아이콘 크기(긴 변 기준, px)", _iconSize);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(_planSprite == null || _devSprite == null || _artSprite == null))
        {
            if (GUILayout.Button("생성 / 재생성", GUILayout.Height(28)))
            {
                CreateOne(_planSprite, "PlanIconSpriteAsset", "plan", _iconSize);
                CreateOne(_devSprite,  "DevIconSpriteAsset",  "dev", _iconSize);
                CreateOne(_artSprite,  "ArtIconSpriteAsset",  "art", _iconSize);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[RoleIconSpriteAssetCreator] 3개 아이콘 스프라이트 에셋 생성 완료");
            }
        }
    }

    // size: 아이콘의 긴 변(가로/세로 중 큰 쪽) 기준 렌더 크기(px, TMP 기준 폰트 크기와 같은 단위) — 원본 비율은 유지.
    static void CreateOne(Sprite sprite, string assetName, string spriteName, float size)
    {
        if (sprite == null) return;
        var texture = sprite.texture;
        var rect    = sprite.rect; // 아틀라스에 패킹된 스프라이트도 지원하도록 전체 텍스처가 아닌 서브렉트 사용

        float scale  = size / Mathf.Max(rect.width, rect.height);
        float glyphW = rect.width  * scale;
        float glyphH = rect.height * scale;

        var outputPath = $"{OutputDir}/{assetName}.asset";

        var spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
        spriteAsset.name = assetName;
        spriteAsset.spriteSheet = texture;

        var glyph = new TMP_SpriteGlyph
        {
            index     = 0,
            metrics   = new GlyphMetrics(glyphW, glyphH, 0, glyphH * 0.8f, glyphW),
            glyphRect = new GlyphRect((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height),
            scale     = 1f,
            sprite    = sprite
        };

        var character = new TMP_SpriteCharacter(0xF8FF, glyph)
        {
            name  = spriteName,
            scale = 1f
        };

        spriteAsset.spriteGlyphTable.Add(glyph);
        spriteAsset.spriteCharacterTable.Add(character);
        spriteAsset.UpdateLookupTables();

        // m_Version 미설정 시 다음 로드에서 TMP가 구버전 업그레이드 루틴을 돌리며 방금 채운 테이블을 비움.
        var so = new SerializedObject(spriteAsset);
        so.FindProperty("m_Version").stringValue = "1.1.0";
        so.ApplyModifiedPropertiesWithoutUndo();

        var material = new Material(Shader.Find("TextMeshPro/Sprite")) { name = $"{assetName} Material" };
        material.mainTexture = texture;
        spriteAsset.material = material;

        if (!Directory.Exists(OutputDir)) Directory.CreateDirectory(OutputDir);
        if (AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(outputPath) != null)
            AssetDatabase.DeleteAsset(outputPath);
        AssetDatabase.CreateAsset(spriteAsset, outputPath);
        AssetDatabase.AddObjectToAsset(material, spriteAsset);

        EditorUtility.SetDirty(spriteAsset);
        Debug.Log($"[RoleIconSpriteAssetCreator] 생성 완료: {outputPath}");
    }

    // execute_menu_item(MCP)이 에디터 창 포커스가 없으면 조용히 실패하는 문제로(창을 열어 버튼을 누르는
    // 수동 조작을 어시스턴트가 대신할 수 없음) — 기본 소스 스프라이트 기준으로 아이콘 크기를 맞출 때는
    // 도메인 리로드마다 자동 실행되는 이 훅으로 재생성. 기존 에셋의 glyph 폭이 이미 목표 크기와 같으면 스킵.
    const float DefaultIconSize = 30f;

    [InitializeOnLoadMethod]
    static void AutoResizeDefaultsIfNeeded()
    {
        var plan = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/DefaultUI/Job_Plan_s.png");
        var dev  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/DefaultUI/Job_Dev_s.png");
        var art  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/DefaultUI/Job_Art_s.png");
        if (plan == null || dev == null || art == null) return;

        bool changed = false;
        changed |= RegenIfSizeMismatch(plan, "PlanIconSpriteAsset", "plan");
        changed |= RegenIfSizeMismatch(dev,  "DevIconSpriteAsset",  "dev");
        changed |= RegenIfSizeMismatch(art,  "ArtIconSpriteAsset",  "art");

        if (changed)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RoleIconSpriteAssetCreator] AutoResizeDefaultsIfNeeded 로 아이콘 크기 30 자동 반영됨");
        }
    }

    // "_L"(대형) 역할 아이콘 세트 — ResumeUI용 원본(Job_{Plan|Dev|Art}_L.png)을 그대로 인라인 스프라이트로.
    // 위 "_s" 세트와 별개 에셋(PlanIconSpriteAsset을 덮어쓰지 않음) — 퀘스트 아이템처럼 큰 아이콘이 필요한
    // 곳 전용. 텍스트에서는 <sprite="PlanIconLSpriteAsset" name="planL"> 등으로 사용.
    [InitializeOnLoadMethod]
    static void AutoCreateLargeIconsIfMissing()
    {
        var planL = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/ResumeUI/Job_Plan_L.png");
        var devL  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/ResumeUI/Job_Dev_L.png");
        var artL  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/ResumeUI/Job_Art_L.png");
        if (planL == null || devL == null || artL == null) return;

        bool changed = false;
        changed |= RegenIfSizeMismatch(planL, "PlanIconLSpriteAsset", "planL");
        changed |= RegenIfSizeMismatch(devL,  "DevIconLSpriteAsset",  "devL");
        changed |= RegenIfSizeMismatch(artL,  "ArtIconLSpriteAsset",  "artL");

        if (changed)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RoleIconSpriteAssetCreator] AutoCreateLargeIconsIfMissing 로 _L 아이콘 스프라이트 에셋 생성됨");
        }
    }

    static bool RegenIfSizeMismatch(Sprite sprite, string assetName, string spriteName)
    {
        var outputPath = $"{OutputDir}/{assetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(outputPath);
        if (existing != null && existing.spriteGlyphTable.Count > 0)
        {
            var m = existing.spriteGlyphTable[0].metrics;
            if (Mathf.Approximately(Mathf.Max(m.width, m.height), DefaultIconSize))
                return false;
        }

        CreateOne(sprite, assetName, spriteName, DefaultIconSize);
        return true;
    }

    [MenuItem("Tools/GameDevTycoon/Test Role Icon Sprite Assets Load")]
    public static void TestLoad()
    {
        foreach (var assetName in new[] { "PlanIconSpriteAsset", "DevIconSpriteAsset", "ArtIconSpriteAsset" })
        {
            var asset = Resources.Load<TMP_SpriteAsset>($"Sprite Assets/{assetName}");
            Debug.Log(asset != null
                ? $"[RoleIconSpriteAssetCreator] Resources.Load 성공: {asset.name}, chars={asset.spriteCharacterTable.Count}"
                : $"[RoleIconSpriteAssetCreator] Resources.Load 실패: {assetName}");
        }
    }
}
