using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OfficeManager : MonoBehaviour
{
    public static OfficeManager Instance { get; private set; }

    [Header("Spawn")]
    public GameObject fallbackPrefab;   // portraitId 매핑 없을 때 기본 프리팹
    public Transform  spawnPoint;       // 스폰 위치 (문 앞 등)

    [Header("Patrol Settings")]
    [SerializeField] private float patrolCheckInterval = 20f;  // 몇 초마다 patrol 발동 체크
    [SerializeField] private int   patrolCountPerCycle = 1;    // 한 번에 patrol 보낼 최대 인원
    [SerializeField] private float patrolStayDuration = 5f;    // 목적지 도착 후 대기 시간

    [Header("Block Popup Sorting")]
    [SerializeField] private string blockPopupSortingLayer = "Default";
    [SerializeField] private int    blockPopupBgOrder      = 9;
    [SerializeField] private int    blockPopupCellOrder    = 10;
    public string BlockPopupSortingLayer => blockPopupSortingLayer;
    public int    BlockPopupBgOrder      => blockPopupBgOrder;
    public int    BlockPopupCellOrder    => blockPopupCellOrder;

    // employeeId → OfficeCharacter
    private Dictionary<string, OfficeCharacter> _characters = new();

    [SerializeField] private float _characterSpeedMultiplier = 1f;
    public float CharacterSpeedMultiplier => _characterSpeedMultiplier;
    public void SetCharacterSpeedMultiplier(float value) => _characterSpeedMultiplier = value;

    private PatrolPoint[] _patrolPoints;
    private DialogPatrolPoint[] _dialogPatrolPoints;
    private Coroutine _patrolScheduler;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private GameObject GetPrefab(string portraitId)
    {
        if (!string.IsNullOrEmpty(portraitId))
        {
            var prefab = Resources.Load<GameObject>($"Characters/{portraitId}");
            if (prefab != null) return prefab;
        }
        return fallbackPrefab;
    }

    // EmployeeManager.HireEmployee() 완료 후 호출
    public void OnEmployeeHired(EmployeeData employee)
    {
        var desk = DeskManager.Instance.GetEmptyDesk();
        if (desk == null)
        {
            Debug.LogWarning("배정할 Desk 없음");
            return;
        }

        DeskManager.Instance.AssignDesk(desk.deskId, employee.id);
        employee.assignedDeskId = desk.deskId;
        EmployeeManager.Instance.UpdateEmployee(employee);

        var obj  = Instantiate(GetPrefab(employee.portraitId), spawnPoint.position, Quaternion.identity);
        var oc   = obj.GetComponent<OfficeCharacter>();
        oc.Init(employee.id, desk);

        _characters[employee.id] = oc;

        oc.GoToDesk();
        EnsurePatrolScheduler();

        EmployeeStatusBarUI.Instance?.AddSlot(employee.id);

        Debug.Log($"{employee.employeeName} 스폰 → {desk.deskId}로 이동");
    }

    // 머리 위 수치 팝업 표시
    public void ShowStatPopup(string employeeId, string text, Color color)
    {
        if (_characters.TryGetValue(employeeId, out var oc))
            oc.ShowStatPopup(text, color);
    }

    public void ShowBlockPopup(string employeeId, int[][] cells, Color color)
    {
        if (_characters.TryGetValue(employeeId, out var oc))
            oc.ShowBlockPopup(cells, color);
    }

    // 상시 개발틱 팝업: statKey는 StatPopupSprites 키 (planning/develop/art/bug)
    public void ShowStatTickPopup(string employeeId, string statKey, int amount, Color color, bool isJackpot)
    {
        if (!_characters.TryGetValue(employeeId, out var oc)) return;
        var statSprite = StatPopupSprites.Instance != null ? StatPopupSprites.Instance.GetStatIcon(statKey) : null;
        oc.ShowStatTickPopup(statSprite, amount, color, isJackpot);
    }

    // 해고 시 — 데이터 즉시 제거, 캐릭터는 SpawnPoint까지 걸어간 뒤 소멸
    public void OnEmployeeFired(EmployeeData employee)
    {
        DeskManager.Instance.UnassignDesk(employee.assignedDeskId);

        EmployeeStatusBarUI.Instance?.RemoveSlot(employee.id);

        if (!_characters.TryGetValue(employee.id, out var oc)) return;
        _characters.Remove(employee.id);

        oc.PrepareToLeave();
        StartCoroutine(WalkOutAndDestroy(oc));
    }

    IEnumerator RefreshAnimationsNextFrame()
    {
        yield return null;
        var stage = DevelopmentManager.Instance != null ? DevelopmentManager.Instance.CurrentStage : ProjectStage.None;
        bool isDeveloping = stage == ProjectStage.Developing || stage == ProjectStage.BugFixing;
        if (isDeveloping) SetAllWorking();
        else RefreshAllDeskAnimations();
    }

    IEnumerator WalkOutAndDestroy(OfficeCharacter oc)
    {
        var controller = oc.GetComponent<CharacterController>();
        var mover      = oc.GetComponent<CharacterMover>();

        if (controller != null && mover != null)
        {
            Vector3Int exitCell = GridManager.Instance.WorldToCell(spawnPoint.position);
            controller.MoveTo(exitCell, spawnPoint.position);

            yield return null; // 이동 시작 대기
            yield return new WaitUntil(() => !mover.IsMoving);
        }

        Destroy(oc.gameObject);
    }

    // 씬 로드 시 기존 직원 복원
    public void RestoreEmployees()
    {
        // CEO 먼저 점유 → 일반 직원이 ceoDeskId(예: desk_04) 못 받게 보호
        SpawnCEO();

        string ceoDeskId = CEOManager.Instance != null ? CEOManager.Instance.ceoDeskId : "";

        foreach (var employee in EmployeeManager.Instance.ownedEmployees)
        {
            WorkStation desk;
            if (string.IsNullOrEmpty(employee.assignedDeskId))
                desk = DeskManager.Instance.GetEmptyDesk();
            else
                desk = DeskManager.Instance.GetDeskById(employee.assignedDeskId);

            // CEO 가 점유한 desk 와 충돌하면 빈 자리로 재할당 (마이그레이션)
            if (desk != null && !string.IsNullOrEmpty(ceoDeskId) && desk.deskId == ceoDeskId)
                desk = DeskManager.Instance.GetEmptyDesk();

            if (desk == null) continue;

            DeskManager.Instance.AssignDesk(desk.deskId, employee.id);
            employee.assignedDeskId = desk.deskId;

            var obj = Instantiate(GetPrefab(employee.portraitId), desk.GetWorkWorldPos(), Quaternion.identity);
            var oc  = obj.GetComponent<OfficeCharacter>();
            oc.Init(employee.id, desk);

            _characters[employee.id] = oc;
        }

        EmployeeStatusBarUI.Instance?.Rebuild();

        EnsurePatrolScheduler();
        StartCoroutine(RefreshAnimationsNextFrame());
    }

    // CEO 스폰 — CEOManager.ceoDeskId 고정 배치 + ceoPrefab 직접 인스펙터 참조.
    // EmployeeStatusBarUI 는 ownedEmployees 만 보므로 CEO 슬롯은 자동 제외.
    public void SpawnCEO()
    {
        var mgr = CEOManager.Instance;
        var ceo = EmployeeManager.Instance?.CEO;
        if (mgr == null || ceo == null) return;

        var desk = DeskManager.Instance.GetDeskById(mgr.ceoDeskId);
        if (desk == null)
        {
            Debug.LogWarning($"[CEO] '{mgr.ceoDeskId}' 데스크 없음 - CEO 스폰 스킵");
            return;
        }

        DeskManager.Instance.AssignDesk(desk.deskId, ceo.id);
        ceo.assignedDeskId = desk.deskId;

        var prefab = mgr.ceoPrefab != null ? mgr.ceoPrefab : fallbackPrefab;
        var obj    = Instantiate(prefab, desk.GetWorkWorldPos(), Quaternion.identity);
        var oc     = obj.GetComponent<OfficeCharacter>();
        oc.Init(ceo.id, desk);

        _characters[ceo.id] = oc;

        Debug.Log($"[CEO] {ceo.employeeName} {desk.deskId} 스폰");
    }

    // 특정 직원의 patrol 여부 확인 (DevelopmentManager 틱 체크용)
    public bool IsPatrolling(string employeeId)
    {
        return _characters.TryGetValue(employeeId, out var oc) && oc.IsPatrolling;
    }

    public bool IsWorking(string employeeId)
    {
        return _characters.TryGetValue(employeeId, out var oc) && oc.State == CharacterState.Working;
    }

    public CharacterState GetState(string employeeId)
    {
        return _characters.TryGetValue(employeeId, out var oc) ? oc.State : CharacterState.Working;
    }

    public void RefreshAllDeskAnimations()
    {
        foreach (var oc in _characters.Values)
            oc.ApplyDeskAnimation();
    }

    public void SetAllWorking()
    {
        foreach (var oc in _characters.Values)
            oc.SetDeskWorking();
    }

    // 개발 시작 시 호출 — patrol 포인트 갱신
    public void StartDevelopmentPatrol()
    {
        _patrolPoints = FindObjectsByType<PatrolPoint>(FindObjectsSortMode.None);
        _dialogPatrolPoints = FindObjectsByType<DialogPatrolPoint>(FindObjectsSortMode.None);
        EnsurePatrolScheduler();
    }

    // 스케줄러가 꺼져있을 때만 시작
    void EnsurePatrolScheduler()
    {
        if (_patrolScheduler != null) return;
        _patrolPoints ??= FindObjectsByType<PatrolPoint>(FindObjectsSortMode.None);
        _dialogPatrolPoints ??= FindObjectsByType<DialogPatrolPoint>(FindObjectsSortMode.None);
        _patrolScheduler = StartCoroutine(PatrolScheduler());
    }

    // 개발 완료/중단 시 호출 — 모든 캐릭터 즉시 데스크로 복귀
    public void StopDevelopmentPatrol()
    {
        if (_patrolScheduler != null)
        {
            StopCoroutine(_patrolScheduler);
            _patrolScheduler = null;
        }

        foreach (var oc in _characters.Values)
            oc.CancelPatrol();
    }

    // 특정 직원을 pointId 위치로 즉시 강제 이동 (이벤트 트리거용)
    public void ForceCharacterToPatrolPoint(string employeeId, string pointId, float stayDuration = 5f)
    {
        if (!_characters.TryGetValue(employeeId, out var oc)) return;
        var point = System.Array.Find(_patrolPoints, p => p.pointId == pointId);
        if (point == null)
        {
            Debug.LogWarning($"[ForcePatrol] pointId '{pointId}' 를 찾을 수 없음");
            return;
        }
        oc.ForcePatrolTo(point.transform, stayDuration);
    }

    // 랜덤 n명 patrol 발동 (스케줄러 자동 호출 or 외부에서 직접 호출)
    public void TriggerPatrolRandom(int count)
    {
        if (_patrolPoints == null || _patrolPoints.Length == 0) return;

        // patrol 가능한 캐릭터 목록 섞기
        var candidates = new List<OfficeCharacter>(_characters.Values);
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        int sent = 0;
        foreach (var oc in candidates)
        {
            if (sent >= count) break;
            if (oc.IsPatrolling) continue;
            var point = _patrolPoints[Random.Range(0, _patrolPoints.Length)];
            var pointCell = GridManager.Instance.WorldToCell(new Vector3(point.transform.position.x, point.transform.position.y, 0));
            if (!GridManager.Instance.IsWalkable(pointCell))
            {
                Debug.LogWarning($"[Patrol] point {point.name} 셀={pointCell} walkable 아님, 스킵");
                continue;
            }
            oc.StartPatrol(point.transform, patrolStayDuration);
            sent++;
        }
    }

    // 특정 직원 patrol 발동
    public void TriggerPatrolForEmployee(string employeeId)
    {
        if (_patrolPoints == null || _patrolPoints.Length == 0) return;
        if (!_characters.TryGetValue(employeeId, out var oc)) return;
        if (oc.IsPatrolling) return;

        var point = _patrolPoints[Random.Range(0, _patrolPoints.Length)];
        oc.StartPatrol(point.transform, patrolStayDuration);
    }

    IEnumerator PatrolScheduler()
    {
        while (true)
        {
            // patrol 체크 간격도 게임 시간 기준으로 대기
            float elapsed = 0f;
            while (elapsed < patrolCheckInterval)
            {
                if (GameTimeManager.Instance != null && GameTimeManager.Instance.IsRunning)
                    elapsed += Time.deltaTime;
                yield return null;
            }

            float devProgress = DevelopmentManager.Instance.developmentDuration > 0
                ? DevelopmentManager.Instance.GetElapsed() / DevelopmentManager.Instance.developmentDuration
                : 0f;
            if (_patrolPoints != null && _patrolPoints.Length > 0
                && DevelopmentManager.Instance.CurrentStage != ProjectStage.BugFixing
                && devProgress < 0.7f)
                TriggerPatrolRandom(patrolCountPerCycle);
            CheckDialogPatrols();
        }
    }

    // 보유 직원 중 랜덤 1명을 DialogPatrolPoint로 보냄 (테스트용)
    public void TriggerDialogPatrolRandom()
    {
        if (_dialogPatrolPoints == null || _dialogPatrolPoints.Length == 0)
            _dialogPatrolPoints = FindObjectsByType<DialogPatrolPoint>(FindObjectsSortMode.None);

        if (_dialogPatrolPoints == null || _dialogPatrolPoints.Length == 0)
        {
            Debug.LogWarning("[DialogPatrol] 씬에 DialogPatrolPoint가 없습니다.");
            return;
        }

        var owned = EmployeeManager.Instance.ownedEmployees;
        if (owned.Count == 0)
        {
            Debug.LogWarning("[DialogPatrol] 보유 직원이 없습니다.");
            return;
        }

        // 패트롤 중이 아닌 직원 중 랜덤 선택
        var candidates = new System.Collections.Generic.List<string>();
        foreach (var e in owned)
        {
            if (_characters.TryGetValue(e.id, out var oc) && !oc.IsPatrolling)
                candidates.Add(e.id);
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning("[DialogPatrol] 패트롤 가능한 직원이 없습니다.");
            return;
        }

        string empId = candidates[Random.Range(0, candidates.Count)];
        var dpp = _dialogPatrolPoints[Random.Range(0, _dialogPatrolPoints.Length)];

        _characters[empId].StartPatrolWithDialog(dpp.transform, dpp.dialogGroupId, dpp.triggerOnce);
        Debug.Log($"[DialogPatrol] {empId} → {dpp.dialogGroupId}");
    }

    void CheckDialogPatrols()
    {
        if (_dialogPatrolPoints == null) return;
        foreach (var dpp in _dialogPatrolPoints)
        {
            if (!_characters.TryGetValue(dpp.employeeId, out var oc)) continue;
            if (oc.IsPatrolling) continue;
            oc.StartPatrolWithDialog(dpp.transform, dpp.dialogGroupId, dpp.triggerOnce);
        }
    }
}
