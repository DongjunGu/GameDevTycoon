using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Tilemaps (인덱스 = 층)")]
    public Tilemap[] groundTilemaps;  // [0]=0층, [1]=1층...
    public Tilemap obstacleTilemap;

    [Header("층당 Y 오프셋")]
    public float heightPerFloor = 0.5f;

    private Dictionary<Vector3Int, Vector3Int> _stairLinks = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── 계단 등록 (양방향 자동) ──────────────────────
    public void RegisterStair(Vector3Int from, Vector3Int to)
    {
        _stairLinks[from] = to;
        _stairLinks[to] = from;
    }

    // ── 좌표 변환 ────────────────────────────────────
    public Vector3Int WorldToCell(Vector3 worldPos, int floor = 0)
    {
        var cell = groundTilemaps[floor].WorldToCell(worldPos);
        return new Vector3Int(cell.x, cell.y, floor);
    }

    public Vector3 CellToWorld(Vector3Int cellPos)
    {
        int floor = Mathf.Clamp(cellPos.z, 0, groundTilemaps.Length - 1);
        var cell2D = new Vector3Int(cellPos.x, cellPos.y, 0);
        Vector3 worldPos = groundTilemaps[floor].GetCellCenterWorld(cell2D);
        worldPos.y += cellPos.z * heightPerFloor;
        return worldPos;
    }

    // ── 이동 가능 여부 ───────────────────────────────
    public bool IsWalkable(Vector3Int cellPos)
    {
        int floor = cellPos.z;
        if (floor < 0 || floor >= groundTilemaps.Length) return false;

        var cell2D = new Vector3Int(cellPos.x, cellPos.y, 0);

        bool hasGround = groundTilemaps[floor].HasTile(cell2D);
        bool hasObstacle = obstacleTilemap != null
            && obstacleTilemap.gameObject.activeInHierarchy
            && obstacleTilemap.HasTile(cell2D);

        return hasGround && !hasObstacle;
    }

    // ── 이웃 셀 (4방향 + 계단) ──────────────────────
    public Vector3Int[] GetNeighbors(Vector3Int cell)
    {
        var neighbors = new List<Vector3Int>
        {
            cell + new Vector3Int( 1,  0, 0),
            cell + new Vector3Int(-1,  0, 0),
            cell + new Vector3Int( 0,  1, 0),
            cell + new Vector3Int( 0, -1, 0),
        };

        if (_stairLinks.TryGetValue(cell, out var stairTarget))
            neighbors.Add(stairTarget);

        return neighbors.ToArray();
    }
}