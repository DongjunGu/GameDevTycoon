using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// 관리 서브메뉴의 테스트 전용 버튼들(TestResetBtn / NextLevelBtn) OnClick 진입점.
public class TestMenuButtons : MonoBehaviour
{
    // TestResetBtn — 온보딩 플래그 초기화 후 LoadingScene부터 다시 시작 → 컷씬 + 게임씬 튜토리얼 재노출.
    public void OnClickTestReset()
    {
        // GameTimeManager는 DontDestroyOnLoad라 씬을 넘어가도 안 죽고 계속 틱을 돈다 — 컷씬/새 런 초기화가
        // 끝날 때까지(NewRunInitializer.StartNewRun이 리셋하기 전까지) 켜져있으면 안 되므로 여기서 먼저 정지.
        GameTimeManager.Instance?.StopTime();
        OnboardingState.ResetAll();
        SceneManager.LoadScene("LoadingScene");
    }

    [Header("NextLevelBtn")]
    public CameraZoomController cameraZoomController; // Main Camera의 CameraZoomController
    public GameObject[] panelsToDeactivate;
    public GameObject[] panelsToActivate;

    [Header("NextLevelBtn - 새 사무실 타일맵")]
    [Tooltip("GridManager는 타일맵을 고정 참조라 새로 깔린 타일맵을 자동으로 인식 못 함 — 여기 연결해서 전환 시 교체")]
    public Tilemap newGroundTilemap;
    public Tilemap newObstacleTilemap;
    [Tooltip("새 구역에 계단 없으면 비워둠")]
    public Tilemap newStairTilemap;
    [Tooltip("새 구역에 엘리베이터 없으면 비워둠")]
    public Tilemap newElevatorTilemap;
    [Tooltip("새 사무실(Level4)의 채용 스폰 위치 — OfficeManager.spawnPoint는 고정 참조라 여기서 전환 시 교체해야 새로 채용한 직원이 여기서 걸어나온다")]
    public Transform newSpawnPoint;
    [Tooltip("HUDUI.feeText(AnnualFeePanel-feeText)를 이 값으로 고정 — 실제 연세 계산(HUDUI.RefreshYearFee)이 다시 갱신하기 전까지 유지")]
    public string testFixedFeeText = "200,000 G";

    // NextLevelBtn — CameraZoomController 활성화 + 지정된 패널 전환.
    // 시간은 멈추지 않음 — CharacterMover.MoveAlongPath가 GameTimeManager.IsRunning일 때만 위치를
    // 갱신하므로, 시간을 멈추면 patrol이 "일어나는 모션"까지만 진입하고 실제로는 안 움직임(눈으로 확인 불가).
    public void OnClickNextLevel()
    {
        if (cameraZoomController != null) cameraZoomController.enabled = true;

        if (panelsToDeactivate != null)
            foreach (var p in panelsToDeactivate) if (p != null) p.SetActive(false);

        // 이름에 "(Clone)"이 붙은 오브젝트(잔여 인스턴스화 산물)도 함께 정리.
        // 단, 스폰된 직원 캐릭터(OfficeCharacter)와 EmployeeStatusBarUI 하단 슬롯(EmployeeSatisfactionSlider),
        // 퀘스트 목록 아이템(QuestItemSimple)도 Instantiate 산물이라 이름에 "(Clone)"이 붙으므로 제외해야
        // 함 — 안 그러면 직원 캐릭터는 남아있는데 하단 상태바 슬롯만 꺼져서 "직원이 없어진" 것처럼 보이거나,
        // 퀘스트 목록이 워프할 때마다 비어보이는 버그가 생긴다.
        foreach (var go in FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!go.name.Contains("(Clone)")) continue;
            if (go.GetComponent<OfficeCharacter>() != null) continue;
            if (go.GetComponent<EmployeeSatisfactionSlider>() != null) continue;
            if (go.name.StartsWith("QuestItemSimple")) continue;
            go.SetActive(false);
        }

        if (panelsToActivate != null)
            foreach (var p in panelsToActivate) if (p != null) p.SetActive(true);

        // 새 사무실 타일맵으로 GridManager 참조 교체 — 안 하면 IsWalkable/WorldToCell이 계속 옛 타일맵을 봐서
        // 새 구역은 전부 walkable=false로 나와 경로탐색이 실패한다.
        if (GridManager.Instance != null && newGroundTilemap != null)
        {
            GridManager.Instance.SetTilemaps(newGroundTilemap, newObstacleTilemap, newStairTilemap, newElevatorTilemap);
            GridManager.Instance.SetOfficeLevel(4); // 이 테스트 버튼은 Level1 → Level4 전환 전용
        }

