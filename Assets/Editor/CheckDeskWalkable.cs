using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 에디터(비 Play) 모드에서 씬의 ground/obstacle 타일맵을 직접 읽어, 각 desk의 착석 지점과 patrol
/// 지점이 실제로 걸어갈 수 있는 셀인지 검사한다. GridManager.Instance/DeskManager 없이 씬 상태만
/// 보므로 Play를 켜지 않고도 확인 가능. 판정식은 GridManager.IsWalkable과 동일:
/// (ground|stair|elevator 타일 존재) && !(obstacle 타일 존재).
/// </summary>
public static class CheckDeskWalkable
{
    [MenuItem("Tools/GameDevTycoon/Check Desk Walkable (Edit Mode)")]
    public static void Check()
    {
        CheckLevel("Level1", "Grid/Level1/ground", "Grid/Level1/obstacle", "Grid/Level1/stair", null);
        CheckLevel("Level4", "Grid/Level4/ground (1)", "Grid/Level4/obstacle (1)", null, "Grid/Level4/elevator");
        CheckElevatorCells.Check();
    }

    static Tilemap Find(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var go = GameObject.Find(path);
        if (go == null)
        {
            // 비활성 오브젝트는 GameObject.Find로 못 잡으므로 전체 스캔으로 폴백.
            foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (GetPath(tm.gameObject) == path) return tm;
            return null;
        }
        return go.GetComponent<Tilemap>();
    }

    static string GetPath(GameObject go)
    {
        string p = go.name;
        var t = go.transform.parent;
        while (t != null) { p = t.name + "/" + p; t = t.parent; }
        return p;
    }

    static void CheckLevel(string levelName, string groundPath, string obstaclePath, string stairPath, string elevatorPath)
    {
        var ground   = Find(groundPath);
        var obstacle = Find(obstaclePath);
        var stair    = Find(stairPath);
        var elevator = Find(elevatorPath);

        if (ground == null) { Debug.LogWarning($"[DeskCheck] {levelName}: ground 타일맵({groundPath})을 못 찾음 — 스킵"); return; }

        bool Walkable(Vector3Int c)
        {
            bool hasGround = ground.HasTile(c)
                          || (stair != null && stair.HasTile(c))
                          || (elevator != null && elevator.HasTile(c));
            bool hasObstacle = obstacle != null && obstacle.HasTile(c);
            return hasGround && !hasObstacle;
        }

        Debug.Log($"[DeskCheck] ===== {levelName} (ground={ground.name}, obstacle={(obstacle != null ? obstacle.name : "none")}) =====");

        string prefix = "Grid/" + levelName + "/";

        // ---- desk 착석 지점 ----
        var stations = Object.FindObjectsByType<WorkStation>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(w => GetPath(w.gameObject).StartsWith(prefix))
            .OrderBy(w => w.deskId).ToList();

        foreach (var ws in stations)
        {
            if (ws.workPoint == null) { Debug.LogError($"[DeskCheck] {ws.deskId}: workPoint 미배선"); continue; }

            Vector3 world = ws.workPoint.position + ws.workPointOffset;
            Vector3Int cell = ground.WorldToCell(world);

            bool g   = ground.HasTile(cell);
            bool s   = stair != null && stair.HasTile(cell);
            bool e   = elevator != null && elevator.HasTile(cell);
            bool obs = obstacle != null && obstacle.HasTile(cell);
            bool ok  = (g || s || e) && !obs;

            // 인접 4방향 중 걸어올 수 있는 칸 수 — 0이면 셀 자체는 멀쩡해도 A*가 도달할 수 없다.
            int openNeighbors = 0;
            foreach (var n in new[] { new Vector3Int(1,0,0), new Vector3Int(-1,0,0), new Vector3Int(0,1,0), new Vector3Int(0,-1,0) })
                if (Walkable(cell + n)) openNeighbors++;

            string reason = ok ? "OK" : (obs ? "OBSTACLE 겹침" : "GROUND 없음");
            string msg = $"[DeskCheck] {ws.deskId,-9} cell={cell}  world=({world.x:F2},{world.y:F2})  ground={g} obstacle={obs} → {reason}  인접walkable={openNeighbors}/4";

            if (!ok) Debug.LogError(msg);
            else if (openNeighbors == 0) Debug.LogError(msg + "  ※ 셀은 멀쩡하나 사방이 막혀 도달 불가");
            else Debug.Log(msg);
        }

        // ---- patrol 지점 ----
        var points = Object.FindObjectsByType<PatrolPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(p => GetPath(p.gameObject).StartsWith(prefix)).ToList();

        foreach (var p in points)
        {
            Vector3Int cell = ground.WorldToCell(new Vector3(p.transform.position.x, p.transform.position.y, 0));
            bool ok = Walkable(cell);
            string msg = $"[DeskCheck] patrol '{p.pointId}' ({GetPath(p.gameObject)}) cell={cell} → {(ok ? "OK" : "NOT WALKABLE")}";
            if (!ok) Debug.LogError(msg); else Debug.Log(msg);
        }

        // ---- 연결성: 가장 큰 walkable 덩어리에 desk/patrol이 모두 들어있는지 ----
        var all = new HashSet<Vector3Int>();
        var b = ground.cellBounds;
        foreach (var c in b.allPositionsWithin) if (Walkable(c)) all.Add(c);
        if (stair != null) foreach (var c in stair.cellBounds.allPositionsWithin) if (Walkable(c)) all.Add(c);
        if (elevator != null) foreach (var c in elevator.cellBounds.allPositionsWithin) if (Walkable(c)) all.Add(c);

        var gridManager = Object.FindAnyObjectByType<GridManager>(FindObjectsInactive.Include);
        Debug.Log($"[DeskCheck] GridManager 발견={(gridManager != null)}, elevatorLinks={(gridManager != null ? gridManager.elevatorLinks.Count : -1)}개, elevator 타일맵={(elevator != null ? elevator.name : "none")}");

        var visited = new HashSet<Vector3Int>();
        var islands = new List<HashSet<Vector3Int>>();
        foreach (var start in all)
        {
            if (visited.Contains(start)) continue;
            var island = new HashSet<Vector3Int>();
            var q = new Queue<Vector3Int>();
            q.Enqueue(start); visited.Add(start); island.Add(start);
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                var nbrs = new List<Vector3Int> {
                    cur + new Vector3Int(1,0,0), cur + new Vector3Int(-1,0,0),
                    cur + new Vector3Int(0,1,0), cur + new Vector3Int(0,-1,0) };
                // 엘리베이터 워프도 A*와 동일하게 이웃으로 취급
                if (elevator != null && elevator.HasTile(cur) && gridManager != null)
                {
                    foreach (var link in gridManager.elevatorLinks)
                    {
                        if (link.cellA == cur) { nbrs.Add(link.cellB); Debug.Log($"[DeskCheck]   워프 확장 {cur} → {link.cellB} (대상 walkable={all.Contains(link.cellB)})"); }
                        if (link.cellB == cur) { nbrs.Add(link.cellA); Debug.Log($"[DeskCheck]   워프 확장 {cur} → {link.cellA} (대상 walkable={all.Contains(link.cellA)})"); }
                    }
                }
                foreach (var n in nbrs)
                {
                    if (visited.Contains(n) || !all.Contains(n)) continue;
                    visited.Add(n); island.Add(n); q.Enqueue(n);
                }
            }
            islands.Add(island);
        }
        islands = islands.OrderByDescending(i => i.Count).ToList();
        Debug.Log($"[DeskCheck] {levelName}: walkable 셀 {all.Count}개, 분리된 덩어리 {islands.Count}개 (최대 {(islands.Count > 0 ? islands[0].Count : 0)}칸)");

