using UnityEngine;

/// <summary>
/// 씬에 배치하는 patrol 목적지 마커.
/// OfficeManager가 FindObjectsByType으로 자동 수집.
/// pointId: 특정 이벤트와 연결할 때 Inspector에서 설정 (비어있으면 일반 지점)
/// </summary>
public class PatrolPoint : MonoBehaviour
{
    public string pointId = "";

    [Header("도착 시 방향 강제")]
    public bool overrideFacing = false;
    public bool facingFront    = true;
    public bool facingFlipX    = false;
}
