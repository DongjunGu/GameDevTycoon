using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Level4 위층: 엘리베이터가 내려주는 오른쪽 구역(CEO실)과 왼쪽 사무공간(desk_09/11/sec, patrol p3)이
/// obstacle 타일로 완전히 끊겨 있어서, 엘리베이터로 위층에 올라와도 사무공간 쪽으로 걸어갈 수 없었다.
/// 두 구역 경계에서 ground가 깔려 있으면서 obstacle만 막고 있는 칸은 (4,3) 하나뿐이라 그 칸만 연다.
/// </summary>
public static class OpenUpperFloorPassage
{
    static readonly Vector3Int PassageCell = new Vector3Int(4, 3, 0);

    [MenuItem("Tools/GameDevTycoon/Open Upper Floor Passage (4,3)")]
    public static void Open()
    {
        Tilemap obstacle = null;
        foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string p = tm.gameObject.name;
            var t = tm.transform.parent;
            while (t != null) { p = t.name + "/" + p; t = t.parent; }
            if (p == "Grid/Level4/obstacle (1)") { obstacle = tm; break; }
        }
        if (obstacle == null) { Debug.LogError("[Passage] Grid/Level4/obstacle (1) 타일맵을 못 찾음"); return; }

        var before = obstacle.GetTile(PassageCell);
        if (before == null) { Debug.Log($"[Passage] {PassageCell} 에 이미 obstacle 타일이 없음 — 변경 없음"); return; }

        Undo.RecordObject(obstacle, "Open upper floor passage");
        obstacle.SetTile(PassageCell, null);
        EditorUtility.SetDirty(obstacle);
        EditorSceneManager.MarkSceneDirty(obstacle.gameObject.scene);

        Debug.Log($"[Passage] {PassageCell} 의 obstacle 타일 '{before.name}' 제거 — 위층 좌우 구역 연결");
    }
}
