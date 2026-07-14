using UnityEngine;
using UnityEngine.Tilemaps;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Tilemaps")]
    public Tilemap groundTilemap;    // 바닥 타일맵
    public Tilemap obstacleTilemap;  // 장애물 타일맵
    public Tilemap stairTilemap;     // 계단 타일맵

    [Header("Stairs")]
    public float stairYOffset = 0.3f; // 계단 셀의 Y 오프셋

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // 스테이지 전환(새 사무실 등) 시 참조 타일맵 교체 — groundTilemap 등은 고정 참조라
    // 새로 깔린 타일맵(예: "ground (1)")은 자동으로 인식되지 않는다. null 허용(예: 새 구역에 계단 없음).
    public void SetTilemaps(Tilemap ground, Tilemap obstacle, Tilemap stair)
    {
        groundTilemap   = ground;
        obstacleTilemap = obstacle;
        stairTilemap    = stair;
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
        bool hasGround = groundTilemap.HasTile(cellPos)
                      || (stairTilemap != null && stairTilemap.HasTile(cellPos));

        // 오브젝트가 비활성화면 장애물 무시
        bool hasObstacle = obstacleTilemap != null
            && obstacleTilemap.gameObject.activeInHierarchy
            && obstacleTilemap.HasTile(cellPos);


        return hasGround && !hasObstacle;
    }

    // 해당 셀의 Y 오프셋 (계단이면 stairYOffset, 아니면 0)
    public float GetCellElevation(Vector3Int cellPos)
    {
        if (stairTilemap != null && stairTilemap.HasTile(cellPos))
            return stairYOffset;
        return 0f;
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