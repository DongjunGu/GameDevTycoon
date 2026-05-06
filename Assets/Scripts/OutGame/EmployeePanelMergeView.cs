using UnityEngine;
using UnityEngine.UI;

// EmployeePanel의 합성 뷰 토글
// - Start: ContainerPanel 비활성, MiddlePanel 활성
// - MergeBtn 클릭: ContainerPanel 활성, MiddlePanel 비활성
// - BackBtn 클릭: 반대로
public class EmployeePanelMergeView : MonoBehaviour
{
    [Header("Buttons")]
    public Button mergeButton;
    public Button backButton;

    [Header("Panels")]
    public GameObject containerPanel;
    public GameObject middlePanel;

    void Awake()
    {
        if (mergeButton != null)
        {
            mergeButton.onClick.RemoveListener(OpenMerge);
            mergeButton.onClick.AddListener(OpenMerge);
        }
        if (backButton != null)
        {
            backButton.onClick.RemoveListener(CloseMerge);
            backButton.onClick.AddListener(CloseMerge);
        }
    }

    void OnEnable()
    {
        // EmployeePanel 진입 시 항상 기본 상태(MiddlePanel)
        CloseMerge();
    }

    public void OpenMerge()
    {
        if (containerPanel != null) containerPanel.SetActive(true);
        if (middlePanel    != null) middlePanel.SetActive(false);
    }

    public void CloseMerge()
    {
        if (containerPanel != null) containerPanel.SetActive(false);
        if (middlePanel    != null) middlePanel.SetActive(true);
    }
}
