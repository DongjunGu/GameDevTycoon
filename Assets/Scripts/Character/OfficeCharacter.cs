using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class OfficeCharacter : MonoBehaviour, IPointerClickHandler
{
    private struct PopupRequest
    {
        public bool    isBlock;
        // stat tick
        public Sprite  stat;
        public string  statKey;   // 흡입 타겟(스탯 텍스트) 결정용
        public int     amount;
        public Color   color;
        public bool    isJackpot;
        // block
        public int[][] cells;
    }
    private readonly Queue<PopupRequest> _popupQueue = new();
    private bool _popupActive;

    public string employeeId;
    public WorkStation assignedDesk;

    // 창의성 블록 팝업(BlockFloatingVisual)이 실제로 spawn된 순간 발동 — 튜토리얼이 그 오브젝트를
    // 강조 대상으로 잡을 때 사용(EnqueuePopup으로 대기열에 밀렸다가 나중에 뜨는 경우까지 커버).
    public event System.Action<Transform> OnBlockPopupSpawned;
    public Transform statPopupAnchor; // 머리 위 위치 (Inspector에서 설정)
    public Vector2 statTickPopupOffset = new Vector2(1.2f, 0.7f); // StatTickPopup 위치 (캐릭터 기준 오프셋)

    [SerializeField] private CharacterState _state = CharacterState.Idle;
    public CharacterState State => _state;
    public bool IsPatrolling => _state == CharacterState.Patrolling;

    public void SetState(CharacterState state) => _state = state;

    private CharacterController _controller;
    private CharacterMover _mover;
    private CharacterAnimator _animator;
    private Coroutine _patrolCoroutine;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _mover      = GetComponent<CharacterMover>();
        _animator   = GetComponent<CharacterAnimator>();
        _mover.OnMoveComplete += OnArrived;
    }

    // 캐릭터 클릭 → 직원 카드 UI 표시 (시간은 멈추지 않음)
    // New Input System 모드에서는 OnMouseDown이 동작 안 하므로 IPointerClickHandler 사용
    // 카메라에 Physics2DRaycaster 컴포넌트 + 씬에 EventSystem 필요
    public void OnPointerClick(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(employeeId)) return;
        EmployeeCardUI.Instance?.Show(employeeId);
    }

    // 이동 완료 시 — 데스크 타입 + 프로젝트 진행 여부로 애니메이션 결정
    void OnArrived()
    {
        if (State == CharacterState.Patrolling) return;
        ApplyDeskAnimation();
    }

    public void SetDeskWorking()
    {
        if (_mover != null && _mover.IsMoving) return;
        if (_state == CharacterState.Patrolling) return;
        _state = CharacterState.Working;
        _animator?.SetWorking(true);
    }

    public void ApplyDeskAnimation()
    {
        if (assignedDesk == null) return;
        if (_mover != null && _mover.IsMoving) return;
        if (_state == CharacterState.Patrolling) return;

        if (assignedDesk.stationType == WorkStationType.Talking)
        {
            _state = CharacterState.Idle;
            _animator?.SetTalking();
            return;
        }

        var stage = DevelopmentManager.Instance != null ? DevelopmentManager.Instance.CurrentStage : ProjectStage.None;
        bool isDeveloping = stage == ProjectStage.Developing || stage == ProjectStage.BugFixing;
        if (isDeveloping)
        {
            _state = CharacterState.Working;
            _animator?.SetWorking(true);
        }
        else
        {
            _state = CharacterState.Idle;
            _animator?.SetIdle(true);
        }
    }

    // 해고 시 호출 — 패트롤 중단, 데스크 해제, 상태 초기화
    public void PrepareToLeave()
    {
        if (_patrolCoroutine != null)
        {
            StopCoroutine(_patrolCoroutine);
            _patrolCoroutine = null;
        }
        assignedDesk = null;
        _state = CharacterState.Idle;
    }

    // 채용 시 초기화
    public void Init(string empId, WorkStation desk)
    {
        employeeId   = empId;
        assignedDesk = desk;
    }

    // 이동 없이 즉시 방향만 강제 적용 (스폰 직후 등 patrol 코루틴 없이 바로 세팅할 때 — 튜토리얼 비서 스폰 등)
    public void ApplyFacing(bool front, bool flipX) => _animator?.SetFacing(front, flipX);

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

    // 현재 patrol 중단 후 즉시 지정 위치로 강제 이동 (이벤트 트리거용)
    // onArrived: 방향 강제 적용까지 끝난 직후(도착 시점) 1회 호출 — 외부에서 도착 대기용
    public void ForcePatrolTo(Transform target, float stayDuration, System.Action onArrived = null)
    {
        if (_patrolCoroutine != null) StopCoroutine(_patrolCoroutine);
        _patrolCoroutine = StartCoroutine(PatrolRoutine(target, stayDuration, onArrived));
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
        _state = CharacterState.Moving; // Patrolling 해제 → OnArrived에서 ApplyDeskAnimation 호출됨
        GoToDesk();
    }

    IEnumerator PatrolRoutine(Transform target, float stayDuration, System.Action onArrived = null)
    {
        _state = CharacterState.Patrolling;
        Debug.Log($"[Patrol] {employeeId} → Patrolling 시작, target={target.name}");
        _animator?.SetWorking(false);

        // 1. patrol 지점으로 이동
        Vector3 targetPos = new Vector3(target.position.x, target.position.y, 0);
        Vector3Int targetCell = GridManager.Instance.WorldToCell(targetPos);
        _controller.MoveTo(targetCell, target.position);

        yield return null; // 이동 시작 대기 (IsMoving이 다음 프레임에 설정됨)
        Debug.Log($"[Patrol] {employeeId} → 이동 시작 후 IsMoving={_mover.IsMoving}");
        yield return new WaitUntil(() => !_mover.IsMoving);
        Debug.Log($"[Patrol] {employeeId} → patrol 지점 도착, State={State}");

        // 패트롤 도착 → 방향 강제 적용 (패트롤 포인트에 설정된 경우)
        var pp = target.GetComponent<PatrolPoint>();
        if (pp != null && pp.overrideFacing)
            _animator?.SetFacing(pp.facingFront, pp.facingFlipX);

        onArrived?.Invoke();

        // 패트롤 도착 → 이벤트 발동 체크 (개발 외 타이밍도 포함: 사내연애 등)
        RandomEventManager.Instance?.OnPatrolArrived(pp != null ? pp.pointId : "", employeeId);

        // 2. 목적지에서 대기 (게임 시간 기준)
        float stayed = 0f;
        while (stayed < stayDuration)
        {
            if (GameTimeManager.Instance != null && GameTimeManager.Instance.IsRunningForMovement)
                stayed += Time.deltaTime;
            yield return null;
        }
        Debug.Log($"[Patrol] {employeeId} → 대기 완료, GoToDesk 호출");

        // 3. 원래 데스크로 복귀
        _state = CharacterState.Moving; // Patrolling 해제 → OnArrived/ApplyDeskAnimation 정상 동작
        GoToDesk();

        yield return null;
        Debug.Log($"[Patrol] {employeeId} → GoToDesk 후 IsMoving={_mover.IsMoving}");
        yield return new WaitUntil(() => !_mover.IsMoving);
        Debug.Log($"[Patrol] {employeeId} → 데스크 복귀 완료");

        _patrolCoroutine = null;
        ApplyDeskAnimation();
    }

    IEnumerator PatrolWithDialogRoutine(Transform target, string dialogGroupId, bool triggerOnce)
    {
        _state = CharacterState.Patrolling;
        _animator?.SetWorking(false);

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
        _state = CharacterState.Moving; // Patrolling 해제 → OnArrived/ApplyDeskAnimation 정상 동작
        GoToDesk();

        yield return null;
        yield return new WaitUntil(() => !_mover.IsMoving);

        _patrolCoroutine = null;
        ApplyDeskAnimation();
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

    // 창의성 블록 팝업도 같은 큐로 — 다른 stat tick과 겹치지 않게
    public void ShowBlockPopup(int[][] cells, Color color)
    {
        EnqueuePopup(new PopupRequest { isBlock = true, cells = cells, color = color });
    }

    // 상시 개발틱 팝업
    public void ShowStatTickPopup(Sprite statSprite, string statKey, int amount, Color color, bool isJackpot)
    {
        if (StatTickPopupPool.Instance == null) return;
        EnqueuePopup(new PopupRequest
        {
            isBlock = false,
            stat = statSprite, statKey = statKey, amount = amount, color = color, isJackpot = isJackpot
        });
    }

    void EnqueuePopup(PopupRequest req)
    {
        StatTickPopup.ActiveCount++;   // 대기 + 표시 합계
        if (_popupActive)
            _popupQueue.Enqueue(req);
        else
            SpawnPopup(req);
    }

    void SpawnPopup(PopupRequest req)
    {
        _popupActive = true;

        if (req.isBlock)
        {
            Vector3 pos = statPopupAnchor != null
                ? statPopupAnchor.position
                : transform.position + Vector3.up * 0.6f;
            var bfv = BlockFloatingVisual.Spawn(pos, req.cells, req.color, OnPopupFinished);
            OnBlockPopupSpawned?.Invoke(bfv.transform);
        }
        else
        {
            Vector3 pos = transform.position
                        + new Vector3(statTickPopupOffset.x, statTickPopupOffset.y, 0f);
            var p = StatTickPopupPool.Instance.Get(pos);
            if (p == null)
            {
                // 풀 실패 — 즉시 콜백
                OnPopupFinished();
                return;
            }
            p.Show(req.stat, req.statKey, req.amount, req.color, req.isJackpot, OnPopupFinished);
        }
    }

    void OnPopupFinished()
    {
        _popupActive = false;
        StatTickPopup.ActiveCount = Mathf.Max(0, StatTickPopup.ActiveCount - 1);
        if (_popupQueue.Count > 0)
            SpawnPopup(_popupQueue.Dequeue());
    }

    void OnDestroy()
    {
        // 큐+활성 카운터 누수 방지
        int leftover = _popupQueue.Count + (_popupActive ? 1 : 0);
        if (leftover > 0)
            StatTickPopup.ActiveCount = Mathf.Max(0, StatTickPopup.ActiveCount - leftover);
    }

    // 개발 시작 등에서 호출 — 이 캐릭터의 개발틱 팝업 큐/활성 상태를 정리.
    // (전역 StatTickPopup.ActiveCount 리셋은 호출측 OfficeManager.ClearAllPopups 가 담당)
    public void ForceClearPopups()
    {
        _popupQueue.Clear();
        _popupActive = false;
    }
}
