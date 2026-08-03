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

    [Header("등급별 스프라이트 (공용 GradeSpriteSet 에셋)")]
    [Tooltip("초상화 패널(portraitImagePanel) 에 적용할 등급 프레임 세트")]
    public Image portraitImagePanel;
    public GradeSpriteSet portraitFrameSet;
    [Tooltip("gradePanel-gradeBG 에 적용할 등급BG 세트")]
    public Image gradeBG;
    public GradeSpriteSet gradeBGSet;
    [Header("역할 아이콘 (공용 RoleIconSet 에셋)")]
    public Image roleBadge;                 // 역할 아이콘
    public RoleIconSet roleIconSet;
    public TextMeshProUGUI traitText;   // 캐릭터 특성명 (등급 무관 항상 표시, 미충족이면 lockedPanel)
    public TextMeshProUGUI eventText;   // 전용 이벤트명 (등급 무관 항상 표시, 미충족이면 lockedPanel)
    [Tooltip("특성 등급(Epic) 미충족 시 활성화 — 위에 raycast Image 가 있어 터치 차단")]
    public GameObject traitLockedPanel;
    [Tooltip("이벤트 등급(Unique) 미충족 시 활성화 — 위에 raycast Image 가 있어 터치 차단")]
    public GameObject eventLockedPanel;
    public TextMeshProUGUI enhancementText;
    public Slider satisfactionSlider;
    public TextMeshProUGUI satisfactionText;

    [Header("만족도 Fill 이미지 (구간별 — 색 대신 sprite 교체)")]
    [Tooltip("구간별 Fill sprite 묶음 (공용 SatisfactionFillSet 에셋)")]
    public SatisfactionFillSet satisfactionFillSet;
    [Tooltip("파견중일 때 표시할 badge (옵션)")]
    public GameObject dispatchedBadge;
    public TextMeshProUGUI planningText;
    public TextMeshProUGUI developText;
    public TextMeshProUGUI artText;
    public TextMeshProUGUI creativityText;

    [Header("Stat arrows — 능력치 옆 ArrowImage (버프=기본 / 디버프=Y축 flip / 무변화=숨김)")]
    public Image planningArrow;
    public Image developArrow;
    public Image artArrow;
    public Image creativityArrow;
    [Tooltip("화살표 스프라이트 1개. 버프는 그대로, 디버프는 Y축으로 뒤집어 사용")]
    public Sprite statArrowSprite;

    [Header("Animation")]
    [Tooltip("만족도 슬라이더가 1초당 변하는 단위 수 (값이 클수록 빠름)")]
    public float satisfactionAnimSpeed = 30f;

    [Header("아이템/강화 버튼 — 튜토리얼 18-1 전까지 잠금")]
    [Tooltip("\"아이템\" 버튼 — 18-1 전까지 interactable=false")]
    public Button itemUseBtn;
    [Tooltip("itemUseBtn 위에 덮이는 잠금 표시(터치 차단) — 18-1 전까지 활성")]
    public GameObject itemUseLockBG;
    [Tooltip("\"강화하기\" 버튼 — 18-1 전까지 interactable=false")]
    public Button trainingEmployeeBtn;
    [Tooltip("trainingEmployeeBtn 위에 덮이는 잠금 표시(터치 차단) — 18-1 전까지 활성")]
    public GameObject trainingLockBG;

    [Header("선택 캐릭터 머리 위 화살표 (월드스페이스, 카드와 동시에 표시)")]
    [Tooltip("SpriteRenderer가 붙은 월드스페이스 오브젝트. 카드가 열릴 때 선택된 캐릭터 머리 위(12px)로 이동해 표시")]
    public Transform selectedArrow;
    const float SelectedArrowHeadOffsetUnits = 12f / 32f; // 12px, spritePixelsToUnits=32 기준

    private string _currentEmployeeId;

    // 카드가 특정 직원으로 열릴 때마다 발동 — 튜토리얼이 "이 직원의 카드가 열렸는지"를 기다릴 때 사용.
    public event System.Action<string> OnCardShown;

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
            var sprite = Resources.Load<Sprite>($"Portraits/Mini/{emp.portraitId}");
            if (sprite != null) portraitImage.sprite = sprite;
        }
        if (nameText != null) nameText.text = emp.employeeName;
        if (potentialText != null) potentialText.text = $"{emp.PotentialToString()}";
        if (gradeText != null) gradeText.text = emp.GradeToString();
        RoleIconSet.Apply(roleBadge, roleIconSet, emp.role);
        ApplyGradeColor(emp.grade);
        SetupTraitWithLock(emp);
        SetupEventWithLock(emp);
        if (satisfactionSlider != null)
        {
            satisfactionSlider.minValue = 0f;
            satisfactionSlider.maxValue = 100f;
            // 첫 표시 시 슬라이더 값은 즉시 세팅 (애니메이션 시작점)
            satisfactionSlider.value = emp.satisfaction;
        }

        // 파견중 badge
        if (dispatchedBadge != null)
            dispatchedBadge.SetActive(DispatchManager.Instance != null && DispatchManager.Instance.IsDispatched(emp.id));

        // 동적 수치(능력치 텍스트 등) 즉시 반영
        RefreshDynamic(emp);
        ApplyItemTrainingLock();

        if (cardPanel != null) cardPanel.SetActive(true);
        PositionSelectedArrow(emp.id);
        OnCardShown?.Invoke(emp.id);
    }

    // 아이템/강화 버튼 — 튜토리얼 17-8 잠금 해제 신호 전까지 interactable=false + LockBG 활성.
    // public — 온보딩 튜토리얼(17-8)이 MarkTutorial17_8UnlockDone() 직후 이미 열려있는 카드의 잠금
    // 상태를 즉시 재적용시킬 때 호출한다(Show()를 다시 타지 않고 잠금 UI만 갱신).
    public void ApplyItemTrainingLock()
    {
        bool locked = !OnboardingState.Tutorial17_8UnlockDone;

        if (itemUseBtn != null) itemUseBtn.interactable = !locked;
        if (itemUseLockBG != null) itemUseLockBG.SetActive(locked);

        if (trainingEmployeeBtn != null) trainingEmployeeBtn.interactable = !locked;
        if (trainingLockBG != null) trainingLockBG.SetActive(locked);
    }

    // 카드와 함께 뜨는 선택 화살표 — 캐릭터 머리 위(statPopupAnchor 없으면 0.6유닛 위) 기준 12px 추가로 위에 표시.
    void PositionSelectedArrow(string employeeId)
    {
        if (selectedArrow == null) return;
        var oc = OfficeManager.Instance?.GetCharacter(employeeId);
        if (oc == null) { selectedArrow.gameObject.SetActive(false); return; }

        Vector3 headPos = oc.statPopupAnchor != null
            ? oc.statPopupAnchor.position
            : oc.transform.position + Vector3.up * 1f;
        Vector3 pos = headPos + Vector3.up * SelectedArrowHeadOffsetUnits;

        // 둥실 애니메이션(FloatBob)이 있으면 기준점까지 같이 갱신 — 없으면 단순 위치만 세팅
        var bob = selectedArrow.GetComponent<FloatBob>();
        if (bob != null) bob.SetBasePosition(pos);
        else selectedArrow.position = pos;

        selectedArrow.gameObject.SetActive(true);
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
            SatisfactionFillSet.Apply(satisfactionSlider, satisfactionFillSet, displayedSatisfaction);
        }
        if (satisfactionText != null) satisfactionText.text = $"{displayedSatisfaction}/100";
        if (enhancementText  != null) enhancementText.text  = $"Lv.{emp.enhancementLevel}";
        // 능력치 = 버프/디버프 적용된 실제값 + 색상(버프 빨강 / 디버프 파랑 / 변화 없음 흰색)
        SetStatColored(planningText,   emp.planningSkill,   emp.EffectivePlanningSkill);
        SetStatColored(developText,    emp.developSkill,    emp.EffectiveDevelopSkill);
        SetStatColored(artText,        emp.artSkill,        emp.EffectiveArtSkill);
        SetStatColored(creativityText, emp.creativitySkill, emp.EffectiveCreativitySkill);

        // 수치 옆 화살표 — 수치 색상과 동일 기준 (상승=red / 하락=blue / 무변화=숨김)
        SetStatArrow(planningArrow,   emp.planningSkill,   emp.EffectivePlanningSkill);
        SetStatArrow(developArrow,    emp.developSkill,    emp.EffectiveDevelopSkill);
        SetStatArrow(artArrow,        emp.artSkill,        emp.EffectiveArtSkill);
        SetStatArrow(creativityArrow, emp.creativitySkill, emp.EffectiveCreativitySkill);
    }

    // 무변화(노말) 상태 outline 색 — #663D45. EmployeeListUI/DispatchPanelUI도 이 메서드를 그대로 호출해 공유.
    static readonly Color StatNormalColor = new Color(0.400f, 0.239f, 0.271f); // #663D45

    // 능력치 텍스트에 버프/디버프 적용 실제값 + 색상을 한 번에 세팅 (EmployeeResumePanel 해고 모드와 공유).
    public static void SetStatColored(TextMeshProUGUI label, int baseSkill, int effectiveSkill)
    {
        if (label == null) return;
        label.text  = $"{effectiveSkill}";
        // 면색은 흰색으로 통일, 버프/디버프 구분은 outline 색으로 (버프 #E63356 / 디버프 #517FFF / 무변화 #663D45)
        label.color = Color.white;
        Color outline = effectiveSkill == baseSkill
            ? StatNormalColor
            : EmployeeData.GetStatColor(baseSkill, effectiveSkill);
        label.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, outline);
    }

    // 능력치 변화 방향에 따라 화살표 표시. 스프라이트는 1개 — 버프는 그대로, 디버프는 Y축 flip. 무변화면 숨김.
    void SetStatArrow(Image arrow, int baseSkill, int effectiveSkill)
    {
        if (arrow == null) return;
        if (effectiveSkill == baseSkill) { arrow.enabled = false; return; }

        bool debuff = effectiveSkill < baseSkill;
        arrow.sprite  = statArrowSprite;
        arrow.color   = EmployeeData.GetStatColor(baseSkill, effectiveSkill); // 수치 색상과 동일 (버프 #E63356 / 디버프 #517FFF)
        arrow.enabled = true;

        // Y축 flip (디버프) / 원복 (버프) — x·z 는 유지
        var rt = arrow.rectTransform;
        var s  = rt.localScale;
        float magY = Mathf.Abs(s.y);
        rt.localScale = new Vector3(s.x, debuff ? -magY : magY, s.z);
    }

    public void Hide()
    {
        _currentEmployeeId = null;
        if (cardPanel != null) cardPanel.SetActive(false);
        if (selectedArrow != null) selectedArrow.gameObject.SetActive(false);
    }

    // 특성 — 등급 무관 이름 표시. 등급 충족이면 클릭 시 설명, 미충족이면 lockedPanel 활성화(터치 차단).
    void SetupTraitWithLock(EmployeeData emp)
    {
        string name = CharacterTraitApplier.GetTraitNameAnyGrade(emp);
        bool unlocked = CharacterTraitApplier.IsTraitUnlocked(emp);
        bool hasTrait = !string.IsNullOrEmpty(name);

        if (traitText != null)
        {
            traitText.text = hasTrait ? $"특성 : {name}" : "";
            var btn = traitText.GetComponent<TraitDescriptionButton>();
            if (hasTrait && unlocked)
            {
                if (btn == null) btn = traitText.gameObject.AddComponent<TraitDescriptionButton>();
                btn.Bind(emp);
                traitText.raycastTarget = true;
            }
            else
            {
                if (btn != null) btn.Bind(null);
                traitText.raycastTarget = false; // 미충족/미보유 → 클릭(설명) 비활성
            }
        }

        // 특성이 있는데 등급 미충족일 때만 잠금 패널 노출 (미보유는 잠글 게 없음)
        if (traitLockedPanel != null) traitLockedPanel.SetActive(hasTrait && !unlocked);
    }

    // 전용 이벤트 — 등급 무관 이름 표시. 등급 충족이면 클릭 시 설명, 미충족이면 lockedPanel 활성화(터치 차단).
    void SetupEventWithLock(EmployeeData emp)
    {
        string name = CharacterUniqueEvents.GetEventNameAnyGrade(emp);
        bool unlocked = CharacterUniqueEvents.IsEventUnlocked(emp);
        bool hasEvent = !string.IsNullOrEmpty(name);

        if (eventText != null)
        {
            eventText.text = hasEvent ? $"이벤트 : {name}" : "";
            var btn = eventText.GetComponent<EventDescriptionButton>();
            if (hasEvent && unlocked)
            {
                if (btn == null) btn = eventText.gameObject.AddComponent<EventDescriptionButton>();
                btn.Bind(emp);
                eventText.raycastTarget = true;
            }
            else
            {
                if (btn != null) btn.Bind(null);
                eventText.raycastTarget = false;
            }
        }

        if (eventLockedPanel != null) eventLockedPanel.SetActive(hasEvent && !unlocked);
    }

    // ── 등급색 (gradePanel) — 슬롯 UI 와 동일 규칙. Normal/Rare/Epic 단색, Unique/Legendary 셰머 ──
    private static readonly Color GradeNormal = new Color(0.92f, 0.92f, 0.92f);
    private static readonly Color GradeRare   = new Color(0.75f, 0.88f, 0.95f);
    private static readonly Color GradeEpic   = new Color(0.55f, 0.30f, 0.85f);

    void ApplyGradeColor(EmployeeGrade grade)
    {
        // 등급별 스프라이트 적용 (공용 SO) — 초상화 프레임 / gradeBG
        GradeSpriteSet.Apply(portraitImagePanel, portraitFrameSet, grade);
        GradeSpriteSet.Apply(gradeBG,            gradeBGSet,       grade);

        if (gradePanel == null) return;
        StopAllCoroutines();

        if (grade == EmployeeGrade.Legendary) { StartCoroutine(LegendaryShimmer()); return; }
        if (grade == EmployeeGrade.Unique)    { gradePanel.color = new Color(1f, 0.85f, 0.30f); return; }

        gradePanel.color = grade switch
        {
            EmployeeGrade.Rare => GradeRare,
            EmployeeGrade.Epic => GradeEpic,
            _                  => GradeNormal,
        };
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
        if (DispatchManager.Instance != null && DispatchManager.Instance.IsDispatched(_currentEmployeeId))
        {
            AlertUI.Instance?.Show("파견중인 직원에게는 사용할 수 없습니다.");
            return;
        }

        string savedEmpId = _currentEmployeeId;

        // 카드 일시 숨김 (close-outside 충돌 방지) — ItemPanel 닫힐 때 콜백으로 다시 표시
        if (cardPanel != null) cardPanel.SetActive(false);
        if (selectedArrow != null) selectedArrow.gameObject.SetActive(false);

        ItemPanelUI.Instance.OpenForEmployee(savedEmpId, () => Show(savedEmpId));
    }

    // 카드의 "강화하기" 버튼 OnClick에 연결
    // → 직원관리 패널(EmployeeListUI)을 열고 현재 직원을 선택 + TrainingPanel 표시
    public void OnClickEnhanceButton()
    {
        if (string.IsNullOrEmpty(_currentEmployeeId)) return;
        if (EmployeeListUI.Instance == null) return;
        if (DispatchManager.Instance != null && DispatchManager.Instance.IsDispatched(_currentEmployeeId))
        {
            AlertUI.Instance?.Show("파견중인 직원은 강화할 수 없습니다.");
            return;
        }

        var emp = EmployeeManager.Instance?.GetEmployee(_currentEmployeeId);
        if (emp == null) return;

        if (cardPanel != null) cardPanel.SetActive(false);
        if (selectedArrow != null) selectedArrow.gameObject.SetActive(false);

        EmployeeListUI.Instance.OpenForEnhance(emp);
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

            // 카드가 열려있는 동안 캐릭터가 이동(패트롤/파견 등)할 수 있어 화살표 위치도 매 프레임 재동기화.
            // Show() 시점에만 한 번 세팅하면 캐릭터가 움직인 뒤 화살표만 원래 자리에 남아 분리돼 보임.
            PositionSelectedArrow(_currentEmployeeId);
        }

        // ② 닫기 판정 — Mouse / Touchscreen 명시적 체크
        // 다이얼로그/랜덤이벤트 선택지/AlertUI 등은 대부분 GameTimeManager.StopTime()으로 열려있는 동안
        // 시간을 멈춘다 — 그 UI들을 조작하는 클릭에 카드가 같이 닫혀버리지 않도록, 시간이 멈춰있는 동안은
        // 바깥 클릭 닫기 판정 자체를 건너뛴다. ⚠️ IsRunning이 아니라 IsRunningForMovement로 판단 —
        // 온보딩 18-4~20처럼 실제 모달 없이 의도적으로 시간만 멈춰있는 구간(AllowMovementWhileStopped)엔
        // 바깥 클릭 닫기가 정상 동작해야 한다.
        if (GameTimeManager.Instance != null && !GameTimeManager.Instance.IsRunningForMovement) return;

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
