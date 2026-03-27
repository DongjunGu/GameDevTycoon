using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterMover : MonoBehaviour
{
    [Header("Settings")]
    public float moveSpeed = 3f;

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
            Vector3 targetWorld = (i == path.Count - 1 && _targetWorldPos.HasValue)
                ? _targetWorldPos.Value
                : GridManager.Instance.CellToWorld(path[i]);

            Vector3 targetFlat = new Vector3(targetWorld.x, targetWorld.y, 0);
            Vector3 currentFlat = new Vector3(transform.position.x, transform.position.y, 0);

            // 이동 방향으로 애니메이션 업데이트
            _anim?.UpdateDirection(currentFlat, targetFlat);

            while (Vector3.Distance(
                new Vector3(transform.position.x, transform.position.y, 0),
                targetFlat) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    new Vector3(transform.position.x, transform.position.y, 0),
                    targetFlat,
                    moveSpeed * Time.deltaTime);

                // // 소팅 오더
                // GetComponent<SpriteRenderer>().sortingOrder =
                //     Mathf.RoundToInt(-transform.position.y * 10);
// GetComponent<SpriteRenderer>().sortingOrder =
//     Mathf.RoundToInt(-transform.position.y * 100f);
                yield return null;
            }

            transform.position = new Vector3(targetWorld.x, targetWorld.y, targetWorld.y * 0.01f);
            OnCellChanged?.Invoke(path[i]);
        }

        _targetWorldPos = null;
        IsMoving = false;
        _anim?.SetMoving(false);
        OnMoveComplete?.Invoke();
    }
}