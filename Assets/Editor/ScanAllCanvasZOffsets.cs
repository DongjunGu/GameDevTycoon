using UnityEditor;
using UnityEngine;

// FixHUDCanvasZOffset은 HUDCanvas 하나만 스캔한다 — 이 도구는 씬의 모든 루트 Canvas를 훑어서
// 로컬 Z가 0이 아닌 RectTransform을 전부 찾아 로그만 남긴다(수정 안 함, 조사 전용).
public static class ScanAllCanvasZOffsets
{
    static readonly string[] CanvasNames =
    {
        "HUDCanvas", "EndingCanvas", "MenuCanvas", "CreativityCanvas", "DialogCanvas", "PopupOverlayCanvas"
    };

    [MenuItem("Tools/GameDevTycoon/Fix All Canvas Z Offsets")]
    public static void Fix()
    {
        int total = 0;
        foreach (var name in CanvasNames)
        {
            var canvas = GameObject.Find(name);
            if (canvas == null)
            {
                Debug.LogWarning($"[ScanAllCanvasZOffsets] {name}를 찾을 수 없음 (씬에 없거나 다른 씬이 열려있음)");
                continue;
            }

            int fixedCount = 0;
            var rects = canvas.GetComponentsInChildren<RectTransform>(true);
            foreach (var rt in rects)
            {
                if (rt == canvas.transform) continue;
                if (Mathf.Approximately(rt.localPosition.z, 0f)) continue;

                Debug.Log($"[ScanAllCanvasZOffsets] FIX {GetPath(rt)} : z={rt.localPosition.z} → 0");
                var ap = rt.anchoredPosition3D;
                ap.z = 0f;
                rt.anchoredPosition3D = ap;
                EditorUtility.SetDirty(rt);
                fixedCount++;
                total++;
            }

            if (fixedCount > 0 && !Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.scene);
        }

        Debug.Log($"[ScanAllCanvasZOffsets] Fix 완료 — 총 {total}개 수정");
    }

    [MenuItem("Tools/GameDevTycoon/Scan All Canvas Z Offsets (report only)")]
    public static void Scan()
    {
        int total = 0;
        foreach (var name in CanvasNames)
        {
            var canvas = GameObject.Find(name);
            if (canvas == null)
            {
                Debug.LogWarning($"[ScanAllCanvasZOffsets] {name}를 찾을 수 없음 (씬에 없거나 다른 씬이 열려있음)");
                continue;
            }

            int found = 0;
            var rects = canvas.GetComponentsInChildren<RectTransform>(true);
            foreach (var rt in rects)
            {
                if (rt == canvas.transform) continue;
                if (Mathf.Approximately(rt.localPosition.z, 0f)) continue;

                Debug.LogWarning($"[ScanAllCanvasZOffsets] {GetPath(rt)} : z={rt.localPosition.z}");
                found++;
                total++;
            }

            Debug.Log($"[ScanAllCanvasZOffsets] {name}: {rects.Length}개 RectTransform 중 {found}개 z≠0");
        }

        Debug.Log($"[ScanAllCanvasZOffsets] 전체 완료 — 총 {total}개 z≠0 발견");
    }

    static string GetPath(Transform t)
    {
        var path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }
}
