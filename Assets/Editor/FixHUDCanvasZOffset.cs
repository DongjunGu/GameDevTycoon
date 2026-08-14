using UnityEditor;
using UnityEngine;

// HUDCanvas(ScreenSpaceCamera)는 자식 로컬 Z가 0이 아니면 카메라 프러스텀 밖으로 밀려나 Scene 뷰엔
// 보이는데 Game 뷰에선 안 보이는 버그로 이어진다(QuestItemSimpleforTest1/2가 이 케이스로 실제 겪음,
// anchoredPosition3D.z=-12960). 도메인 리로드마다 자동으로 훑어서 0이 아닌 걸 0으로 되돌린다(멱등이라
// 매번 재실행해도 이미 고쳐진 건 건드리지 않음) — RoleIconSpriteAssetCreator의 AutoXxxIfMissing과 동일 패턴.
public static class FixHUDCanvasZOffset
{
    [MenuItem("Tools/GameDevTycoon/Fix HUDCanvas Z Offsets")]
    public static void Fix()
    {
        var hud = GameObject.Find("HUDCanvas");
        if (hud == null) { Debug.LogWarning("[FixHUDCanvasZOffset] HUDCanvas를 찾을 수 없음"); return; }

        int fixedCount = 0;
        var rects = hud.GetComponentsInChildren<RectTransform>(true);
        foreach (var rt in rects)
        {
            if (rt == hud.transform) continue; // 루트 캔버스 자체는 Canvas 컴포넌트가 매 프레임 구동하므로 제외
            if (Mathf.Approximately(rt.localPosition.z, 0f)) continue;

            Debug.Log($"[FixHUDCanvasZOffset] {GetPath(rt)} : z={rt.localPosition.z} → 0");
            var ap = rt.anchoredPosition3D;
            ap.z = 0f;
            rt.anchoredPosition3D = ap;
            EditorUtility.SetDirty(rt);
            fixedCount++;
        }

        if (fixedCount > 0)
        {
            EditorSceneManager_MarkSceneDirtySafe(hud);
            Debug.Log($"[FixHUDCanvasZOffset] 완료 — {fixedCount}개 수정");
        }
        else
        {
            Debug.Log("[FixHUDCanvasZOffset] 완료 — 수정할 것 없음(전부 z=0)");
        }
    }

    static void EditorSceneManager_MarkSceneDirtySafe(GameObject go)
    {
        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
    }

    static string GetPath(Transform t)
    {
        var path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }

    // MCP 등 외부에서 execute_menu_item(포커스 필요)을 못 쓰는 경우를 위해 도메인 리로드마다 자동 실행 —
    // 멱등이라 이미 전부 z=0이면 로그 한 줄 찍고 끝(무해).
    [InitializeOnLoadMethod]
    static void AutoFixOnLoad()
    {
        if (Application.isPlaying) return;
        Fix();
    }
}
