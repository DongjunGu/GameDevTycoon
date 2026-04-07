using UnityEngine;

public enum WorkStationType { Working, Talking }

public class WorkStation : MonoBehaviour
{
    public string          deskId;    // 인스펙터에서 고유 ID 설정
    public WorkStationType stationType = WorkStationType.Working;
    public Transform       workPoint;
    public Vector3         workPointOffset;

    public Vector3Int GetWorkCell()
        => GridManager.Instance.WorldToCell(workPoint.position + workPointOffset);

    public Vector3 GetWorkWorldPos()
        => workPoint.position + workPointOffset;
}