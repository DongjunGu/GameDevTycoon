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

    // NextLevelBtn — CameraZoomController 활성화 + 지정된 패널 전환.
    // 시간은 멈추지 않음 — CharacterMover.MoveAlongPath가 GameTimeManager.IsRunning일 때만 위치를
    // 갱신하므로, 시간을 멈추면 patrol이 "일어나는 모션"까지만 진입하고 실제로는 안 움직임(눈으로 확인 불가).
    public void OnClickNextLevel()
    {
        if (cameraZoomController != null) cameraZoomController.enabled = true;

        if (panelsToDeactivate != null)
            foreach (var p in panelsToDeactivate) if (p != null) p.SetActive(false);

        // 이름에 "(Clone)"이 붙은 오브젝트(잔여 인스턴스화 산물)도 함께 정리.
        // 단, 스폰된 직원 캐릭터(OfficeCharacter)도 Instantiate 산물이라 이름에 "(Clone)"이 붙으므로 제외해야 함.
        foreach (var go in FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!go.name.Contains("(Clone)")) continue;
            if (go.GetComponent<OfficeCharacter>() != null) continue;
            go.SetActive(false);
        }

        if (panelsToActivate != null)
            foreach (var p in panelsToActivate) if (p != null) p.SetActive(true);

        // 새 사무실 타일맵으로 GridManager 참조 교체 — 안 하면 IsWalkable/WorldToCell이 계속 옛 타일맵을 봐서
        // 새 구역은 전부 walkable=false로 나와 경로탐색이 실패한다.
        if (GridManager.Instance != null && newGroundTilemap != null)
            GridManager.Instance.SetTilemaps(newGroundTilemap, newObstacleTilemap, newStairTilemap);

        // [테스트] 새 사무실 자리로 순간이동(걷지 않고 위치만 교체) — 타일/데스크 재배치 테스트용.
        // desk_01~04 → desk_05~08. 이동된 인원 중 랜덤 1명을 p3/p4 중 한 곳으로 patrol.
        if (OfficeManager.Instance != null)
        {
            // p3/p4(Level4 자식)는 씬 시작 시 캐싱된 patrol point 목록엔 없음(그때는 Level4가 비활성) —
            // Level4를 막 활성화한 지금 다시 스캔해야 ForceCharacterToPatrolPoint가 찾을 수 있음.
            OfficeManager.Instance.RefreshPatrolPoints();

            var moved = OfficeManager.Instance.WarpDesksInstant(
                ("desk_01", "desk_05"),
                ("desk_02", "desk_06"),
                ("desk_03", "desk_07"),
                ("desk_04", "desk_08"));

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
