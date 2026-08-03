using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// 프로젝트 전체(모든 프리팹 + 모든 씬)에서 지정한 Old Font 를 쓰는 TMP_Text 만 New Font 로 일괄 변경.
// 메뉴: Tools/Font/Font Replacer
public class TMPFontReplacer : EditorWindow
{
    const string SettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

    TMP_FontAsset oldFont;
    TMP_FontAsset newFont;
    Material newMaterial;
    bool alsoSetDefaultFont = true;

    TMP_FontAsset filterTargetFont;

    [MenuItem("Tools/Font/Font Replacer")]
    static void Open()
    {
        GetWindow<TMPFontReplacer>("Font Replacer");
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("TMP 폰트 일괄 교체", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        oldFont = (TMP_FontAsset)EditorGUILayout.ObjectField("Old Font (바꿀 대상)", oldFont, typeof(TMP_FontAsset), false);
        newFont = (TMP_FontAsset)EditorGUILayout.ObjectField("New Font (바뀔 결과)", newFont, typeof(TMP_FontAsset), false);
        newMaterial = (Material)EditorGUILayout.ObjectField("New Material (선택, 비우면 머티리얼은 안 건드림)", newMaterial, typeof(Material), false);
        alsoSetDefaultFont = EditorGUILayout.Toggle("TMP Settings 기본 폰트도 변경", alsoSetDefaultFont);

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(oldFont == null || newFont == null))
        {
            if (GUILayout.Button("교체 실행", GUILayout.Height(30)))
                Run(oldFont, newFont, newMaterial, alsoSetDefaultFont);
        }

        if (oldFont != null && newFont != null && oldFont == newFont)
            EditorGUILayout.HelpBox("Old Font 와 New Font 가 같습니다.", MessageType.Warning);

        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("픽셀폰트 아틀라스 Filter Mode → Point", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("임베디드 아틀라스 텍스처는 Import Settings가 없어서 여기서 Point(no filter)로 강제 설정해야 함.", MessageType.Info);

        filterTargetFont = (TMP_FontAsset)EditorGUILayout.ObjectField("대상 Font", filterTargetFont, typeof(TMP_FontAsset), false);

        using (new EditorGUI.DisabledScope(filterTargetFont == null))
        {
            if (GUILayout.Button("Filter Mode를 Point로 설정", GUILayout.Height(30)))
                SetAtlasFilterToPoint(filterTargetFont);
        }
    }

    static void SetAtlasFilterToPoint(TMP_FontAsset font)
    {
        int changed = 0;
        var textures = new List<Texture2D> { font.atlasTexture };
        if (font.atlasTextures != null) textures.AddRange(font.atlasTextures);

        foreach (var tex in textures)
        {
            if (tex == null || tex.filterMode == FilterMode.Point) continue;
            tex.filterMode = FilterMode.Point;
            EditorUtility.SetDirty(tex);
            changed++;
        }

        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        Debug.Log($"[TMPFont] {font.name} 아틀라스 텍스처 {changed}개 Filter Mode → Point");
    }

    static void Run(TMP_FontAsset oldFont, TMP_FontAsset newFont, Material newMaterial, bool alsoSetDefaultFont)
    {
        // 현재 열린 씬 먼저 저장 (작업 손실 방지)
        EditorSceneManager.SaveOpenScenes();
        string activeScenePath = SceneManager.GetActiveScene().path;

        if (alsoSetDefaultFont) SetDefaultFont(newFont);

        // 프리팹
        int prefabFiles = 0, prefabComps = 0;
        foreach (var g in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (IsExcluded(path)) continue;

            GameObject root = null;
            try { root = PrefabUtility.LoadPrefabContents(path); }
            catch { continue; }
            if (root == null) continue;

            bool changed = false;
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.font != oldFont) continue;
                t.font = newFont;
                if (newMaterial != null) t.fontSharedMaterial = newMaterial;
                changed = true;
                prefabComps++;
            }
            if (changed) { PrefabUtility.SaveAsPrefabAsset(root, path); prefabFiles++; }
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 모든 씬
        int sceneFiles = 0, sceneComps = 0;
        var scenePaths = new List<string>();
        foreach (var g in AssetDatabase.FindAssets("t:Scene"))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (IsExcluded(path)) continue;
            scenePaths.Add(path);
        }

        foreach (var sp in scenePaths)
        {
            Scene scene;
            try { scene = EditorSceneManager.OpenScene(sp, OpenSceneMode.Single); }
            catch { continue; }

            bool changed = false;
            foreach (var rootGo in scene.GetRootGameObjects())
                foreach (var t in rootGo.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t.font != oldFont) continue;
                    t.font = newFont;
                    if (newMaterial != null) t.fontSharedMaterial = newMaterial;
                    changed = true;
                    sceneComps++;
                }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                sceneFiles++;
            }
        }

        // 원래 씬으로 복귀
        if (!string.IsNullOrEmpty(activeScenePath))
            EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TMPFont] {oldFont.name} → {newFont.name} 완료 / 프리팹 {prefabFiles}파일({prefabComps} comp), 씬 {sceneFiles}파일({sceneComps} comp)");
    }

    // TextMesh Pro 예제/패키지 콘텐츠는 제외
    static bool IsExcluded(string path)
    {
        return path.Contains("/Examples") || path.Contains("Examples & Extras") || path.StartsWith("Packages/");
    }

    static void SetDefaultFont(TMP_FontAsset font)
    {
        var settings = AssetDatabase.LoadAssetAtPath<Object>(SettingsPath);
        if (settings == null) { Debug.LogWarning("[TMPFont] TMP Settings 못 찾음 — 기본폰트 변경 스킵"); return; }
        var so = new SerializedObject(settings);
        var prop = so.FindProperty("m_defaultFontAsset");
        if (prop != null)
        {
            prop.objectReferenceValue = font;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            Debug.Log("[TMPFont] TMP 기본 폰트 변경 완료");
        }
    }
}
