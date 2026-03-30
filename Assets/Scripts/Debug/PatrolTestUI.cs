using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// patrol 테스트용 버튼 UI.
/// 씬의 Canvas 하위 빈 GameObject에 추가 후 버튼 연결.
/// </summary>
public class PatrolTestUI : MonoBehaviour
{
    [Header("Random N명")]
    public Button btnPatrolRandom;
    public int randomCount = 1;

    [Header("특정 직원")]
    public Button btnPatrolEmployee;
    public string targetEmployeeId;

    void Start()
    {
        btnPatrolRandom?.onClick.AddListener(OnClickRandom);
        btnPatrolEmployee?.onClick.AddListener(OnClickEmployee);
    }

    void OnClickRandom()
    {
        OfficeManager.Instance?.TriggerPatrolRandom(randomCount);
    }

    void OnClickEmployee()
    {
        if (string.IsNullOrEmpty(targetEmployeeId))
        {
            Debug.LogWarning("[PatrolTest] targetEmployeeId가 비어있습니다.");
            return;
        }
        OfficeManager.Instance?.TriggerPatrolForEmployee(targetEmployeeId);
    }
}
