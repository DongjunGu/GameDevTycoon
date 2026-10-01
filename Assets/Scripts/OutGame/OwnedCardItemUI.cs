using System;
using UnityEngine;
using UnityEngine.UI;

// 보유 카드 1장 (employeeId + grade + stage)을 표시하는 카드 UI
// 중복은 카드 GameObject를 N개 생성해서 표현 (count 표시 없음)
// 표시(이름/초상화/직군/등급 프레임/합성 단계)는 같은 오브젝트의 EmployeePanelItemUI.SetPreview 에 위임
// → ItemPrefabOwnedCard 는 ItemPrefabEmployeePanel 의 Prefab Variant (+ 이 컴포넌트 + DimOverlay)
public class OwnedCardItemUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("카드 표시 담당. 비우면 같은 오브젝트에서 자동 탐색")]
    public EmployeePanelItemUI card;
    public Button button;
    [Tooltip("합성 매칭 안 되는 카드 dim 오버레이 (검정 알파 50%)")]
    public GameObject dimOverlay;
    [Tooltip("합성 슬롯에 올라간 카드 표시 (DimOverlayCheck). 비우면 이름으로 자동 탐색")]
    public GameObject checkOverlay;

    public string EmployeeId { get; private set; }
    // 합성 슬롯에 올라가 있는지 — 선택된 카드는 다시 클릭해도 슬롯에 안 들어간다
    public bool IsSelected { get; private set; }
    public EmployeeGrade Grade { get; private set; }
    public int Stage { get; private set; }

    public event Action<OwnedCardItemUI> OnClicked;

    void Awake()
    {
        if (card == null) card = GetComponent<EmployeePanelItemUI>();
        if (button == null) button = GetComponent<Button>();
        if (dimOverlay == null) { var t = transform.Find("DimOverlay"); if (t != null) dimOverlay = t.gameObject; }
        if (dimOverlay != null) dimOverlay.SetActive(false);
        if (checkOverlay == null) { var t = transform.Find("DimOverlayCheck"); if (t != null) checkOverlay = t.gameObject; }
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if (checkOverlay != null) checkOverlay.SetActive(selected);
    }

    public void SetDimmed(bool dim)
    {
        if (dimOverlay != null) dimOverlay.SetActive(dim);
        if (button != null) button.interactable = !dim;
    }

    public void SetData(string empId, EmployeeGrade grade, int stage, EmployeeData masterEmp = null)
    {
        EmployeeId = empId;
        Grade      = grade;
        Stage      = stage;

        if (card != null) card.SetPreview(masterEmp, grade, stage);

        // card.SetPreview 가 같은 Button 에 리스너를 다시 거므로 그 뒤에 덮어쓴다
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnClicked?.Invoke(this));
        }
    }
}
