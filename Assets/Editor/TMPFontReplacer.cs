using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// 프로젝트 전체에서 NeoDunggeunmoPro-Regular SDF 를 쓰는 TMP_Text 만 DNFBitBitTTF SDF 로 일괄 변경.
// - TMP 기본 폰트(TMP Settings) 변경
// - 모든 프리팹 + 모든 씬에서 .font 가 oldFont 인 TMP_Text 만 newFont 로 교체 (다른 폰트/아웃라인 머티리얼 커스텀은 그대로 유지)
// 메뉴: Tools/Font/Set All TMP To NeoDunggeunmo
public static class TMPFontReplacer
{
    const string OldFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/DNFBitBitTTF SDF.asset";
    const string NewFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/DNFBitBitv2 SDF.asset";
    const string SettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

    [MenuItem("Tools/Font/Set All TMP To NeoDunggeunmo")]
    public static void Run()
    {
        var oldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OldFontPath);
        var newFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NewFontPath);
        if (oldFont == null) { Debug.LogError("[TMPFont] 기존 폰트 못 찾음: " + OldFontPath); return; }
        if (newFont == null) { Debug.LogError("[TMPFont] 새 폰트 못 찾음: " + NewFontPath); return; }

        // 현재 열린 씬 먼저 저장 (작업 손실 방지)
        EditorSceneManager.SaveOpenScenes();
        string activeScenePath = SceneManager.GetActiveScene().path;

        // 1) TMP 기본 폰트 변경
        SetDefaultFont(newFont);

        // 2) 프리팹
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
                if (t.font == oldFont) { t.font = newFont; changed = true; prefabComps++; }
            }
            if (changed) { PrefabUtility.SaveAsPrefabAsset(root, path); prefabFiles++; }
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 3) 모든 씬
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
                    if (t.font == oldFont) { t.font = newFont; changed = true; sceneComps++; }

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
        Debug.Log($"[TMPFont] 완료 → DNFBitBitv2 SDF / 프리팹 {prefabFiles}파일({prefabComps} comp), 씬 {sceneFiles}파일({sceneComps} comp)");
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
