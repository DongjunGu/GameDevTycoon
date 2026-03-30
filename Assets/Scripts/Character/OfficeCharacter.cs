using System.Collections;
using UnityEngine;

public class OfficeCharacter : MonoBehaviour
{
    public string employeeId;
    public WorkStation assignedDesk;
    public Transform statPopupAnchor; // 머리 위 위치 (Inspector에서 설정)

    public bool IsPatrolling { get; private set; }

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

    // 개발 완료 등 외부 이벤트로 즉시 복귀
    public void CancelPatrol()
    {
        if (!IsPatrolling) return;
        if (_patrolCoroutine != null)
        {
            StopCoroutine(_patrolCoroutine);
            _patrolCoroutine = null;
        }
        IsPatrolling = false;
        GoToDesk();
    }

    IEnumerator PatrolRoutine(Transform target, float stayDuration)
    {
        IsPatrolling = true;

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

        IsPatrolling = false;
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
