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

        for (int i = 0; i < path.Count; i++)
        {
            // 마지막 셀이면 정확한 WorkPoint 월드좌표 사용
            Vector3 targetWorld = (i == path.Count - 1 && _targetWorldPos.HasValue)
                ? _targetWorldPos.Value
                : GridManager.Instance.CellToWorld(path[i]);

            Vector3 targetFlat = new Vector3(targetWorld.x, targetWorld.y, 0);

            while (Vector3.Distance(
                new Vector3(transform.position.x, transform.position.y, 0),
                targetFlat) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    new Vector3(transform.position.x, transform.position.y, 0),
                    targetFlat,
                    moveSpeed * Time.deltaTime);
                yield return null;
            }

            transform.position = new Vector3(targetWorld.x, targetWorld.y, targetWorld.y * 0.01f);
            OnCellChanged?.Invoke(path[i]);
        }

        _targetWorldPos = null;
        IsMoving = false;
        OnMoveComplete?.Invoke();
    }
}