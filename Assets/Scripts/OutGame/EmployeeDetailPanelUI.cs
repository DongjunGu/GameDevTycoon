using TMPro;
using UnityEngine;
using UnityEngine.UI;

// EmployeePanel 의 "등급 능력" 상세 패널 (EmployeeDetailPanel)
// EmployeeDetailBtn 클릭 → 갤러리에서 현재 선택된 직원 기준으로 채워서 표시, 확인 버튼으로 닫기.
//
// 패널 자체는 꺼진 상태로 시작하므로 이 컴포넌트는 EmployeePanel(항상 켜져 있는 탭 루트)에 붙인다.
// (패널에 붙이면 비활성 상태라 Start 가 안 돌아 열기 버튼을 못 배선함)
public class EmployeeDetailPanelUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("EmployeeDetailPanel — 열고 닫을 대상")]
    public GameObject panelRoot;
    [Tooltip("PreviewContainer2/EmployeeDetailBtn")]
    public Button openButton;
    [Tooltip("DetailBottomPanel/Button — 확인(닫기)")]
    public Button closeButton;
    [Tooltip("선택 직원을 물어볼 갤러리. 비우면 같은 오브젝트에서 찾음")]
    public OutGameEmployeePanelGallery gallery;

    [Header("Contents")]
    [Tooltip("DetailLeftPanel/ItemPrefabEmployeePanel 의 EmployeePanelItemUI")]
    public EmployeePanelItemUI card;
    [Tooltip("DetailPanelRare/DetailGradeDesText")]
    public TMP_Text rareDescText;
    [Tooltip("DetailPanelEpic/DetailGradeDesText — 캐릭터 특성명")]
    public TMP_Text epicDescText;
    [Tooltip("DetailPanelUnique/DetailGradeDesText — 전용 이벤트명")]
    public TMP_Text uniqueDescText;
    [Tooltip("DetailPanelLegend/DetailGradeDesText")]
    public TMP_Text legendDescText;

    // EmployeeData.GradeIntervalBonus 와 같은 값 — Rare 이상이면 주스탯 +50.
    const int GradeStatBonus = 50;

    void Awake()
    {
        if (gallery == null) gallery = GetComponent<OutGameEmployeePanelGallery>();
    }

    void Start()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveAllListeners();
            openButton.onClick.AddListener(Open);
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // 탭을 다시 열었을 때 상세 패널이 남아있지 않도록
    void OnEnable()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void Open()
    {
        var emp = gallery != null ? gallery.Selected : null;
        if (emp == null) return;

        Apply(emp, gallery.SelectedUnlocked);
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    void Apply(EmployeeData emp, bool unlocked)
    {
        if (card != null) card.SetData(emp, unlocked);

        // 등급별 해금 내용 — 이름만 표시(설명은 기존 인게임 AlertUI 흐름 사용).
        // Any 계열을 쓰는 이유: 갤러리의 직원은 마스터 데이터라 grade 가 Normal 이라서
        // 등급 게이팅된 GetTraitName/GetEventName 은 항상 빈 문자열이 됨.
        string statText = $"{MainStatName(emp.role)} 점수 +{GradeStatBonus}";
        if (rareDescText   != null) rareDescText.text   = statText;
        if (epicDescText   != null) epicDescText.text   = CharacterTraitApplier.GetTraitNameAnyGrade(emp);
        if (uniqueDescText != null) uniqueDescText.text = CharacterUniqueEvents.GetEventNameAnyGrade(emp);
        // ponytail: Legend 전용 효과 명세가 아직 없어 Rare 와 같은 주스탯 보너스를 표시한다.
        // 별도 효과가 정해지면 여기만 교체.
        if (legendDescText != null) legendDescText.text = statText;
    }

    static string MainStatName(EmployeeRole role) => role switch
    {
        EmployeeRole.Programmer => "개발",
        EmployeeRole.Artist     => "아트",
        _                       => "기획",
    };
}
