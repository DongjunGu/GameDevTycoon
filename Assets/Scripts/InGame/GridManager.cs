using UnityEngine;
using UnityEngine.Tilemaps;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Tilemaps")]
    public Tilemap groundTilemap;    // 바닥 타일맵
    public Tilemap obstacleTilemap;  // 장애물 타일맵

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // 월드 좌표 → 셀 좌표
    public Vector3Int WorldToCell(Vector3 worldPos)
    {
        return groundTilemap.WorldToCell(worldPos);
    }

    // 셀 좌표 → 월드 좌표 (타일 중심)
    public Vector3 CellToWorld(Vector3Int cellPos)
    {
        return groundTilemap.GetCellCenterWorld(cellPos);
    }

    // 이동 가능 여부
    public bool IsWalkable(Vector3Int cellPos)
    {
        bool hasGround = groundTilemap.HasTile(cellPos);

        // 오브젝트가 비활성화면 장애물 무시
        bool hasObstacle = obstacleTilemap != null
            && obstacleTilemap.gameObject.activeInHierarchy
            && obstacleTilemap.HasTile(cellPos);


        return hasGround && !hasObstacle;
    }

    // 4방향 이웃 셀 (isometric 4방향)
    public Vector3Int[] GetNeighbors(Vector3Int cell)
    {
        return new Vector3Int[]
        {
            cell + new Vector3Int(1, 0, 0),
            cell + new Vector3Int(-1, 0, 0),
            cell + new Vector3Int(0, 1, 0),
            cell + new Vector3Int(0, -1, 0),
        };
    }
}