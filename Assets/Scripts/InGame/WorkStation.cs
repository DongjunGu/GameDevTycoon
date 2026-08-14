using UnityEngine;

public enum WorkStationType { Working, Talking }

public class WorkStation : MonoBehaviour
{
    public string          deskId;    // 인스펙터에서 고유 ID 설정
    public WorkStationType stationType = WorkStationType.Working;
    public Transform       workPoint;
    public Vector3         workPointOffset;
    [Tooltip("가구 스프라이트가 좌우 반전된 자리(예: CEO/비서석)용 — 켜면 여기 앉은 캐릭터도 SetWorking/SetIdle이 매번 되돌리는 flipX=false를 무시하고 계속 좌우 반전 유지")]
    public bool             flipXWhileSeated = false;

    public Vector3Int GetWorkCell()
    {
        if (workPoint == null) { Debug.LogWarning($"[WorkStation] {deskId} workPoint is null!"); return Vector3Int.zero; }
        return GridManager.Instance.WorldToCell(workPoint.position + workPointOffset);
    }

    public Vector3 GetWorkWorldPos()
    {
        if (workPoint == null) { Debug.LogWarning($"[WorkStation] {deskId} workPoint is null!"); return Vector3.zero; }
        return workPoint.position + workPointOffset;
    }
}