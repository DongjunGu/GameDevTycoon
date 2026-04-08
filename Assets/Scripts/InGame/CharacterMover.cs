using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterMover : MonoBehaviour
{
    [Header("Settings")]
    public float moveSpeed = 3f;

    private Coroutine _moveCoroutine;

    public event System.Action OnMoveComplete;
    public event System.Action<Vector3Int> OnCellChanged;

    public bool IsMoving { get; private set; }
    public int CurrentFloor { get; private set; } = 0; // ★ 현재 층

    private Vector3? _targetWorldPos = null;
    private CharacterAnimator _anim;
    private SpriteRenderer _sr;

    void Awake()
    {
        _anim = GetComponent<CharacterAnimator>();
        _sr = GetComponent<SpriteRenderer>();
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
            var cell = path[i];

            // ★ 계단 진입 시 목표 Y에 층 오프셋 반영된 worldPos 사용
            Vector3 targetWorld = (i == path.Count - 1 && _targetWorldPos.HasValue)
                ? _targetWorldPos.Value
                : GridManager.Instance.CellToWorld(cell); // heightPerFloor 이미 포함

            // ★ z는 항상 0 유지 (isometric 2D), Y에 높이가 녹아있음
            Vector3 targetFlat  = new Vector3(targetWorld.x, targetWorld.y, 0);
            Vector3 currentFlat = new Vector3(transform.position.x, transform.position.y, 0);

            _anim?.UpdateDirection(currentFlat, targetFlat);

            while (Vector3.Distance(
                new Vector3(transform.position.x, transform.position.y, 0),
                targetFlat) > 0.01f)
            {
                if (GameTimeManager.Instance != null && !GameTimeManager.Instance.IsRunning)
                {
                    yield return null;
                    continue;
                }

                transform.position = Vector3.MoveTowards(
                    new Vector3(transform.position.x, transform.position.y, 0),
                    targetFlat,
                    moveSpeed * Time.deltaTime);

                // ★ 소팅 오더: 층이 높을수록 무조건 앞에
                if (_sr != null)
                    _sr.sortingOrder = Mathf.RoundToInt(-transform.position.y * 100f)
                                       + CurrentFloor * 10000;

                yield return null;
            }

            transform.position = targetWorld;

            // ★ 층 갱신 (계단 통과 시 자동 반영)
            CurrentFloor = cell.z;

            OnCellChanged?.Invoke(cell);
        }

        _targetWorldPos = null;
        IsMoving = false;
        _anim?.SetMoving(false);
        OnMoveComplete?.Invoke();
    }
}