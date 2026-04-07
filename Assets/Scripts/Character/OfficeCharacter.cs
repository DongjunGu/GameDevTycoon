using System.Collections;
using UnityEngine;

public class OfficeCharacter : MonoBehaviour
{
    public string employeeId;
    public WorkStation assignedDesk;
    public Transform statPopupAnchor; // 머리 위 위치 (Inspector에서 설정)

    public CharacterState State { get; private set; } = CharacterState.Working;
    public bool IsPatrolling => State == CharacterState.Patrolling;

    private CharacterController _controller;
    private CharacterMover _mover;
    private Coroutine _patrolCoroutine;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _mover = GetComponent<CharacterMover>();
    }

    // 채용 시 초기화
    public void Init(string empId, WorkStation desk)
    {
        employeeId   = empId;
        assignedDesk = desk;
    }

    // 지정된 Desk로 이동
    public void GoToDesk()
    {
        if (assignedDesk == null) return;
        _controller.MoveTo(
            assignedDesk.GetWorkCell(),
            assignedDesk.GetWorkWorldPos()
        );
    }

    // 특정 지점으로 patrol 시작 → 일정 시간 대기 후 자동 복귀
    public void StartPatrol(Transform target, float stayDuration)
    {
        if (IsPatrolling) return;
        if (_patrolCoroutine != null) StopCoroutine(_patrolCoroutine);
        _patrolCoroutine = StartCoroutine(PatrolRoutine(target, stayDuration));
    }

    // 다이얼로그 patrol — 도착 시 다이얼로그 실행, 끝나면 복귀
    public void StartPatrolWithDialog(Transform target, string dialogGroupId, bool triggerOnce)
    {
        if (IsPatrolling) return;
        if (_patrolCoroutine != null) StopCoroutine(_patrolCoroutine);
        _patrolCoroutine = StartCoroutine(PatrolWithDialogRoutine(target, dialogGroupId, triggerOnce));
    }

    // 개발 완료 등 외부 이벤트로 즉시 복귀
    public void CancelPatrol()
    {
        if (State != CharacterState.Patrolling) return;
        if (_patrolCoroutine != null)
        {
            StopCoroutine(_patrolCoroutine);
            _patrolCoroutine = null;
        }
        State = CharacterState.Working;
        GoToDesk();
    }

    IEnumerator PatrolRoutine(Transform target, float stayDuration)
    {
        State = CharacterState.Patrolling;

        // 1. patrol 지점으로 이동
        Vector3 targetPos = new Vector3(target.position.x, target.position.y, 0);
        Vector3Int targetCell = GridManager.Instance.WorldToCell(targetPos);
        _controller.MoveTo(targetCell, target.position);

        yield return null; // 이동 시작 대기 (IsMoving이 다음 프레임에 설정됨)
        yield return new WaitUntil(() => !_mover.IsMoving);

        // 2. 목적지에서 대기 (게임 시간 기준)
        float stayed = 0f;
        while (stayed < stayDuration)
        {
            if (GameTimeManager.Instance != null && GameTimeManager.Instance.IsRunning)
                stayed += Time.deltaTime;
            yield return null;
        }

        // 3. 원래 데스크로 복귀
        GoToDesk();

        yield return null;
        yield return new WaitUntil(() => !_mover.IsMoving);

        State = CharacterState.Working;
        _patrolCoroutine = null;
    }

    IEnumerator PatrolWithDialogRoutine(Transform target, string dialogGroupId, bool triggerOnce)
    {
        State = CharacterState.Patrolling;

        // 1. 목적지로 이동
        Vector3 targetPos = new Vector3(target.position.x, target.position.y, 0);
        Vector3Int targetCell = GridManager.Instance.WorldToCell(targetPos);
        _controller.MoveTo(targetCell, target.position);

        yield return null;
        yield return new WaitUntil(() => !_mover.IsMoving);

        // 2. 다이얼로그 실행
        if (!string.IsNullOrEmpty(dialogGroupId)
            && DialogManager.Instance != null
            && DialogManager.Instance.HasGroup(dialogGroupId))
        {
            // 이 직원의 이름·초상화를 플레이스홀더로 세팅
            var empData = EmployeeManager.Instance.GetEmployee(employeeId);
            if (empData != null)
            {
                DialogManager.Instance.SetContextEmployeeId(employeeId);
                DialogManager.Instance.SetPlaceholder("employeeName", empData.employeeName);
                DialogManager.Instance.SetPlaceholder("portraitId", empData.portraitId);
            }

            bool dialogDone = false;

            void OnEnd()
            {
                DialogManager.Instance.OnDialogEnd -= OnEnd;
                dialogDone = true;
                GameTimeManager.Instance?.StartTime();
            }

            GameTimeManager.Instance?.StopTime();
            DialogManager.Instance.OnDialogEnd += OnEnd;
            EventDialogTable.PlayManual(dialogGroupId, triggerOnce);

            yield return new WaitUntil(() => dialogDone);
        }

        // 3. 원래 데스크로 복귀
        GoToDesk();

        yield return null;
        yield return new WaitUntil(() => !_mover.IsMoving);

        State = CharacterState.Working;
        _patrolCoroutine = null;
    }

    // 머리 위 수치 팝업 표시
    public void ShowStatPopup(string text, Color color)
    {
        if (StatFloatingTextPool.Instance == null) return;

        Vector3 pos = statPopupAnchor != null
            ? statPopupAnchor.position
            : transform.position + Vector3.up * 0.6f;

        StatFloatingTextPool.Instance.Get(pos)?.Show(text, color);
    }
}
