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

    [Header("Secretary (Visual NPC)")]
    [Tooltip("비서 캐릭터 prefab. OfficeCharacter 컴포넌트 부착된 portrait_secretary 등을 드래그.")]
    public GameObject secretaryPrefab;
    [Tooltip("비서가 항상 앉을 데스크 ID. 일반 직원이 못 앉도록 보호됨.")]
    public string secretaryDeskId = "desk_03";
    [Tooltip("비서 인스턴스의 합성 ID — _characters dict 키로만 사용. EmployeeData 없음.")]
    public string secretaryId = "secretary_001";

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
        oc.ShowStatTickPopup(statSprite, statKey, amount, color, isJackpot);
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

    // ── 파견(Dispatch) ───────────────────────────────────────
    // 파견 출발 — 데이터/슬롯 유지. 데스크는 비우지 않고 그대로 예약(보존)해 복귀 시 원래 자리로 돌아오게 한다.
    // 캐릭터만 SpawnPoint 로 퇴장 후 소멸.
    public void SendOnDispatch(string employeeId)
    {
        // assignedDeskId / DeskManager 배정은 그대로 둠 (다른 직원이 못 쓰게 자리 예약 유지)
        if (!_characters.TryGetValue(employeeId, out var oc)) return;
        _characters.Remove(employeeId);
        oc.PrepareToLeave();
        StartCoroutine(WalkOutAndDestroy(oc));
    }

    // 파견 복귀 — SpawnPoint 에서 스폰해 master_desk 로 이동, 도착하면 onArrivedAtMaster 호출(보고 모달)
    public void ReturnFromDispatch(string employeeId, System.Action onArrivedAtMaster)
    {
        var emp = EmployeeManager.Instance != null ? EmployeeManager.Instance.GetEmployee(employeeId) : null;
        if (emp == null || spawnPoint == null) { onArrivedAtMaster?.Invoke(); return; }

        var obj = Instantiate(GetPrefab(emp.portraitId), spawnPoint.position, Quaternion.identity);
        var oc  = obj.GetComponent<OfficeCharacter>();
        oc.Init(employeeId, null);
        _characters[employeeId] = oc;

        StartCoroutine(WalkToPointAndReport(oc, "master_desk", onArrivedAtMaster));
    }

    IEnumerator WalkToPointAndReport(OfficeCharacter oc, string pointId, System.Action onArrived)
    {
        _patrolPoints ??= FindObjectsByType<PatrolPoint>(FindObjectsSortMode.None);
        var point = _patrolPoints != null ? System.Array.Find(_patrolPoints, p => p.pointId == pointId) : null;

        var controller = oc.GetComponent<CharacterController>();
        var mover      = oc.GetComponent<CharacterMover>();

        if (point != null && controller != null && mover != null)
        {
            Vector3 pos = new Vector3(point.transform.position.x, point.transform.position.y, 0);
            Vector3Int cell = GridManager.Instance.WorldToCell(pos);
            controller.MoveTo(cell, pos);

            yield return null; // 이동 시작 대기
            yield return new WaitUntil(() => !mover.IsMoving);
        }

        onArrived?.Invoke();
    }

    // 보고 종료 후 — 원래 자리로 복귀(예약돼 있던 assignedDeskId). 없거나 못 찾으면 빈 자리.
    public void AssignDeskAndReturn(string employeeId)
    {
        if (!_characters.TryGetValue(employeeId, out var oc)) return;
        var emp = EmployeeManager.Instance != null ? EmployeeManager.Instance.GetEmployee(employeeId) : null;
        if (emp == null) return;

        WorkStation desk = null;
        if (!string.IsNullOrEmpty(emp.assignedDeskId))
            desk = DeskManager.Instance.GetDeskById(emp.assignedDeskId); // 원래 자리 (출발 시 예약 보존됨)
        if (desk == null) desk = DeskManager.Instance.GetEmptyDesk();
        if (desk == null) { Debug.LogWarning("[Dispatch] 복귀 배정할 데스크 없음"); return; }

        DeskManager.Instance.AssignDesk(desk.deskId, employeeId);
        emp.assignedDeskId = desk.deskId;
        oc.Init(employeeId, desk);
        oc.GoToDesk();
    }

    // 씬 로드 시 기존 직원 복원
    public void RestoreEmployees()
    {
        // CEO 먼저 점유 → 일반 직원이 ceoDeskId(예: desk_04) 못 받게 보호
        SpawnCEO();
        // 비서도 같은 패턴 — 점유 먼저 (desk_03 보호)
        SpawnSecretary();

        string ceoDeskId       = CEOManager.Instance != null ? CEOManager.Instance.ceoDeskId : "";
        string secretaryDeskId = !string.IsNullOrEmpty(this.secretaryDeskId) ? this.secretaryDeskId : "";

        // 파견중 직원의 원래 자리를 먼저 예약(점유) — 캐릭터는 스폰 안 하되 다른 직원이 그 자리를 못 앉게(항상 비워둠).
        // 자리는 DispatchManager.DispatchDeskId(영속) 기준, 비어 있으면 직원 assignedDeskId 폴백. 복귀 시 같은 자리로 돌아감.
        if (DispatchManager.Instance != null && DispatchManager.Instance.IsActive)
        {
            string dEmpId  = DispatchManager.Instance.DispatchEmployeeId;
            string dDeskId = DispatchManager.Instance.DispatchDeskId;
            if (string.IsNullOrEmpty(dDeskId))
            {
                var dEmp = EmployeeManager.Instance.ownedEmployees.Find(e => e.id == dEmpId);
                if (dEmp != null) dDeskId = dEmp.assignedDeskId;
            }
            if (!string.IsNullOrEmpty(dDeskId)
                && (string.IsNullOrEmpty(ceoDeskId)       || dDeskId != ceoDeskId)
                && (string.IsNullOrEmpty(secretaryDeskId) || dDeskId != secretaryDeskId)
                && DeskManager.Instance.GetDeskById(dDeskId) != null)
                DeskManager.Instance.AssignDesk(dDeskId, dEmpId);
        }

        foreach (var employee in EmployeeManager.Instance.ownedEmployees)
        {
            // 파견중 직원은 사무실에 없음 — 자리는 위에서 예약됨. 복귀 시 ReturnFromDispatch 가 스폰
            if (DispatchManager.Instance != null && DispatchManager.Instance.IsDispatched(employee.id)) continue;

            WorkStation desk;
            if (string.IsNullOrEmpty(employee.assignedDeskId))
                desk = DeskManager.Instance.GetEmptyDesk();
            else
                desk = DeskManager.Instance.GetDeskById(employee.assignedDeskId);

            // CEO 또는 비서가 점유한 desk 와 충돌하면 빈 자리로 재할당 (마이그레이션)
            if (desk != null &&
                ((!string.IsNullOrEmpty(ceoDeskId)       && desk.deskId == ceoDeskId) ||
                 (!string.IsNullOrEmpty(secretaryDeskId) && desk.deskId == secretaryDeskId)))
            {
                // [테스트] desk_03 겹침 허용 모드면 비서석에 저장된 직원을 그대로 복원(재할당 안 함)
                bool overflowKeep = DeskManager.Instance.overflowToDesk03
                                    && !string.IsNullOrEmpty(secretaryDeskId)
                                    && desk.deskId == secretaryDeskId;
                if (!overflowKeep)
                    desk = DeskManager.Instance.GetEmptyDesk();
            }

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

    // 비서 스폰 — secretaryDeskId 고정 배치, EmployeeData 없는 시각 NPC.
    // ownedEmployees 에 없으므로 만족도/아이템/팀장후보/이벤트 자동 제외.
    public void SpawnSecretary()
    {
        if (secretaryPrefab == null || string.IsNullOrEmpty(secretaryDeskId)) return;

        var desk = DeskManager.Instance.GetDeskById(secretaryDeskId);
        if (desk == null)
        {
            Debug.LogWarning($"[Secretary] '{secretaryDeskId}' 데스크 없음 - 스폰 스킵");
            return;
        }

        DeskManager.Instance.AssignDesk(desk.deskId, secretaryId);

        var obj = Instantiate(secretaryPrefab, desk.GetWorkWorldPos(), Quaternion.identity);
        var oc  = obj.GetComponent<OfficeCharacter>();
        if (oc == null)
        {
            Debug.LogError("[Secretary] secretaryPrefab 에 OfficeCharacter 컴포넌트 없음");
            Destroy(obj);
            return;
        }
        oc.Init(secretaryId, desk);

        _characters[secretaryId] = oc;

        Debug.Log($"[Secretary] {desk.deskId} 스폰");
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

    // 개발 시작 시 호출 — 이전 프로젝트에서 남은 개발틱 팝업 큐/카운터 정리.
    // 잔재 ActiveCount 가 LeaderSelectUI 의 카운트업 대기를 막아 딜레이/hang 을 유발하던 것 방지.
    public void ClearAllPopups()
    {
        foreach (var oc in _characters.Values)
            oc?.ForceClearPopups();
        StatTickPopup.ActiveCount = 0;
    }

    // 개발 시작 시 호출 — patrol 포인트 갱신
    public void StartDevelopmentPatrol()
    {
        _patrolPoints = FindObjectsByType<PatrolPoint>(FindObjectsSortMode.None);
        _dialogPatrolPoints = FindObjectsByType<DialogPatrolPoint>(FindObjectsSortMode.None);
        EnsurePatrolScheduler();
    }

    // 사무실 레이아웃 전환(Level1→Level4 등) 등으로 patrol point가 새로 활성화/비활성화됐을 때 재스캔.
    // _patrolPoints는 씬 시작 시(RestoreEmployees→EnsurePatrolScheduler) 한 번 캐싱된 뒤 ??=라서
    // 이후 새로 활성화된 PatrolPoint(예: Level4의 p3/p4)는 자동으로 안 잡힌다 — 명시적 재스캔 필요.
    public void RefreshPatrolPoints()
    {
        _patrolPoints = FindObjectsByType<PatrolPoint>(FindObjectsSortMode.None);
        _dialogPatrolPoints = FindObjectsByType<DialogPatrolPoint>(FindObjectsSortMode.None);
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

    // [테스트] 지정된 (기존데스크→새데스크) 매핑대로 자리 잡고 있는 직원을 걷지 않고 순간이동.
    // 새 사무실 타일 확장 테스트용 — assignedDesk/DeskManager 배정만 갱신, 백엔드 저장은 안 함(테스트 전용이라).
    // 반환값: 실제로 이동된 employeeId 목록(빈 자리였던 매핑은 스킵됨).
    public List<string> WarpDesksInstant(params (string fromDeskId, string toDeskId)[] mappings)
    {
        var movedEmployeeIds = new List<string>();
        foreach (var (fromDeskId, toDeskId) in mappings)
        {
            OfficeCharacter target = null;
            foreach (var oc in _characters.Values)
            {
                if (oc.assignedDesk != null && oc.assignedDesk.deskId == fromDeskId) { target = oc; break; }
            }
            if (target == null)
            {
                Debug.LogWarning($"[Test] {fromDeskId} 자리에 직원 없음 — 스킵");
                continue;
            }

            var newDesk = DeskManager.Instance.GetDeskById(toDeskId);
            if (newDesk == null)
            {
                Debug.LogWarning($"[Test] {toDeskId} 데스크를 찾을 수 없음 — 스킵");
                continue;
            }

            DeskManager.Instance.UnassignDesk(fromDeskId);
            DeskManager.Instance.AssignDesk(toDeskId, target.employeeId);

            target.assignedDesk = newDesk;
            target.transform.position = newDesk.GetWorkWorldPos();
            target.ApplyDeskAnimation();

            var emp = EmployeeManager.Instance?.GetEmployee(target.employeeId);
            if (emp != null) emp.assignedDeskId = toDeskId;

            movedEmployeeIds.Add(target.employeeId);
            Debug.Log($"[Test] {target.employeeId}: {fromDeskId} → {toDeskId} 순간이동");
        }
        return movedEmployeeIds;
    }

    // 특정 직원을 pointId 위치로 즉시 강제 이동 (이벤트 트리거용)
    public void ForceCharacterToPatrolPoint(string employeeId, string pointId, float stayDuration = 5f)
    {
        if (!_characters.TryGetValue(employeeId, out var oc)) return;
        _patrolPoints ??= FindObjectsByType<PatrolPoint>(FindObjectsSortMode.None);
        var point = System.Array.Find(_patrolPoints, p => p.pointId == pointId);
        if (point == null)
        {
            Debug.LogWarning($"[ForcePatrol] pointId '{pointId}' 를 찾을 수 없음");
            return;
        }
        oc.ForcePatrolTo(point.transform, stayDuration);
    }

    // 개발 완료 시 호출 — CEO/비서를 제외한 일반 직원 중 랜덤으로 patrol 보냄.
    // 후보를 셔플한 뒤 앞에서부터 서로 다른 지점(A/B)에 1명씩 배정하는 방식이라 같은 지점에 겹칠 수 없음.
    // 후보가 1명뿐이면 그 1명만 pointA로, 0명이면 아무도 보내지 않음.
    public void TriggerDevelopmentCompletePatrol(string pointIdA, string pointIdB, float stayDuration = 5f)
    {
        if (string.IsNullOrEmpty(pointIdA) || string.IsNullOrEmpty(pointIdB)) return;

        _patrolPoints ??= FindObjectsByType<PatrolPoint>(FindObjectsSortMode.None);
        if (_patrolPoints == null || _patrolPoints.Length == 0) return;

        var pointA = System.Array.Find(_patrolPoints, p => p.pointId == pointIdA);
        var pointB = System.Array.Find(_patrolPoints, p => p.pointId == pointIdB);
        if (pointA == null || pointB == null)
        {
            Debug.LogWarning($"[Patrol] 개발완료 patrol point 없음: {pointIdA} / {pointIdB}");
            return;
        }

        string ceoId = EmployeeManager.Instance?.CEO?.id;
        var candidates = new List<OfficeCharacter>();
        foreach (var oc in _characters.Values)
        {
            if (oc.employeeId == secretaryId) continue;
            if (!string.IsNullOrEmpty(ceoId) && oc.employeeId == ceoId) continue;
            candidates.Add(oc);
        }
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }
        if (candidates.Count == 0) return;

        candidates[0].ForcePatrolTo(pointA.transform, stayDuration);
        if (candidates.Count >= 2)
            candidates[1].ForcePatrolTo(pointB.transform, stayDuration);
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

            // 자동 random patrol 비활성 — 직원이 자기 데스크 떠나 master_desk 등으로 무작위 이동하는 문제.
            // 다이얼로그 patrol 과 명시적 ForcePatrolTo (상인 등) 는 그대로 동작.
            // 다시 켜려면 아래 주석 해제 + CEO/비서 제외 가드 + master_desk 같은 system PatrolPoint 풀 분리 필요.
            // float devProgress = DevelopmentManager.Instance.developmentDuration > 0
            //     ? DevelopmentManager.Instance.GetElapsed() / DevelopmentManager.Instance.developmentDuration
            //     : 0f;
            // if (_patrolPoints != null && _patrolPoints.Length > 0
            //     && DevelopmentManager.Instance.CurrentStage != ProjectStage.BugFixing
            //     && devProgress < 0.7f)
            //     TriggerPatrolRandom(patrolCountPerCycle);
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
