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

    // employeeId → OfficeCharacter
    private Dictionary<string, OfficeCharacter> _characters = new();

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

        Debug.Log($"{employee.employeeName} 스폰 → {desk.deskId}로 이동");
    }

    // 머리 위 수치 팝업 표시
    public void ShowStatPopup(string employeeId, string text, Color color)
    {
        if (_characters.TryGetValue(employeeId, out var oc))
            oc.ShowStatPopup(text, color);
    }

    // 해고 시 캐릭터 제거
    public void OnEmployeeFired(EmployeeData employee)
    {
        if (_characters.TryGetValue(employee.id, out var oc))
        {
            DeskManager.Instance.UnassignDesk(employee.assignedDeskId);
            Destroy(oc.gameObject);
            _characters.Remove(employee.id);
        }
    }

    // 씬 로드 시 기존 직원 복원
    public void RestoreEmployees()
    {
        foreach (var employee in EmployeeManager.Instance.ownedEmployees)
        {
            var desk = string.IsNullOrEmpty(employee.assignedDeskId)
                ? DeskManager.Instance.GetEmptyDesk()
                : DeskManager.Instance.GetDeskById(employee.assignedDeskId);

            if (desk == null) continue;

            DeskManager.Instance.AssignDesk(desk.deskId, employee.id);
            employee.assignedDeskId = desk.deskId;

            var obj = Instantiate(GetPrefab(employee.portraitId), desk.GetWorkWorldPos(), Quaternion.identity);
            var oc  = obj.GetComponent<OfficeCharacter>();
            oc.Init(employee.id, desk);

            oc.ApplyDeskAnimation();

            _characters[employee.id] = oc;
        }

        EnsurePatrolScheduler();
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

            if (_patrolPoints != null && _patrolPoints.Length > 0
                && DevelopmentManager.Instance.CurrentStage != ProjectStage.BugFixing)
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
