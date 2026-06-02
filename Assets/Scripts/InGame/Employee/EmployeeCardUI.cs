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
    public TextMeshProUGUI potentialText;   // "잠재력: {잠재력}"
    public TextMeshProUGUI gradeText;       // 등급 텍스트
    public Image gradePanel;                // 등급색 배경
    public Image roleBadge;                 // 역할 아이콘
    public Sprite[] roleIcons;              // role enum 인덱스 순서 [Planner, Programmer, Artist]
    public TextMeshProUGUI traitText;   // 캐릭터 특성명 (grade >= Epic 일 때만, 아니면 빈 문자열)
    public TextMeshProUGUI eventText;   // 전용 이벤트명 (grade >= Unique 일 때만, 아니면 빈 문자열)
    public TextMeshProUGUI enhancementText;
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
        if (potentialText != null) potentialText.text = $"잠재력: {emp.PotentialToString()}";
        if (gradeText != null) gradeText.text = emp.GradeToString();
        if (roleBadge != null && roleIcons != null
            && (int)emp.role >= 0 && (int)emp.role < roleIcons.Length
            && roleIcons[(int)emp.role] != null)
            roleBadge.sprite = roleIcons[(int)emp.role];
        ApplyGradeColor(emp.grade);
        CharacterTraitApplier.SetupTraitText(traitText, emp);
        CharacterUniqueEvents.SetupEventTextDirect(eventText, emp);
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
        if (enhancementText  != null) enhancementText.text  = $"Lv.{emp.enhancementLevel}";
        // 능력치 = 버프/디버프 적용된 실제값 + 색상(버프 빨강 / 디버프 파랑 / 변화 없음 흰색)
        SetStatColored(planningText,   emp.planningSkill,   emp.EffectivePlanningSkill);
        SetStatColored(developText,    emp.developSkill,    emp.EffectiveDevelopSkill);
        SetStatColored(artText,        emp.artSkill,        emp.EffectiveArtSkill);
        SetStatColored(creativityText, emp.creativitySkill, emp.EffectiveCreativitySkill);
    }

    // 능력치 텍스트에 버프/디버프 적용 실제값 + 색상을 한 번에 세팅 (EmployeeResumePanel 해고 모드와 공유).
    public static void SetStatColored(TextMeshProUGUI label, int baseSkill, int effectiveSkill)
    {
        if (label == null) return;
        label.text  = $"{effectiveSkill}";
        label.color = EmployeeData.GetStatColor(baseSkill, effectiveSkill);
    }

    public void Hide()
    {
        _currentEmployeeId = null;
        if (cardPanel != null) cardPanel.SetActive(false);
    }

    // ── 등급색 (gradePanel) — 슬롯 UI 와 동일 규칙. Normal/Rare/Epic 단색, Unique/Legendary 셰머 ──
    private static readonly Color GradeNormal = new Color(0.92f, 0.92f, 0.92f);
    private static readonly Color GradeRare   = new Color(0.75f, 0.88f, 0.95f);
    private static readonly Color GradeEpic   = new Color(0.55f, 0.30f, 0.85f);

    void ApplyGradeColor(EmployeeGrade grade)
    {
        if (gradePanel == null) return;
        StopAllCoroutines();

        if (grade == EmployeeGrade.Legendary) { StartCoroutine(LegendaryShimmer()); return; }
        if (grade == EmployeeGrade.Unique)    { StartCoroutine(UniqueShimmer());    return; }

        gradePanel.color = grade switch
        {
            EmployeeGrade.Rare => GradeRare,
            EmployeeGrade.Epic => GradeEpic,
            _                  => GradeNormal,
        };
    }

    System.Collections.IEnumerator UniqueShimmer()
    {
        Color goldA = new Color(1.0f, 0.85f, 0.30f);
        Color goldB = new Color(0.85f, 0.65f, 0.10f);
        while (true)
        {
            float t = (Mathf.Sin(Time.time * 2.0f) + 1f) / 2f;
            gradePanel.color = Color.Lerp(goldB, goldA, t);
            yield return null;
        }
    }

    System.Collections.IEnumerator LegendaryShimmer()
    {
        while (true)
        {
            float h = Mathf.Repeat(Time.time * 0.25f, 1f);
            gradePanel.color = Color.HSVToRGB(h, 0.55f, 1f);
            yield return null;
        }
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
    // → 단일 화면 강화 패널(TrainingPanelUI)을 열고 현재 직원을 바로 선택
    public void OnClickEnhanceButton()
    {
        if (string.IsNullOrEmpty(_currentEmployeeId)) return;
        if (TrainingPanelUI.Instance == null) return;

        var emp = EmployeeManager.Instance?.GetEmployee(_currentEmployeeId);
        if (emp == null) return;

        string savedEmpId = _currentEmployeeId;

        if (cardPanel != null) cardPanel.SetActive(false);

        TrainingPanelUI.Instance.OpenForEmployee(emp, () => Show(savedEmpId));
    }

    // 만족도 능력치 보정 구간(EmployeeData.GetSatisfactionMultiplier)에 맞춰 슬라이더 Fill 색상 변경
    //   81~100  → x1.1  초록 (강화)
    //   61~80   → x1.0  파랑 (보통)
    //   41~60   → x0.9  주황 (약 디버프)
    //   0~40    → x0.8  빨강 (강 디버프)
    public static void ApplySatisfactionColor(Slider slider, int satisfaction)
    {
        if (slider == null || slider.fillRect == null) return;
        var fill = slider.fillRect.GetComponent<Image>();
        if (fill == null) return;
        fill.color = GetSatisfactionColor(satisfaction);
    }

    public static Color GetSatisfactionColor(int satisfaction)
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

        // ② 닫기 판정 — Mouse / Touchscreen 명시적 체크
        // (Pointer.current는 시뮬레이터에서 터치 전환 타이밍에 따라 wasPressedThisFrame을 놓치는 케이스가 있음)
        Vector2 mousePos;
        if (!TryGetPressedPointerPosition(out mousePos)) return;

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

    static bool TryGetPressedPointerPosition(out Vector2 position)
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            position = mouse.position.ReadValue();
            return true;
        }
        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
        {
            position = touch.primaryTouch.position.ReadValue();
            return true;
        }
        position = default;
        return false;
    }
}