        // 채용 스폰 위치를 새 사무실 쪽으로 교체 — 안 하면 OfficeManager.OnEmployeeHired가 계속 옛
        // spawnPoint(Level1 문 앞)에서 캐릭터를 Instantiate한다.
        if (OfficeManager.Instance != null && newSpawnPoint != null)
            OfficeManager.Instance.spawnPoint = newSpawnPoint;

        // [테스트] StageManager 단계를 올려 직원 채용 상한(MaxEmployeeCount)에 안 막히게 — 현재 씬의
        // maxEmployeePerStage는 4단계(값 [2,4,6,7])까지만 정의돼있고 그 밖은 999(무제한) 폴백이라, 새로
        // 생긴 desk_05~11까지 다 채워볼 수 있게 배열 범위 밖인 5로 강제.
        StageManager.Instance?.SetStage(5);

        // [테스트] AnnualFeePanel-feeText 고정 표시 — HUDUI.RefreshYearFee가 실제 연세(Year 파생값)로
        // 다시 덮어쓸 때까지는(고용/해고/급여협상 등 RefreshAll 트리거 전까지) 이 값으로 유지된다.
        if (HUDUI.Instance != null && HUDUI.Instance.feeText != null)
            HUDUI.Instance.feeText.text = testFixedFeeText;

        // [테스트] 새 사무실 자리로 순간이동(걷지 않고 위치만 교체) — 타일/데스크 재배치 테스트용.
        // desk_01/02 → desk_05/06(일반 직원), desk_03(비서 고정석) → desk_sec, desk_04(CEO 고정석,
        // CEOManager.ceoDeskId) → desk_ceo — 새 사무실의 전용 비서/CEO석으로 정확히 이어붙인다.
        // 이동된 인원 중 랜덤 1명을 p3/p4 중 한 곳으로 patrol.
        if (OfficeManager.Instance != null)
        {
            // p3/p4(Level4 자식)는 씬 시작 시 캐싱된 patrol point 목록엔 없음(그때는 Level4가 비활성) —
            // Level4를 막 활성화한 지금 다시 스캔해야 ForceCharacterToPatrolPoint가 찾을 수 있음.
            OfficeManager.Instance.RefreshPatrolPoints();

            var moved = OfficeManager.Instance.WarpDesksInstant(
                ("desk_01", "desk_05"),
                ("desk_02", "desk_06"),
                ("desk_03", "desk_sec"),
                ("desk_04", "desk_ceo"));

            // 워프로 desk_01~04는 이제 비활성화된 Level1 쪽이라 전부 비었지만 여전히 DeskManager 풀에
            // 남아있음 — 제거 안 하면 GetEmptyDesk()가 순서상 이 빈 자리들을 먼저 집어서, 신규 채용이
            // desk_05가 아니라 안 보이는 desk_01부터 배정돼버린다.
            DeskManager.Instance?.RemoveDesks("desk_01", "desk_02", "desk_03", "desk_04");

            if (moved.Count > 0)
            {
                string randomEmployeeId = moved[Random.Range(0, moved.Count)];
                string patrolPointId = Random.value < 0.5f ? "p3" : "p4";
                OfficeManager.Instance.ForceCharacterToPatrolPoint(randomEmployeeId, patrolPointId);
            }
        }
    }

    // [테스트 전용] 아무 버튼에나 연결해서 단독으로 호출 가능 — 보유 직원 중 랜덤 1명을 p3/p4 중
    // 랜덤 한 곳으로 즉시 patrol. RefreshPatrolPoints()를 먼저 호출해 Level4가 방금 활성화된
    // 상황(p3/p4가 씬 시작 캐시에 없는 경우)에도 정상 동작하게 함.
    public void OnClickTestPatrolP3P4()
    {
        if (OfficeManager.Instance == null || EmployeeManager.Instance == null) return;

        var owned = EmployeeManager.Instance.ownedEmployees;
        if (owned == null || owned.Count == 0)
        {
            Debug.LogWarning("[Test] 보유 직원이 없습니다.");
            return;
        }

        OfficeManager.Instance.RefreshPatrolPoints();

        var emp = owned[Random.Range(0, owned.Count)];
        string patrolPointId = Random.value < 0.5f ? "p3" : "p4";
        OfficeManager.Instance.ForceCharacterToPatrolPoint(emp.id, patrolPointId);
        Debug.Log($"[Test] {emp.employeeName} → {patrolPointId} patrol");
    }
}
