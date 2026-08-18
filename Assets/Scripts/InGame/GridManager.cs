using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    // 사무실 확장 단계(1/4 — Level1/Level4 테스트 시스템과 동일 기준). 다이얼로그 배경(Resources/Dialog/BG_Office_Lv{N})
    // 등 "지금 사무실이 몇 레벨인지"가 필요한 곳에서 이 값을 단일 소스로 참조한다.
    public int CurrentOfficeLevel { get; private set; } = 1;

    [Header("Tilemaps")]
    public Tilemap groundTilemap;    // 바닥 타일맵
    public Tilemap obstacleTilemap;  // 장애물 타일맵
    public Tilemap stairTilemap;     // 계단 타일맵
    public Tilemap elevatorTilemap;  // 엘리베이터 시각 요소(샤프트 등)가 칠해진 타일맵. 링크 양쪽 셀에 타일이 있어야 워프가 작동.

    [Header("Stairs")]
    public float stairYOffset = 0.3f; // 계단 셀의 Y 오프셋

    [System.Serializable]
    public struct ElevatorLink
    {
        public Vector3Int cellA; // 아래층 진입 셀
        public Vector3Int cellB; // 위층 도착 셀
    }

    [Header("Elevator Links")]
    [Tooltip("비인접 셀을 잇는 워프 연결. 양방향. 양쪽 셀 모두 elevatorTilemap에 타일이 있어야 실제로 발동함.")]
    public List<ElevatorLink> elevatorLinks = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // elevatorLinks에 등록된 좌표가 실제로 elevatorTilemap에 칠해져 있는지 진입 시점에 검증.
        foreach (var link in elevatorLinks)
        {
            bool aOk = elevatorTilemap != null && elevatorTilemap.HasTile(link.cellA);
            bool bOk = elevatorTilemap != null && elevatorTilemap.HasTile(link.cellB);
            if (!aOk || !bOk)
                Debug.LogWarning($"[GridManager] ElevatorLink {link.cellA} <-> {link.cellB} 비활성 상태: elevatorTilemap에 타일 없음 (cellA={aOk}, cellB={bOk})");
        }
    }

    // 스테이지 전환(새 사무실 등) 시 참조 타일맵 교체 — groundTilemap 등은 고정 참조라
    // 새로 깔린 타일맵(예: "ground (1)")은 자동으로 인식되지 않는다. null 허용(예: 새 구역에 계단/엘리베이터 없음).
    public void SetTilemaps(Tilemap ground, Tilemap obstacle, Tilemap stair, Tilemap elevator = null)
    {
        groundTilemap   = ground;
        obstacleTilemap = obstacle;
        stairTilemap    = stair;
        elevatorTilemap = elevator;
    }

    // 사무실 레벨 전환 시(현재는 TestMenuButtons.OnClickNextLevel 뿐) 호출 — CurrentOfficeLevel 갱신.
    public void SetOfficeLevel(int level)
    {
        CurrentOfficeLevel = level;
    }

    // 사무실 레벨별 다이얼로그 배경 — 원래 계획은 Resources/Dialog/BG_Office_Lv{CurrentOfficeLevel}이지만
    // 아직 Lv1/Lv4용 이미지 2장만 있고 레벨별 확장이 미확정이라(레벨2/3 리소스 없음), 임시로 이 2장을
    // 호출될 때마다 번갈아 보여준다. Lv2/Lv3 이미지가 준비되면 CurrentOfficeLevel 기반으로 되돌릴 것.
    static bool _altBgToggle;
    static readonly string[] _tempBgNames = { "BG_Office_Lv1", "BG_Office_Lv4" };
    public static Sprite LoadDialogBackgroundSprite()
    {
        _altBgToggle = !_altBgToggle;
        return Resources.Load<Sprite>($"Dialog/{_tempBgNames[_altBgToggle ? 0 : 1]}");
    }

    // cell이 엘리베이터 링크의 한쪽 끝이면 반대쪽 셀을 반환 (없으면 null)
    // elevatorTilemap에 시각 요소가 여러 칸 칠해져 있어도, 실제 워프는 elevatorLinks에 등록된
    // 두 셀 사이에서만 일어난다 — 단, 그 두 셀 모두 elevatorTilemap에 타일이 있어야 발동.
    public Vector3Int? GetElevatorLink(Vector3Int cell)
    {
        if (elevatorTilemap == null || !elevatorTilemap.HasTile(cell)) return null;

        foreach (var link in elevatorLinks)
        {
            if (link.cellA == cell && elevatorTilemap.HasTile(link.cellB)) return link.cellB;
            if (link.cellB == cell && elevatorTilemap.HasTile(link.cellA)) return link.cellA;
        }
        return null;
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
                      || (stairTilemap != null && stairTilemap.HasTile(cellPos))
                      || (elevatorTilemap != null && elevatorTilemap.HasTile(cellPos));

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