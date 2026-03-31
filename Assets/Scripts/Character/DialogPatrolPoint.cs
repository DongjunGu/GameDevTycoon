using UnityEngine;

/// <summary>
/// 특정 직원이 이 지점에 도달하면 다이얼로그를 실행하는 patrol 목적지 마커.
/// OfficeManager가 FindObjectsByType으로 자동 수집.
/// </summary>
public class DialogPatrolPoint : MonoBehaviour
{
    [Tooltip("이 지점에 도달했을 때 다이얼로그를 트리거할 직원 ID")]
    public string employeeId;

    [Tooltip("실행할 다이얼로그 그룹 ID (DialogManager.Play에 전달)")]
    public string dialogGroupId;

    [Tooltip("true면 한 번만 발동")]
    public bool triggerOnce = false;
}
