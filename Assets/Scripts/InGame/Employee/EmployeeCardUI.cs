using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

public class EmployeeCardUI : MonoBehaviour
{
    public static EmployeeCardUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject cardPanel;

    [Header("UI References")]
    public Image portraitImage;
    public TextMeshProUGUI nameText;
    public Slider satisfactionSlider;
    public TextMeshProUGUI satisfactionText;
    public TextMeshProUGUI planningText;
    public TextMeshProUGUI developText;
    public TextMeshProUGUI artText;
    public TextMeshProUGUI creativityText;

    [Header("Animation")]
    [Tooltip("만족도 슬라이더가 1초당 변하는 단위 수 (값이 클수록 빠름)")]
    public float satisfactionAnimSpeed = 30f;

    private string _currentEmployeeId;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        if (cardPanel != null) cardPanel.SetActive(false);
    }

    public void Show(string employeeId)
    {
        if (string.IsNullOrEmpty(employeeId)) return;
        var emp = EmployeeManager.Instance?.GetEmployee(employeeId);
        if (emp == null) return;
        Show(emp);
    }

    public void Show(EmployeeData emp)
    {
        if (emp == null) return;
        _currentEmployeeId = emp.id;

        // 정적 정보(초상화·이름)는 Show 시점에 한 번만 세팅
        if (portraitImage != null && !string.IsNullOrEmpty(emp.portraitId))
        {
            var sprite = Resources.Load<Sprite>($"Portraits/{emp.portraitId}");
            if (sprite != null) portraitImage.sprite = sprite;
        }
        if (nameText != null) nameText.text = emp.employeeName;
        if (satisfactionSlider != null)
        {
            satisfactionSlider.minValue = 0f;
            satisfactionSlider.maxValue = 100f;
            // 첫 표시 시 슬라이더 값은 즉시 세팅 (애니메이션 시작점)
            satisfactionSlider.value = emp.satisfaction;
        }

        // 동적 수치(능력치 텍스트 등) 즉시 반영
        RefreshDynamic(emp);

        if (cardPanel != null) cardPanel.SetActive(true);
    }

    // 만족도/능력치는 시간 흐름에 따라 변하므로 카드가 열려있는 동안 매 프레임 갱신
    // 슬라이더는 MoveTowards로 자연스럽게 보간, 텍스트는 슬라이더 현재 값과 동기화
    void RefreshDynamic(EmployeeData emp)
    {
        int displayedSatisfaction = emp.satisfaction;
        if (satisfactionSlider != null)
        {
            satisfactionSlider.value = Mathf.MoveTowards(
                satisfactionSlider.value,
                emp.satisfaction,
                satisfactionAnimSpeed * Time.deltaTime
            );
            displayedSatisfaction = Mathf.RoundToInt(satisfactionSlider.value);
            ApplySatisfactionColor(satisfactionSlider, displayedSatisfaction);
        }
        if (satisfactionText != null) satisfactionText.text = $"{displayedSatisfaction}";
        if (planningText     != null) planningText.text     = $"{emp.EffectivePlanningSkill}";
        if (developText      != null) developText.text      = $"{emp.EffectiveDevelopSkill}";
        if (artText          != null) artText.text          = $"{emp.EffectiveArtSkill}";
        if (creativityText   != null) creativityText.text   = $"{emp.EffectiveCreativitySkill}";
    }

    public void Hide()
    {
        _currentEmployeeId = null;
        if (cardPanel != null) cardPanel.SetActive(false);
    }

    public void OnClickClose() => Hide();

    // 카드의 "아이템" 버튼 OnClick에 연결
    // → ItemPanel을 카드 컨텍스트로 열어, 아이템 사용 시 직원 선택 없이 현재 직원에게 즉시 적용
    public void OnClickItemButton()
    {
        if (string.IsNullOrEmpty(_currentEmployeeId)) return;
        if (ItemPanelUI.Instance == null) return;

        string savedEmpId = _currentEmployeeId;

        // 카드 일시 숨김 (close-outside 충돌 방지) — ItemPanel 닫힐 때 콜백으로 다시 표시
        if (cardPanel != null) cardPanel.SetActive(false);

        ItemPanelUI.Instance.OpenForEmployee(savedEmpId, () => Show(savedEmpId));
    }

    // 카드의 "강화하기" 버튼 OnClick에 연결
    // → TrainingUI의 ListPanel 단계를 건너뛰고 현재 직원의 강화 패널을 바로 표시
    public void OnClickEnhanceButton()
    {
        if (string.IsNullOrEmpty(_currentEmployeeId)) return;
        if (TrainingUI.Instance == null) return;

        var emp = EmployeeManager.Instance?.GetEmployee(_currentEmployeeId);
        if (emp == null) return;

        string savedEmpId = _currentEmployeeId;

        if (cardPanel != null) cardPanel.SetActive(false);

        TrainingUI.Instance.OpenTrainingForEmployee(emp, () => Show(savedEmpId));
    }

    // 만족도 능력치 보정 구간(EmployeeData.GetSatisfactionMultiplier)에 맞춰 슬라이더 Fill 색상 변경
    //   81~100  → x1.1  초록 (강화)
    //   61~80   → x1.0  파랑 (보통)
    //   41~60   → x0.9  주황 (약 디버프)
    //   0~40    → x0.8  빨강 (강 디버프)
    static void ApplySatisfactionColor(Slider slider, int satisfaction)
    {
        if (slider == null || slider.fillRect == null) return;
        var fill = slider.fillRect.GetComponent<Image>();
        if (fill == null) return;
        fill.color = GetSatisfactionColor(satisfaction);
    }

    static Color GetSatisfactionColor(int satisfaction)
    {
        if (satisfaction >= 81) return new Color(0.32f, 0.83f, 0.52f); // #52d486 초록
        if (satisfaction >= 61) return new Color(0.36f, 0.62f, 1.00f); // #5b9eff 파랑
        if (satisfaction >= 41) return new Color(0.94f, 0.69f, 0.25f); // #f0b040 주황
        return                       new Color(0.91f, 0.35f, 0.35f);   // #e85a5a 빨강
    }

    // 카드가 열려있는 동안: ① 매 프레임 데이터 갱신 ② 빈 공간/다른 UI 클릭 시 닫음
    private static readonly System.Collections.Generic.List<RaycastResult> _raycastResults = new();
    void Update()
    {
        if (cardPanel == null || !cardPanel.activeSelf) return;

        // ① 데이터 실시간 갱신 (만족도·능력치는 게임 진행 중 변동)
        if (!string.IsNullOrEmpty(_currentEmployeeId))
        {
            var emp = EmployeeManager.Instance?.GetEmployee(_currentEmployeeId);
            if (emp != null) RefreshDynamic(emp);
        }

        // ② 닫기 판정 (마우스/터치 둘 다 지원: Pointer.current 사용)
        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return;

        Vector2 mousePos = pointer.position.ReadValue();

        // 카드 패널 자체(또는 그 자식) 위 클릭만 닫지 않음. 다른 UI는 닫음.
        if (EventSystem.current != null)
        {
            var ped = new PointerEventData(EventSystem.current) { position = mousePos };
            _raycastResults.Clear();
            EventSystem.current.RaycastAll(ped, _raycastResults);
            foreach (var r in _raycastResults)
            {
                if (r.gameObject.transform.IsChildOf(cardPanel.transform))
                    return;
            }
        }

        // 캐릭터 클릭 → 닫지 않음 (해당 캐릭터의 OnPointerClick이 카드 내용을 갱신)
        if (Camera.main != null)
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);
            var hits = Physics2D.OverlapPointAll(new Vector2(worldPos.x, worldPos.y));
            foreach (var h in hits)
            {
                if (h.GetComponentInParent<OfficeCharacter>() != null) return;
            }
        }

        Hide();
    }
}
