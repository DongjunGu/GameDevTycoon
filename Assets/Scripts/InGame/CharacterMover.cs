using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterMover : MonoBehaviour
{
    [Header("Settings")]
    public float moveSpeed = 3f;

    [Header("Elevator")]
    [Tooltip("엘리베이터 링크(비인접 셀 워프)를 탈 때 대기하는 시간(초) 후 순간이동")]
    public float elevatorRideDuration = 0.6f;

    private Coroutine _moveCoroutine;

    public event System.Action OnMoveComplete;
    public event System.Action<Vector3Int> OnCellChanged; // 서버 동기화 트리거용

    public bool IsMoving { get; private set; }
    private Vector3? _targetWorldPos = null;
    private CharacterAnimator _anim;

    void Awake()
    {
        _anim = GetComponent<CharacterAnimator>();
    }

    public void StartMoveTo(List<Vector3Int> path, Vector3 targetWorldPos)
    {
        _targetWorldPos = targetWorldPos;
        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        _moveCoroutine = StartCoroutine(MoveAlongPath(path));
    }

    public void StartMove(List<Vector3Int> path)
    {
        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        _moveCoroutine = StartCoroutine(MoveAlongPath(path));
    }

    public void StopMove()
    {
        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        IsMoving = false;
    }

    IEnumerator MoveAlongPath(List<Vector3Int> path)
    {
        IsMoving = true;
        _anim?.SetMoving(true);

        for (int i = 0; i < path.Count; i++)
        {
            // 셀의 기준 월드 좌표 (타일맵 중심)
            Vector3 baseWorld = GridManager.Instance.CellToWorld(path[i]);

            // 마지막 셀은 정확한 목표 위치 사용 (데스크 오프셋 등)
            Vector3 rawTarget = (i == path.Count - 1 && _targetWorldPos.HasValue)
                ? _targetWorldPos.Value
                : baseWorld;

            // 계단 고도 적용
            float elevation = GridManager.Instance.GetCellElevation(path[i]);
            Vector3 targetPos = new Vector3(rawTarget.x, rawTarget.y + elevation, 0);

            // 애니메이션 방향: 고도를 제외한 평면 이동 방향 기준
            Vector3Int fromCell = i == 0
                ? GridManager.Instance.WorldToCell(transform.position)
                : path[i - 1];
            Vector3 fromBase = GridManager.Instance.CellToWorld(fromCell);

            // 인접하지 않은 셀로의 이동 = 엘리베이터 링크(워프). 걸어서 슬라이드하지 않고 잠깐 대기 후 순간이동.
            bool isElevatorJump = Mathf.Abs(path[i].x - fromCell.x) + Mathf.Abs(path[i].y - fromCell.y) > 1;

            if (isElevatorJump)
            {
                _anim?.SetMoving(false);

                float remaining = elevatorRideDuration;
                while (remaining > 0f)
                {
                    if (GameTimeManager.Instance != null && !GameTimeManager.Instance.IsRunning)
                    {
                        yield return null;
                        continue;
                    }
                    remaining -= Time.deltaTime;
                    yield return null;
                }

                transform.position = targetPos;
                _anim?.SetMoving(true);
                OnCellChanged?.Invoke(path[i]);
                continue;
            }

            _anim?.UpdateDirection(fromBase, baseWorld);

            while (Vector3.Distance(transform.position, targetPos) > 0.01f)
            {
                if (GameTimeManager.Instance != null && !GameTimeManager.Instance.IsRunning)
                {
                    yield return null;
                    continue;
                }

                float multiplier = OfficeManager.Instance != null ? OfficeManager.Instance.CharacterSpeedMultiplier : 1f;
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    targetPos,
                    moveSpeed * multiplier * Time.deltaTime);

                yield return null;
            }

            transform.position = targetPos;
            OnCellChanged?.Invoke(path[i]);
        }

        _targetWorldPos = null;
        IsMoving = false;
        _anim?.SetMoving(false);
        OnMoveComplete?.Invoke();
    }
}