using UnityEngine;

public class WorkStation : MonoBehaviour
{
    public Transform workPoint; // 자식 빈 오브젝트로 설정

public Vector3Int GetWorkCell()
{
    return GridManager.Instance.WorldToCell(workPoint.position);
}

public Vector3 GetWorkWorldPos()
{
    return workPoint.position; // 정확한 위치 반환
}
}