        // 덩어리별로 어떤 desk / patrol 지점이 들어있는지 나열 — 어느 구역이 서로 끊겼는지 한눈에 보기 위함.
        for (int i = 0; i < islands.Count; i++)
        {
            var members = new List<string>();
            foreach (var ws in stations)
            {
                if (ws.workPoint == null) continue;
                if (islands[i].Contains(ground.WorldToCell(ws.workPoint.position + ws.workPointOffset))) members.Add(ws.deskId);
            }
            foreach (var pt in points)
            {
                if (islands[i].Contains(ground.WorldToCell(new Vector3(pt.transform.position.x, pt.transform.position.y, 0))))
                    members.Add($"patrol:{pt.pointId}");
            }
            var sample = islands[i].OrderBy(c => c.x).ThenBy(c => c.y).Take(6);
            Debug.Log($"[DeskCheck]   덩어리#{i} {islands[i].Count}칸  포함=[{string.Join(", ", members)}]  셀예시={string.Join(" ", sample.Select(c => $"({c.x},{c.y})"))}");
        }

        if (islands.Count > 0)
        {
            var main = islands[0];
            foreach (var ws in stations)
            {
                if (ws.workPoint == null) continue;
                var cell = ground.WorldToCell(ws.workPoint.position + ws.workPointOffset);
                if (!main.Contains(cell))
                    Debug.LogError($"[DeskCheck] {ws.deskId}: 메인 통로 덩어리와 분리됨 (cell={cell}) — 걸어서 못 감");
            }
            foreach (var p in points)
            {
                var cell = ground.WorldToCell(new Vector3(p.transform.position.x, p.transform.position.y, 0));
                if (!main.Contains(cell))
                    Debug.LogError($"[DeskCheck] patrol '{p.pointId}': 메인 통로 덩어리와 분리됨 (cell={cell}) — 걸어서 못 감");
            }
        }
    }
}
