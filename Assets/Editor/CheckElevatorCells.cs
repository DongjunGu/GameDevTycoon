using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 엘리베이터 워프 배선을 잡기 위한 좌표 대조 도구 — elevator 타일맵에 칠해진 셀, 씬의 Elevator_* 스프라이트
/// 오브젝트가 어느 셀에 해당하는지, 그 주변이 걸어갈 수 있는 칸인지 한번에 찍는다.
/// </summary>
public static class CheckElevatorCells
{
    [MenuItem("Tools/GameDevTycoon/Check Elevator Cells (Edit Mode)")]
    public static void Check()
    {
        Tilemap ground = null, obstacle = null, elevator = null;
        foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string p = Path(tm.gameObject);
            if (p == "Grid/Level4/ground (1)")   ground   = tm;
            if (p == "Grid/Level4/obstacle (1)") obstacle = tm;
            if (p == "Grid/Level4/elevator")     elevator = tm;
        }
        if (ground == null || elevator == null) { Debug.LogError("[Elev] Level4 타일맵을 못 찾음"); return; }

        bool Walkable(Vector3Int c) =>
            (ground.HasTile(c) || elevator.HasTile(c)) && !(obstacle != null && obstacle.HasTile(c));

        Debug.Log("[Elev] ===== elevator 타일맵에 칠해진 셀 =====");
        foreach (var c in elevator.cellBounds.allPositionsWithin)
        {
            if (!elevator.HasTile(c)) continue;
            Vector3 w = ground.GetCellCenterWorld(c);
            Debug.Log($"[Elev] cell={c}  worldCenter=({w.x:F2}, {w.y:F2})  walkable={Walkable(c)}");
        }

        Debug.Log("[Elev] ===== 씬의 Elevator_* 스프라이트 오브젝트 =====");
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!go.name.StartsWith("Elevator")) continue;
            Vector3 pos = go.transform.position;
            Vector3Int cell = ground.WorldToCell(pos);
            Debug.Log($"[Elev] '{Path(go)}'  pos=({pos.x:F2},{pos.y:F2})  → cell={cell}  ground={ground.HasTile(cell)} obstacle={(obstacle != null && obstacle.HasTile(cell))} elevatorTile={elevator.HasTile(cell)}");

            // 발 밑에서 사방 2칸까지 걸어갈 수 있는 칸 나열 — 워프 도착 후보를 고르기 위함.
            var open = new List<string>();
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                {
                    var c = cell + new Vector3Int(dx, dy, 0);
                    if (Walkable(c)) open.Add($"({c.x},{c.y})");
                }
            Debug.Log($"[Elev]     주변 5x5 중 걸을 수 있는 칸: {string.Join(" ", open)}");
        }

        // GridManager 에 등록된 링크 현황
        var gm = Object.FindAnyObjectByType<GridManager>(FindObjectsInactive.Include);
        if (gm != null)
            foreach (var l in gm.elevatorLinks)
                Debug.Log($"[Elev] 등록된 링크: {l.cellA} <-> {l.cellB}  (A타일={elevator.HasTile(l.cellA)}, B타일={elevator.HasTile(l.cellB)})");
    }

    static string Path(GameObject go)
    {
        string p = go.name;
        var t = go.transform.parent;
        while (t != null) { p = t.name + "/" + p; t = t.parent; }
        return p;
    }
}
