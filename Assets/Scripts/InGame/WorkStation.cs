using UnityEngine;

public class WorkStation : MonoBehaviour
{
    public string    deskId;    // 인스펙터에서 고유 ID 설정
    public Transform workPoint;

    public Vector3Int GetWorkCell()
        => GridManager.Instance.WorldToCell(workPoint.position);

    public Vector3 GetWorkWorldPos()
        => workPoint.position;
}