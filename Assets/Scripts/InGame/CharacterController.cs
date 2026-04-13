using System.Collections.Generic;
using UnityEngine;

public class CharacterController : MonoBehaviour
{
    public CharacterData data;

    [Header("순찰 설정")]
    public List<WorkStation> patrolStations; // 인스펙터에서 순서대로 연결
    public float waitTime = 1f;              // 도착 후 대기 시간
    public bool autoPatrol = false;          // 시작하자마자 순찰 시작 여부

    private CharacterMover _mover;
    private CharacterState _state = CharacterState.Idle;
    private int _currentPatrolIndex = 0;
    private bool _isPatrolling = false;

    public CharacterState State => _state;

    void Awake()
    {
        _mover = GetComponent<CharacterMover>();
        _mover.OnMoveComplete += OnMoveComplete;
        _mover.OnCellChanged += OnCellChanged;
    }

    void Start()
    {
        CharacterManager.Instance.Register(this);

        if (autoPatrol && patrolStations.Count > 0)
            StartPatrol();
    }

    // 순찰 시작 (외부에서도 호출 가능)
    public void StartPatrol()
    {
        _isPatrolling = true;
        _currentPatrolIndex = 0;
        MoveToCurrentStation();
    }

    public void StopPatrol()
    {
        _isPatrolling = false;
    }

    void MoveToCurrentStation()
    {
        if (patrolStations == null || patrolStations.Count == 0) return;
        var station = patrolStations[_currentPatrolIndex];
        MoveTo(station.GetWorkCell(), station.GetWorkWorldPos()); // 정확한 위치 전달
    }

    public void MoveTo(Vector3Int targetCell, Vector3? exactWorldPos = null)
    {
        Vector3 flatPos = new Vector3(transform.position.x, transform.position.y, 0);
        Vector3Int currentCell = GridManager.Instance.WorldToCell(flatPos);

        var path = AStarPathfinder.Instance.FindPath(currentCell, targetCell);
        if (path == null || path.Count == 0)
        {
            bool startWalkable = GridManager.Instance.IsWalkable(currentCell);
            bool goalWalkable  = GridManager.Instance.IsWalkable(targetCell);
            Debug.LogWarning($"{name}: 경로 없음 | 현재셀={currentCell}(walkable={startWalkable}) → 목표셀={targetCell}(walkable={goalWalkable})");
            return;
        }

        SetState(CharacterState.Moving);

        if (exactWorldPos.HasValue)
            _mover.StartMoveTo(path, exactWorldPos.Value); // 정확한 위치 사용
        else
            _mover.StartMove(path); // 셀 중심 사용
    }

    public void SetState(CharacterState newState)
    {
        _state = newState;
        if (data != null) data.state = newState.ToString();
        Debug.Log($"{name} 상태: {newState}");
    }

    void OnMoveComplete()
    {
        SetState(CharacterState.Working);


        if (_isPatrolling)
        {
            _currentPatrolIndex = (_currentPatrolIndex + 1) % patrolStations.Count;
            Invoke(nameof(MoveToCurrentStation), waitTime);
        }
    }

    void OnCellChanged(Vector3Int cell)
    {
        if (data != null) { data.cellX = cell.x; data.cellY = cell.y; }
    }
}