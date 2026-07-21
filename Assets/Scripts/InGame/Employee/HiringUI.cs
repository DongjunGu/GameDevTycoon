using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using DG.Tweening;

public class HiringUI : MonoBehaviour
{
    public static HiringUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject tierPanel;       // 티어 선택 패널 (새로 추가)
    public GameObject hiringPanel;
    public GameObject confirmPanel;
    public GameObject loadingPanel;

    [Header("Tier Buttons")]
    [Tooltip("티어 버튼 GameObject 3개를 순서대로 인스펙터에 할당 (1단계/2단계/3단계). 2/3단계는 hire_tier2/3 해금 시 노출.")]
    public GameObject[] tierButtons;
    [Tooltip("TierPanel/confirmBtn — 선택된 티어로 채용을 확정하는 버튼")]
    public Button tierConfirmButton;

    [Header("Refresh")]
    [Tooltip("후보 리스트(HiringPanel) 새로고침 버튼. 'hire_refresh' 해금 시 활성, 같은 세션 1회만 사용 가능.")]
    public Button refreshButton;
    [Tooltip("ConfirmHirePanel 새로고침 버튼. refreshButton 과 같은 1회 가드를 공유. 누르면 이력서 셔플 후 후보 재추첨(확정 화면 유지).")]
    public Button confirmRefreshButton;
    [Tooltip("'hire_refresh' 미해금 시 활성화되는 잠금 덮개 (refreshButton 용). 해금되면 자동 비활성.")]
    public GameObject refreshLockedPanel;
    [Tooltip("'hire_refresh' 미해금 시 활성화되는 잠금 덮개 (confirmRefreshButton 용). 해금되면 자동 비활성.")]
    public GameObject confirmRefreshLockedPanel;

    [Header("Slots")]
    public Transform slotParent;
    public GameObject employeeSlotPrefab;

    [Header("Confirm")]
    public TextMeshProUGUI confirmNameText;
    public TextMeshProUGUI enhancementText;
    public TextMeshProUGUI confirmRoleText;
    public TextMeshProUGUI confirmGradeText;
    public TextMeshProUGUI confirmTraitText;   // 캐릭터 특성명 (grade >= Epic 일 때만, 아니면 빈 문자열)
    public TextMeshProUGUI confirmPotentialText;
    public TextMeshProUGUI confirmDevelopText;
    public TextMeshProUGUI confirmPlanningText;
    public TextMeshProUGUI confirmArtText;
    public TextMeshProUGUI confirmCreativityText;
    public TextMeshProUGUI confirmSalaryText;
    public TextMeshProUGUI confirmSatisfactionText;
    public TextMeshProUGUI confirmHireCostText;
    [Header("Exist Employee")]
    [Tooltip("ConfirmHirePanel/ExistEmployeePanel — 이력서 후보와 동일한 직원을 이미 보유 중일 때 표시(초상화+능력치)")]
    public ExistEmployeePanelUI existEmployeePanel;

    [Header("Comparison")]
    public GameObject comparisonSection;        // 보유 직원 비교 섹션 (없으면 숨김)
    public TextMeshProUGUI ownedNameText;
    public TextMeshProUGUI ownedRoleText;
    public TextMeshProUGUI ownedGradeText;
    public TextMeshProUGUI ownedTraitText;   // 캐릭터 특성명 (grade >= Epic 일 때만, 아니면 빈 문자열)
    public TextMeshProUGUI ownedPotentialText;
    public TextMeshProUGUI ownedDevelopText;
    public TextMeshProUGUI ownedPlanningText;
    public TextMeshProUGUI ownedArtText;
    public TextMeshProUGUI ownedCreativityText;
    public TextMeshProUGUI ownedSalaryText;
    public TextMeshProUGUI ownedEnhancementText;
    public Button confirmButton;                // 채용하기 버튼
    public Button keepButton;                   // 유지하기 버튼
    [Tooltip("EmployeeResumePanel/ERSalaryPanel/StampImage — 채용하기 클릭 시 scale 0→1 + 알파 0→255 로 0.5초간 등장하는 도장 연출")]
    public Image hireStampImage;

    [Header("Resume Browse (NewEmployeeSlot)")]
    [Tooltip("이력서(NewEmployeeSlot)에 부착된 페이지 넘김 컴포넌트")]
    public ResumeFlipper resumeFlipper;
    public Button prevCandidateButton;          // 왼쪽 화살표 (이전 후보)
    public Button nextCandidateButton;          // 오른쪽 화살표 (다음 후보)
    [Tooltip("CountPanel 의 countText — '현재 후보 순서/전체 후보 수' (예: 1/4) 표시. 넘길 때마다 갱신.")]
    public TextMeshProUGUI countText;

    [Header("Resume Panels (EmployeeResumePanel x3 — 캐러셀 슬롯)")]
    [Tooltip("가운데(현재 후보) 패널 — ResumeFlipper 가 붙은 그 패널")]
    public EmployeeResumePanel resumeCenterPanel;
    [Tooltip("왼쪽(이전 후보) 패널")]
    public EmployeeResumePanel resumeLeftPanel;
    [Tooltip("오른쪽(다음 후보) 패널")]
    public EmployeeResumePanel resumeRightPanel;

    [Header("Settings")]
    public int candidateCount = 4;
    const int INTERVIEW_WEEKS = 3; // 채용 클릭 → 후보 리스트 공개까지 대기 주차
    // [임시테스트] true면 "면접 확정" 멘트는 그대로 뜨되, 확인 후 실제 대기(주 단위) 없이 짧은 테스트
    // 딜레이(TEST_INTERVIEW_DELAY_SECONDS)만 지나면 바로 후보 공개. 온보딩 첫 채용 1주 단축(FirstHireDone)은
    // 이 플래그와 무관하게 항상 살아있는 별도 로직이라 false로 되돌려도 정상 동작한다.
    public static bool InstantInterview = true;

    private EmployeeData _selectedEmployee;
    private EmployeeData _conflictingOwned;     // 동일 masterEmployeeId 보유 직원
    private List<EmployeeData> _currentCandidates = new();
    private int _hireCost;
    private int _currentIndex = -1;                 // _currentCandidates 내 현재 표시 중인 후보
    private readonly List<int> _hireCosts = new();  // 후보별 채용 비용 캐시 — 화살표로 넘길 때 재추첨 방지

    private int  _currentTierIndex = -1;        // hire_refresh 재로드용 — 마지막 OnClickTier 의 티어
    private int  _selectedTierIndex = -1;       // 선택 표시용 — RefreshTierButtonVisibility 에서 초기화
    private readonly Dictionary<int, bool>   _tierUnlocked     = new(); // SetTierButtonState 에서 기록
    // tierButtons[i]의 부모(tierNOutlinePanel) — Start()에서 1회만 캐싱. 실시간으로 transform.parent를
    // 다시 찾으면 안 되는 이유: GlobalButtonClickBounce가 "첫 클릭" 시 버튼과 원래 부모 사이에
    // __ClickBounceWrapper를 끼워넣어 버튼을 그 안으로 재배치하기 때문에, 클릭이 한 번이라도 일어난
    // 뒤에는 tierButtons[i].transform.parent가 tierNOutlinePanel이 아니라 그 래퍼가 되어버린다.
    private Transform[] _tierOutlinePanels;
    private ButtonGlossSweep _tierConfirmGlossSweep; // tierConfirmButton과 같은 GameObject — Start()에서 캐싱
    private bool _refreshUsed      = false;     // 같은 세션 1회 가드
    private bool _candidateFlowActive = false;  // 채용 공개~리스트 종료 동안 시간 정지 유지 + ModalGate 점유

    // 티어 데이터 (강화 레벨 범위/가중치, 잠재력 확률은 EmployeeManager.PotentialWeightTable 참조)
    private static readonly (string label, int cost, int[] range, int[] weights)[] Tiers =
    {
        ("채용 1단계", 2000,  new[] { 0 }, new[] { 1 }),
        ("채용 2단계", 7000,  new[] { 0, 11 }, new[] { 1, 1 }),
        ("채용 3단계", 20000, new[] { 12, 14 }, new[] { 1, 1 }),
    };

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (refreshButton != null)
            refreshButton.onClick.AddListener(OnClickRefresh);
        if (confirmRefreshButton != null)
            confirmRefreshButton.onClick.AddListener(OnClickRefreshFromConfirm);
        if (prevCandidateButton != null)
            prevCandidateButton.onClick.AddListener(OnClickPrevCandidate);
        if (nextCandidateButton != null)
            nextCandidateButton.onClick.AddListener(OnClickNextCandidate);
        if (tierConfirmButton != null)
        {
            tierConfirmButton.onClick.AddListener(OnClickTierConfirm);
            _tierConfirmGlossSweep = tierConfirmButton.GetComponent<ButtonGlossSweep>();
            if (_tierConfirmGlossSweep != null) _tierConfirmGlossSweep.enabled = false;
        }

        // GlobalButtonClickBounce가 클릭 시 부모를 바꿔치기하기 전, 아직 안전한 시점에 미리 캐싱.
        if (tierButtons != null)
        {
            _tierOutlinePanels = new Transform[tierButtons.Length];
            for (int i = 0; i < tierButtons.Length; i++)
                _tierOutlinePanels[i] = tierButtons[i] != null ? tierButtons[i].transform.parent : null;
        }
    }

    // PopulateConfirm 시점의 1회성 동기화만으로는 부족했다 — ConfirmHirePanel 이 처음 열릴 때 ModalLayer 가
    // 카드(EmployeeResumePanel)의 sortingOrder 를 아직 할당하기 전이라, 그 순간 읽은 값+1로 고정해버리면
    // 이후 ResumeFlipper.LateUpdate(RestoreSorting) 가 카드 order 를 최종값으로 올려도 ExistEmployeePanel 은
    // 낡은 값에 머물러 뒤로 깔린다(화살표로 한 번 넘겨 PopulateConfirm 이 재호출돼야만 정상화되던 원인).
    // ResumeFlipper 와 동일하게 매 프레임 재적용해 항상 카드보다 한 단계 위를 유지한다.
    void LateUpdate()
    {
        if (existEmployeePanel != null && existEmployeePanel.gameObject.activeInHierarchy)
            SyncExistEmployeePanelSorting();
    }

    // 선택("Selected") 스프라이트는 Button.transition = SpriteSwap(Inspector 설정)이 EventSystem 선택 여부에
    // 따라 자동으로 처리한다 — 코드에서 img.sprite 를 수동으로 건드리지 않는다.
    // 선택 표시는 부모 "tierNOutlinePanel"의 Image + Outline 컴포넌트 enabled 토글로만, 그리고 오직
    // 여기서만 건드린다 — 선택된 티어만 켜지고 나머지(및 pressed/highlighted 등 다른 Button 상태)는
    // 항상 꺼진 채로 유지된다. Image 를 같이 꺼주는 이유: Outline(BaseMeshEffect)은 같은 Graphic이
    // 생성한 메시를 복제해서 그리는 방식이라, Image 가 꺼져 메시 자체가 안 만들어지면 Outline 도
    // 그릴 게 없어 아무 효과가 없다 — 둘은 사실상 같이 켜져야 하는 한 쌍.
    // tierNOutlinePanel(무채색 Rectangle137 스프라이트 + LayoutElement — 실제 레이아웃 슬롯을 이
    // 부모가 담당하고 tier 버튼은 그 안에 풀스트레치로 들어감)에 두는 이유: tier2/3 처럼 배경이
    // 그라데이션(노랑/파랑)인 버튼에 직접 Outline을 붙이면 원본 텍스처를 그대로 복제해 색이
    // 섞여버리는 문제(테두리색이 effectColor 그대로 안 나옴)가 있어서, 무채색 부모 쪽에 Image+Outline을
    // 두고 tier 버튼(실제 배경/텍스트/아이콘)은 그 앞(자식)에 그려지게 분리했다.
    // ⚠ go.transform.parent 를 실시간으로 다시 찾으면 안 됨 — Start()에서 캐싱한 _tierOutlinePanels 사용
    // (GlobalButtonClickBounce 가 첫 클릭 시 부모를 __ClickBounceWrapper 로 바꿔치기하는 문제, 위 필드 주석 참고).
    void SetTierSelected(int selectedIndex)
    {
        _selectedTierIndex = selectedIndex;
        if (tierButtons == null || _tierOutlinePanels == null) return;
        for (int i = 0; i < tierButtons.Length; i++)
            SetOutlinePanelVisible(i, i == selectedIndex);

        // EventSystem 선택을 옮겨야 Button 의 SpriteSwap 이 selectedSprite 로 자동 전환된다.
        if (selectedIndex >= 0 && selectedIndex < tierButtons.Length
            && tierButtons[selectedIndex] != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(tierButtons[selectedIndex]);

        // confirmBtn은 Transition=SpriteSwap(disabledSprite=기본 회색 이미지 기 지정) — interactable만
        // 토글하면 티어 미선택(-1)일 때 자동으로 disabledSprite, 선택 시 원래(활성) 이미지로 전환된다.
        if (tierConfirmButton != null)
            tierConfirmButton.interactable = selectedIndex >= 0;
        if (_tierConfirmGlossSweep != null)
            _tierConfirmGlossSweep.enabled = selectedIndex >= 0;
    }

    // TierPressRelay(각 tier 버튼에 부착)가 포인터 down/up 시 호출 — 눌려있는 동안은 선택 여부와
    // 무관하게 강제로 꺼지고, 떼면 현재 선택 상태(_selectedTierIndex)로 되돌아간다.
    public void SetTierPressed(int index, bool pressed)
    {
        bool visible = pressed ? false : (index == _selectedTierIndex);
        SetOutlinePanelVisible(index, visible);
    }

    void SetOutlinePanelVisible(int index, bool visible)
    {
        if (_tierOutlinePanels == null || index < 0 || index >= _tierOutlinePanels.Length) return;
        var outlinePanel = _tierOutlinePanels[index];
        if (outlinePanel == null) return;

        var image = outlinePanel.GetComponent<Image>();
        if (image != null) image.enabled = visible;
        var outline = outlinePanel.GetComponent<Outline>();
        if (outline != null) outline.enabled = visible;
    }

    // ── 티어 선택 패널 열기 ───────────────────
    public void OpenHiring()
    {
        GameTimeManager.Instance?.StopTime();
        gameObject.SetActive(true);
        tierPanel.SetActive(true);
        hiringPanel.SetActive(false);
        confirmPanel.SetActive(false);
        RefreshTierButtonVisibility();
        RefreshTierPrices();
    }

    // 각 티어 버튼의 자식 "priceText"(TMP)에 가격 표시. hire_discount 해금 시 할인가(20% 감소) 반영.
    void RefreshTierPrices()
    {
        if (tierButtons == null) return;
        bool discounted = TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("hire_discount");
        for (int i = 0; i < tierButtons.Length && i < Tiers.Length; i++)
        {
            if (tierButtons[i] == null) continue;
            var priceText = FindChildText(tierButtons[i].transform, "priceText");
            if (priceText == null) continue;
            int cost = discounted ? Mathf.RoundToInt(Tiers[i].cost * 0.8f) : Tiers[i].cost;
            priceText.text = $"{cost:N0}G";
        }
    }

    static TextMeshProUGUI FindChildText(Transform root, string childName)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == childName) return t.GetComponent<TextMeshProUGUI>();
        return null;
    }

    // 모든 티어 버튼을 항상 표시. 해금 안 된 티어(2/3단계)는 interactable=false(자동 Disabled 스프라이트)
    // + lockImage 활성화로 잠금 표시. 1단계는 항상 해금.
    void RefreshTierButtonVisibility()
    {
        if (tierButtons == null) return;
        bool tier2Unlocked = TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("hire_tier2");
        bool tier3Unlocked = TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("hire_tier3");

        SetTierButtonState(0, true);
        SetTierButtonState(1, tier2Unlocked);
        SetTierButtonState(2, tier3Unlocked);
        SetTierSelected(-1); // 패널을 새로 열 때 선택 없음 — 해금된 티어(tier1 포함)는 전부 alpha 0, 사용자가 직접 눌러야 선택됨
    }

    // 티어 버튼: interactable 토글(잠기면 false → 버튼 Disabled 스프라이트로 자동 전환)
    // + 자식 "lockIcon"/"lockImage" 활성화/비활성화 (해금 안 됐을 때 활성, 해금되면 비활성 — 둘 다 동일
    // 조건. 1단계는 항상 해금이라 이 둘이 아예 없음 — null 무시).
    void SetTierButtonState(int index, bool unlocked)
    {
        _tierUnlocked[index] = unlocked;
        if (tierButtons == null || index >= tierButtons.Length || tierButtons[index] == null) return;
        var go = tierButtons[index];
        go.SetActive(true);

        var btn = go.GetComponent<Button>();
        if (btn != null) btn.interactable = unlocked;

        var lockTf = go.transform.Find("lockIcon");
        if (lockTf != null) lockTf.gameObject.SetActive(!unlocked);

        var lockImageTf = go.transform.Find("lockImage");
        if (lockImageTf != null) lockImageTf.gameObject.SetActive(!unlocked);
    }

    // 티어 버튼 클릭 (0~2) — 이제 선택 표시만 하고 채용 진행은 안 함(확정은 confirmBtn/OnClickTierConfirm).
    public void OnClickTier(int tierIndex)
    {
        SetTierSelected(tierIndex);
    }

    // TierPanel/confirmBtn 클릭 — 선택된 티어로 채용 진행.
    public void OnClickTierConfirm()
    {
        if (_selectedTierIndex < 0)
        {
            AlertUI.Instance.Show("채용 단계를 선택해주세요.");
            return;
        }
        ConfirmTierHire(_selectedTierIndex);
    }

    // 선택된 티어로 실제 채용 요청 진행 (구 OnClickTier 본체).
    void ConfirmTierHire(int tierIndex)
    {
        // 안전망 — 해금 안 된 티어가 다른 경로로 호출되는 케이스 차단
        if (tierIndex == 1 && (TechTreeManager.Instance == null || !TechTreeManager.Instance.IsUnlocked("hire_tier2")))
        {
            AlertUI.Instance.Show("채용 2단계가 해금되지 않았습니다.");
            return;
        }
        if (tierIndex == 2 && (TechTreeManager.Instance == null || !TechTreeManager.Instance.IsUnlocked("hire_tier3")))
        {
            AlertUI.Instance.Show("채용 3단계가 해금되지 않았습니다.");
            return;
        }

        // 정원 초과라도 모집공고/채용 자체는 진행 가능 — 실제 최대인원 체크는 최종 확정(OnClickConfirmHire)에서
        // "교체가 아닌 신규 채용인지"까지 따져서 처리한다 (교체면 인원수 그대로라 예외).

        // 이미 진행 중인 모집공고가 있으면 차단
        if (EmployeeManager.Instance.HiringPendingTier >= 0)
        {
            AlertUI.Instance.Show("이미 모집공고가 등록되었습니다.");
            return;
        }

        // 비용 계산 (hire_discount 반영) — 클릭 즉시 차감
        bool discounted = TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("hire_discount");
        int cost = discounted ? Mathf.RoundToInt(Tiers[tierIndex].cost * 0.8f) : Tiers[tierIndex].cost;
        if (!MoneyManager.Instance.CanAfford(cost))
        {
            GameUIHelper.ShowLoanPrompt();
            return;
        }
        MoneyManager.Instance.SpendGold(cost);

        // pending 등록(INTERVIEW_WEEKS 주 후 리스트) + 즉시 저장 — 돈만 쓰고 리스트 못 받는 일 방지(복원 가능)
        // 온보딩 첫 채용은 1회만 1주로 단축(나머지는 3주)
        int interviewWeeks = INTERVIEW_WEEKS;
        if (!OnboardingState.FirstHireDone)
        {
            interviewWeeks = 1;
            OnboardingState.MarkFirstHireDone();
        }
        // [테스트] 즉시 공개 모드 — weeks=0 으로 두면 OnWeekPassed(>0 조건)가 자동 reveal 하지 않음 → 중복 방지
        if (InstantInterview) interviewWeeks = 0;
        EmployeeManager.Instance.SetHiringPending(tierIndex, interviewWeeks);
        GameTimeManager.Instance?.SaveGameTime();

        // 채용 UI 닫고 "면접 확정" 비서 안내 → 확인 시 시간 재개(OpenHiring 의 StopTime 해소). 이후 게임 진행하며 INTERVIEW_WEEKS 주 경과.
        tierPanel.SetActive(false);

        // [테스트] InstantInterview 여도 "면접 확정" 멘트는 그대로 보여주고, 확인 시점에 대기 없이(짧은 테스트
        // 딜레이만) 바로 후보 공개로 이어간다. 아니면(실서비스) 그냥 시간만 재개 — 실제 INTERVIEW_WEEKS 주 경과 후
        // OnWeekPassed 가 RevealHiring 을 호출한다.
        // (이 StartTime 을 빼면 OpenHiring 의 StopTime 이 안 풀려 채용 후 시간이 영영 멈춤)
        ShowSecretaryEvent("면접 확정 후 알려드리겠습니다.", () =>
        {
            GameTimeManager.Instance?.StartTime();
            if (InstantInterview)
                StartCoroutine(RevealHiringAfterDelay(tierIndex, TEST_INTERVIEW_DELAY_SECONDS));
        });
    }

    // [임시테스트] InstantInterview 켰을 때 즉시(0초) 대신 짧게 대기 후 공개 — 실제 대기 흐름 테스트용.
    const float TEST_INTERVIEW_DELAY_SECONDS = 2f;
    System.Collections.IEnumerator RevealHiringAfterDelay(int tierIndex, float delay)
    {
        yield return new WaitForSeconds(delay);
        RevealHiring(tierIndex);
    }

    // 면접 대기 만료(EmployeeManager.OnWeekPassed) 또는 재접속 복원 시 호출 — "최종 리스트" 안내 후 후보 리스트 표시.
    public void RevealHiring(int tierIndex)
    {
        BeginCandidateFlow(); // 시간 정지 + 흐름 중 외부 시간재개 방어 가드(퇴사 ForceStartTime 등)

        // 공개 시 1회만 초기(A)·리롤(B) 리스트를 확정·저장 → 재접속해도 동일 리스트(악용 방지).
        // 이미 확정돼 있으면(재접속 복원) 그대로 재사용.
        if (!EmployeeManager.Instance.HasHiringLists)
        {
            var listA = GenerateCandidateList(tierIndex);
            var listB = GenerateCandidateList(tierIndex);
            EmployeeManager.Instance.SetHiringLists(listA, listB);
            GameTimeManager.Instance?.SaveGameTime(); // 리스트 영속화 (pending 은 유지)
        }

        ShowSecretaryEvent("최종 리스트 전달드리겠습니다.", () => OpenCandidateList(tierIndex));
    }

    // 채용 공개 흐름 시작 — 시간 정지 + "흐름 중 외부가 시간을 재개하면 즉시 재정지" 가드 구독.
    // (같은 주 퇴사 이벤트 확인의 ForceStartTime 등이 후보 리스트 도중 시간을 풀어버리는 것을 차단)
    void BeginCandidateFlow()
    {
        if (_candidateFlowActive) return;
        _candidateFlowActive = true;
        GameTimeManager.Instance?.StopTime();
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnTimeStopChanged += OnTimeChangedDuringFlow;
    }

    // 채용 공개 흐름 종료 — 가드 해제 + 시간 재개 + ModalGate 점유 해제. 모든 종료 경로(채용/유지/취소)에서 호출.
    void EndCandidateFlow()
    {
        if (!_candidateFlowActive) return;
        _candidateFlowActive = false;
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnTimeStopChanged -= OnTimeChangedDuringFlow;
        GameTimeManager.Instance?.StartTime();
        ModalGate.I.Unregister(this);
    }

    // 흐름 도중 외부가 시간을 재개하면(StartTime/ForceStartTime) 즉시 다시 멈춤.
    void OnTimeChangedDuringFlow(bool stopped)
    {
        if (_candidateFlowActive && !stopped)
            GameTimeManager.Instance?.StopTime();
    }

    void OpenCandidateList(int tierIndex)
    {
        ModalGate.I.Register(this); // 후보 표시 동안 다른 모달(퇴사 등) 차단·큐잉 (닫힐 때 EndCandidateFlow 에서 해제)
        gameObject.SetActive(true);
        tierPanel.SetActive(false);
        hiringPanel.SetActive(false);
        confirmPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(true);

        _currentTierIndex = tierIndex;
        _refreshUsed = false; // 새 진입(재접속 복원 포함) 시 새로고침 권리 부여 — 리롤은 무를 수 있음
        // 후보 리스트(HiringPanel) 단계 폐지 — 바로 확정 화면(ConfirmHirePanel) 캐러셀로 진입.
        ShowConfirmDirect(EmployeeManager.Instance.GetHiringListA()); // 저장된 초기 리스트
    }

    // 슬롯 리스트(HiringPanel) 없이 확정 화면(ConfirmHirePanel)으로 바로 진입 — 첫 후보를 가운데에 띄우고 캐러셀 구성.
    void ShowConfirmDirect(List<EmployeeData> candidates)
    {
        SetCandidates(candidates);
        if (loadingPanel != null) loadingPanel.SetActive(false);
        hiringPanel.SetActive(false);
        confirmPanel.SetActive(true);
        _currentIndex = (candidates != null && candidates.Count > 0) ? 0 : -1;
        PopulateConfirm();
        UpdateArrowButtons();
        UpdateRefreshButton();
    }

    // 비서 초상화 RandomEventUI 안내(EventPanel). 확인 시 onConfirm. type=Recruit 라 시간 강제재개(ResumeFromEvent) 안 함.
    void ShowSecretaryEvent(string description, System.Action onConfirm)
    {
        if (RandomEventUI.Instance == null) { onConfirm?.Invoke(); return; }
        RandomEventUI.Instance.Show(new RandomEventData
        {
            type                    = RandomEventType.Recruit,
            title                   = "직원 채용",
            description             = description,
            portraitId              = "portrait_secretary",
            systemMessage           = "",     // 공백이지만
            keepSystemMessageActive = true,   // systemMessageText GameObject 는 활성 유지
            onApply                 = onConfirm,
        });
    }

    // 후보 리스트 1세트 생성(동기) — 등급/능력치/연봉 + 강화 + 확정 채용비용까지. 공개 시 A/B 각각 생성용.
    List<EmployeeData> GenerateCandidateList(int tierIndex)
    {
        var (label, baseCost, range, weights) = Tiers[tierIndex];
        int recruitBonus  = TraitEffectApplier.GetRecruitApplicantsBonus();
        int hiringPenalty = RandomEventManager.Instance?.HiringPenalty ?? 0;
        // 테크트리 '한 명 더!(hire_more)' — 후보 +1
        int hireMoreBonus = (TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("hire_more")) ? 1 : 0;
        int effectiveCount = Mathf.Max(1, candidateCount + recruitBonus + hireMoreBonus - hiringPenalty);

        List<EmployeeData> result = null;
        EmployeeManager.Instance.LoadRandomCandidates(effectiveCount, tierIndex, candidates =>
        {
            foreach (var employee in candidates)
            {
                ApplyEnhancementLevel(employee, RollWeighted(range, weights));
                // 채용 비용도 이 시점에 확정(재접속에도 고정) — 후보에 저장.
                int b = EmployeeManager.GetExpectedEnhanceCost(employee.enhancementLevel);
                employee.hireCost = Mathf.RoundToInt(b * UnityEngine.Random.Range(0.8f, 1.2f));
            }
            result = candidates;
        });
        return result ?? new List<EmployeeData>();
    }

    // 테크트리 '한 번 더!(hire_refresh)' — 같은 세션 1회 한정 후보 전체 재추첨. 확인창 없이 즉시 실행.
    // HiringPanel 폐지로 확정 화면 셔플(OnClickRefreshFromConfirm)과 동일하게 동작.
    public void OnClickRefresh() => OnClickRefreshFromConfirm();

    // ConfirmHirePanel 의 새로고침 버튼 — refreshButton 과 같은 1회 가드(_refreshUsed)·해금(hire_refresh)을 공유.
    // HiringPanel 새로고침과 달리 슬롯 리스트로 돌아가지 않고 확정 화면을 유지하며 이력서 셔플 연출을 보여준다. 확인창 없이 즉시 실행.
    public void OnClickRefreshFromConfirm()
    {
        if (_refreshUsed) return;
        if (TechTreeManager.Instance == null || !TechTreeManager.Instance.IsUnlocked("hire_refresh")) return;
        if (_currentTierIndex < 0) return;

        _refreshUsed = true;
        RefreshCandidatesWithShuffle();
    }

    // 확정 화면에서 후보 재추첨 + 이력서 셔플. 데이터 교체는 셔플 가운데(스택된 순간)에 숨겨 자연스럽게 보이게 한다.
    void RefreshCandidatesWithShuffle()
    {
        var candidates = EmployeeManager.Instance.GetHiringListB(); // 미리 확정된 리롤 리스트
        SetCandidates(candidates);

        // 셔플 동안 좌/우 이력서도 함께 움직이도록, 후보가 多면 미리 활성화(없던 경우 대비).
        bool multi = candidates.Count > 1;
        if (resumeLeftPanel  != null) resumeLeftPanel.gameObject.SetActive(multi);
        if (resumeRightPanel != null) resumeRightPanel.gameObject.SetActive(multi);

        UpdateRefreshButton(); // _refreshUsed=true → 두 새로고침 버튼 모두 비활성

        void SwapToNewCandidates()
        {
            _currentIndex = 0;
            PopulateConfirm();
            UpdateArrowButtons();
        }

        if (resumeFlipper != null)
            resumeFlipper.Shuffle(onMidpoint: SwapToNewCandidates);
        else
            SwapToNewCandidates();
    }

    void UpdateRefreshButton()
    {
        bool unlocked = TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("hire_refresh");
        bool usable = unlocked && !_refreshUsed;
        if (refreshButton != null)        refreshButton.interactable        = usable;
        if (confirmRefreshButton != null) confirmRefreshButton.interactable = usable;
        // 미해금 시 잠금 덮개 활성화, 해금되면 비활성화
        if (refreshLockedPanel != null)        refreshLockedPanel.SetActive(!unlocked);
        if (confirmRefreshLockedPanel != null) confirmRefreshLockedPanel.SetActive(!unlocked);
    }

    // 가중치 랜덤 롤
    int RollWeighted(int[] range, int[] weights)
    {
        int total = 0;
        foreach (int w in weights) total += w;

        int roll = UnityEngine.Random.Range(0, total);
        int cum = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            cum += weights[i];
            if (roll < cum) return range[i];
        }
        return range[0];
    }

    void ApplyEnhancementLevel(EmployeeData employee, int targetLevel)
    {
        EmployeeManager.Instance.ApplyEnhancementExpected(employee, targetLevel);
    }

    // 후보 리스트 + 후보별 채용 비용 캐시 세팅 (슬롯 UI 갱신 없음 — ShowCandidates/RefreshCandidatesWithShuffle 공용).
    // 비용은 한 번만 추첨해 캐시 — 화살표로 이력서를 넘길 때마다 비용이 흔들리지 않게 고정.
    void SetCandidates(List<EmployeeData> candidates)
    {
        _currentCandidates = candidates;
        _hireCosts.Clear();
        foreach (var e in candidates)
            _hireCosts.Add(e.hireCost); // 공개 시 확정된 비용 사용 (재접속에도 고정)
    }

    // (구) 슬롯 리스트 표시(ShowCandidates)·슬롯 선택(OnSelectEmployee) 제거 — HiringPanel 단계 폐지로 ShowConfirmDirect 가 대체.

    // 오른쪽 화살표 — 다음 후보 이력서로 넘김
    public void OnClickNextCandidate() => FlipTo(+1);
    // 왼쪽 화살표 — 이전 후보 이력서로 넘김
    public void OnClickPrevCandidate() => FlipTo(-1);

    // 현재 채용 리스트에서 dir(+1 다음 / -1 이전)만큼 이동(양끝 순환).
    // ResumeFlipper 가 있으면 페이지 넘김 연출 중간(엣지온)에 데이터를 교체한다.
    void FlipTo(int dir)
    {
        if (_currentCandidates == null || _currentCandidates.Count <= 1) return;
        if (resumeFlipper != null && resumeFlipper.IsFlipping) return;

        int next = (_currentIndex + dir + _currentCandidates.Count) % _currentCandidates.Count;

        if (resumeFlipper != null)
            resumeFlipper.Flip(dir, () =>
            {
                _currentIndex = next;
                PopulateConfirm();
            });
        else
        {
            _currentIndex = next;
            PopulateConfirm();
        }
    }

    // 후보가 2명 이상일 때만 화살표 + 좌/우 이력서 노출 (1명 이하면 가운데만).
    void UpdateArrowButtons()
    {
        bool multi = _currentCandidates != null && _currentCandidates.Count > 1;
        if (prevCandidateButton != null) prevCandidateButton.gameObject.SetActive(multi);
        if (nextCandidateButton != null) nextCandidateButton.gameObject.SetActive(multi);
        if (resumeLeftPanel  != null) resumeLeftPanel.gameObject.SetActive(multi);
        if (resumeRightPanel != null) resumeRightPanel.gameObject.SetActive(multi);
    }

    // 캐러셀 3슬롯을 _currentIndex 기준으로 채운다 — 좌=이전 / 가운데=현재 / 우=다음 (양끝 순환).
    // 미리 옆 패널에 이전/다음 후보를 띄워두므로, 넘기면 옆 패널이 그대로 가운데로 온다.
    // 넘김 종료 콜백(ResumeFlipper onSwap)·최초 선택(OnSelectEmployee)에서 호출.
    void PopulateConfirm()
    {
        if (_currentCandidates == null || _currentCandidates.Count == 0) return;
        int n  = _currentCandidates.Count;
        int ci = _currentIndex;
        var center = _currentCandidates[ci];

        _selectedEmployee = center;

        // 후보별 캐시 비용 사용(넘겨도 고정). 캐시 미스 시에만 즉석 추첨.
        // 테크트리 '채용 비용 할인(hire_discount)' 은 티어 진입 비용에만 적용 — 개별 직원 채용 비용에는 적용 안 함.
        _hireCost = (ci >= 0 && ci < _hireCosts.Count)
            ? _hireCosts[ci]
            : Mathf.RoundToInt(EmployeeManager.GetExpectedEnhanceCost(center.enhancementLevel) * UnityEngine.Random.Range(0.8f, 1.2f));

        // 좌·우 패널을 Setup "전에" 먼저 활성화 — 비활성 상태에서 Setup 하면 TMP 텍스트(수치)는
        // 메시 재생성이 누락돼 늦게 반영되는 반면 Image(초상)는 활성화 후 정상 표시되는 이슈 방지.
        // (이미지는 미리 반영되는데 수치만 늦게 바뀌던 현상 교정 — 활성/비활성은 UpdateArrowButtons 와 동일 규칙: 후보 2명 이상)
        bool multi = n > 1;
        if (resumeLeftPanel  != null && resumeLeftPanel.gameObject.activeSelf  != multi) resumeLeftPanel.gameObject.SetActive(multi);
        if (resumeRightPanel != null && resumeRightPanel.gameObject.activeSelf != multi) resumeRightPanel.gameObject.SetActive(multi);

        // 좌:이전 / 중앙:현재 / 우:다음 후보 미리 표시 (이미지·수치 동시 세팅)
        if (resumeCenterPanel != null) resumeCenterPanel.Setup(center);
        if (resumeLeftPanel  != null) resumeLeftPanel.Setup(_currentCandidates[(ci - 1 + n) % n]);
        if (resumeRightPanel != null) resumeRightPanel.Setup(_currentCandidates[(ci + 1) % n]);

        if (confirmHireCostText != null)
            confirmHireCostText.text = _hireCost <= 0 ? "무료" : $"{_hireCost:N0}G";

        // CountPanel — 현재 후보 순서/전체 후보 수 (예: 1/4). 화살표로 넘기면 PopulateConfirm 재호출로 자동 갱신.
        if (countText != null) countText.text = $"{ci + 1}/{n}";

        // 동일 직원 보유 여부 확인 (중앙 후보 기준, masterEmployeeId 공백이면 이름으로 대조)
        _conflictingOwned = EmployeeManager.Instance.ownedEmployees
            .Find(e => EmployeeSlotUI.IsSameEmployee(e, center));

        bool hasConflict = _conflictingOwned != null;
        // 일단 보유 직원 비교 패널(OwnedEmployeeSlot) 활성화 주석 처리 — EmployeeResumePanel 개편 중 (지우지 말 것, 추후 복구)
        // if (comparisonSection != null) comparisonSection.SetActive(hasConflict);
        if (keepButton != null) keepButton.gameObject.SetActive(hasConflict);

        if (existEmployeePanel != null)
        {
            existEmployeePanel.gameObject.SetActive(hasConflict);
            if (hasConflict)
            {
                SyncExistEmployeePanelSorting(); // 가운데 이력서 카드(EmployeeResumePanel)와 같은 Canvas sortingOrder로
                existEmployeePanel.Setup(_conflictingOwned);
            }
        }

        if (hasConflict)
        {
            var owned = _conflictingOwned;
            if (ownedNameText != null)       ownedNameText.text       = owned.employeeName;
            if (ownedRoleText != null)       ownedRoleText.text       = owned.RoleToString();
            if (ownedGradeText != null)      ownedGradeText.text      = owned.GradeToString();
            CharacterTraitApplier.SetupTraitText(ownedTraitText, owned);
            CharacterUniqueEvents.SetupEventText(ownedTraitText, owned);
            if (ownedPotentialText != null)  ownedPotentialText.text  = owned.PotentialToString();
            if (ownedDevelopText != null)    ownedDevelopText.text    = owned.DevelopText();
            if (ownedPlanningText != null)   ownedPlanningText.text   = owned.PlanningText();
            if (ownedArtText != null)        ownedArtText.text        = owned.ArtText();
            if (ownedCreativityText != null) ownedCreativityText.text = owned.CreativityText();
            if (ownedSalaryText != null)     ownedSalaryText.text     = owned.SalaryText();
            if (ownedEnhancementText != null)ownedEnhancementText.text= $"+{owned.enhancementLevel}";
        }
    }

    // ExistEmployeePanel 은 이력서 카드의 "형제"라 자체 Canvas(overrideSorting)로 따로 그려진다 — 가운데
    // 이력서 카드(EmployeeResumePanel, ResumeFlipper.RestoreSorting 이 매 프레임 갱신)의 sortingOrder를 기준으로
    // +1 을 줘서 항상 카드보다 위에 그려지게 한다.
    void SyncExistEmployeePanelSorting()
    {
        if (existEmployeePanel == null || resumeCenterPanel == null) return;
        var centerCanvas = resumeCenterPanel.GetComponent<Canvas>();
        var existCanvas  = existEmployeePanel.GetComponent<Canvas>();
        if (centerCanvas != null && existCanvas != null)
        {
            existCanvas.overrideSorting = true;
            existCanvas.sortingOrder    = centerCanvas.sortingOrder + 1;
        }
    }

    // 도장(StampImage) "쾅!" 연출 — 확대된 상태(3.8배, 기울어짐)에서 순간적으로 내려찍히듯 축소+정렬되며
    // 알파도 그 찰나에 확 켜지고(페이드 아님), 이후 튕겨나오듯 최종 스케일(1)에 안착. 총 1초.
    void PlayHireStamp()
    {
        if (hireStampImage == null) return;
        hireStampImage.gameObject.SetActive(true);

        var rt = hireStampImage.rectTransform;
        rt.DOKill();
        hireStampImage.DOKill();

        const float startScale  = 3.8f;   // 원래 세팅값 — 여기서 시작해 내려찍힌다
        const float impactScale = 0.55f;  // 찍히는 순간 살짝 눌리는 임팩트 스케일
        const float finalScale  = 1f;
        const float impactDuration = 0.12f;  // "쾅" — 아주 짧고 빠르게
        const float settleDuration = 0.35f;  // 튕겨나오며 최종 크기로 안착
        const float totalDuration  = 1.2f;

        rt.localScale = Vector3.one * startScale;
        //rt.localRotation = Quaternion.Euler(0f, 0f, -12f);
        var c = hireStampImage.color; c.a = 0f; hireStampImage.color = c;

        var seq = DOTween.Sequence().SetUpdate(true).SetTarget(hireStampImage);
        seq.Append(hireStampImage.DOFade(1f, impactDuration));               // 찍히는 순간 알파 확 등장
        seq.Join(rt.DOScale(impactScale, impactDuration).SetEase(Ease.InQuad));
        seq.Join(rt.DOLocalRotate(Vector3.zero, impactDuration).SetEase(Ease.InQuad));
        seq.Append(rt.DOScale(finalScale, settleDuration).SetEase(Ease.OutElastic, 1.1f, 0.6f)); // 임팩트 후 튕겨나오며 안착
        seq.AppendInterval(totalDuration - impactDuration - settleDuration); // 총 1초를 채움
        // 비활성화는 여기서 타이머로 하지 않는다 — ConfirmHirePanel 이 실제로 꺼지는 DoHire() 시점에 함께 꺼야
        // "패널이 닫히기 전에 도장이 먼저 사라지는" 부자연스러움이 없다.
    }

    // 채용 버튼 — 즉시 채용하지 않고 확인 다이얼로그(ConfirmUI)를 띄운다.
    public void OnClickConfirmHire()
    {
        if (_selectedEmployee == null) return;

        // 동명 직원이 파견중이면 해고 후 채용이 불가(파견중 해고 차단) → 채용 자체를 막고 안내
        if (_conflictingOwned != null && DispatchManager.Instance != null
            && DispatchManager.Instance.IsDispatched(_conflictingOwned.id))
        {
            AlertUI.Instance?.Show($"{_conflictingOwned.employeeName}이 파견중입니다.");
            return;
        }

        // 동일 직원 교체(_conflictingOwned)면 인원수가 그대로 유지되므로 정원 체크 예외.
        // 신규 채용인데 이미 정원이 꽉 찼으면 — 알림 후 직원목록(해고 전용 모드)으로 유도, 해고 성공 시 이 지점부터 이어서 채용 완료.
        bool isReplacement = _conflictingOwned != null;
        if (!isReplacement && EmployeeManager.Instance.ownedEmployees.Count >= StageManager.Instance.MaxEmployeeCount)
        {
            int max = StageManager.Instance.MaxEmployeeCount;
            confirmPanel.SetActive(false); // 알림이 뜨는 순간부터 뒤에 안 보이게
            // HiringUI 가 이미 ModalGate 를 쥔 채로 열려있는 상황 — bypassGate 없으면 게이트가 안 풀려 영원히 안 뜬다.
            AlertUI.Instance.Show($"최대 인원은 {max}명입니다.\n한명을 해고하세요.",
                onConfirm: () =>
                {
                    EmployeeListUI.Instance?.OpenForForceFire(fired =>
                    {
                        confirmPanel.SetActive(true); // 취소든 해고든 EmployeeListUI 가 닫히면 항상 복귀
                        // 복귀 직후 곧바로 스탬프가 찍히면 너무 빨라 안 보임 — 1초 대기 후 재생.
                        if (fired) DOVirtual.DelayedCall(1f, OnConfirmHireYes).SetUpdate(true);
                    });
                },
                bypassGate: true);
            return;
        }

        ConfirmUI.Instance.Show(
            $"{_selectedEmployee.employeeName}을(를) 채용하시겠습니까?",
            onConfirm: OnConfirmHireYes,
            onCancel: () => { },          // 아니오 — ConfirmUI 만 닫고 이력서 화면 유지
            confirmText: "네",
            cancelText: "아니오"
        );
    }

    // ConfirmUI "네" 선택 — 도장 연출(1초) 재생 후, 1초 더 대기했다가 실제 채용 처리 + 패널 닫기.
    void OnConfirmHireYes()
    {
        PlayHireStamp();
        DOVirtual.DelayedCall(2f, DoHire).SetUpdate(true);
    }

    // 실제 채용 처리 (ConfirmUI 에서 '네' 선택 시)
    void DoHire()
    {
        if (_selectedEmployee == null) return;
        // 방어 — 파견중 동명 직원은 해고 불가라 중복 보유가 되어버림. 채용 중단.
        if (_conflictingOwned != null && DispatchManager.Instance != null
            && DispatchManager.Instance.IsDispatched(_conflictingOwned.id))
        {
            AlertUI.Instance?.Show($"{_conflictingOwned.employeeName}이 파견중입니다.");
            return;
        }
        if (_hireCost > 0 && !MoneyManager.Instance.CanAfford(_hireCost))
        {
            GameUIHelper.ShowLoanPrompt();
            return;
        }
        if (_hireCost > 0)
        {
            MoneyManager.Instance.SpendGold(_hireCost);
            Debug.Log($"[채용] {_selectedEmployee.employeeName} 채용 비용 차감: {_hireCost:N0}G (강화 +{_selectedEmployee.enhancementLevel})");
        }

        // 동일 직원이 있으면 해고 후 채용
        if (_conflictingOwned != null)
        {
            EmployeeManager.Instance.FireEmployee(_conflictingOwned, countAsExit: false);
            _conflictingOwned = null;
        }

        EndCandidateFlow();
        EmployeeManager.Instance.HireEmployee(_selectedEmployee);
        EmployeeManager.Instance.ClearHiring();        // 채용 완료 → pending/확정 리스트 해제
        GameTimeManager.Instance?.SaveGameTime();
        hiringPanel.SetActive(false);
        confirmPanel.SetActive(false);     // ConfirmHirePanel 비활성화
        if (hireStampImage != null) hireStampImage.gameObject.SetActive(false); // 패널과 같은 시점에 도장도 정리

        // 온보딩: 직원 획득 후 채용창 닫히면 바로 프로젝트 튜토리얼 (1회만).
        // pending=0(실행대기)로 무장 후 즉시 트리거 — ModalGate.WhenFree 가 채용창/다이얼로그 닫힘을 기다렸다 실행.
        OnboardingState.ArmProjectTutorial(0);
        ProjectTutorialController.Instance?.TryFire();

        DialogManager.Instance.Resume();
    }

    // 보유 직원 유지하기 (비교 패널에서) — 채용 안 함, 모집 1회 소모
    public void OnClickKeep()
    {
        EndCandidateFlow();
        EmployeeManager.Instance.ClearHiring();        // 모집 종료 → pending/확정 리스트 해제
        GameTimeManager.Instance?.SaveGameTime();
        hiringPanel.SetActive(false);
        confirmPanel.SetActive(false);
        tierPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(false);
        DialogManager.Instance.Resume();
    }

    // "리스트가기" 제거 — 후보 리스트(HiringPanel) 단계를 폐지해 돌아갈 리스트가 없다. (버튼은 씬에서 제거)
    public void OnClickBack() { }

    public void OnClickClose()
    {
        
        // 취소 확정 시에만 흐름 종료(시간 재개). "채용진행" 선택 시엔 리스트 유지 + 시간 정지 유지.
        ConfirmUI.Instance.Show(
            "채용을 취소하시겠습니까?",
            onConfirm: () =>
            {
                EndCandidateFlow();
                EmployeeManager.Instance.ClearHiring();        // 채용 취소 → pending/확정 리스트 해제
                GameTimeManager.Instance?.SaveGameTime();
                tierPanel.SetActive(false);
                hiringPanel.SetActive(false);
                confirmPanel.SetActive(false);
                if (loadingPanel != null) loadingPanel.SetActive(false);
                DialogManager.Instance.EndDialog(); //다이얼로그 멈춤
            },
            onCancel: () => { },
            confirmText: "채용취소",
            cancelText: "채용진행"
        );
    }
    public void OnClickTierPanelClose()
    {
        tierPanel.SetActive(false);
        GameTimeManager.Instance.StartTime();
    }
}