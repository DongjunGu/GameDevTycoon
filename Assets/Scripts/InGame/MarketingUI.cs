using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class MarketingUI : MonoBehaviour
{
    public static MarketingUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject marketingPanel;

    [Header("Slots - Left(0~7), Right(8~15) 순서")]
    public Button[] slotButtons;   // 16개
    public TextMeshProUGUI[] slotPlatformTexts; // 16개
    public TextMeshProUGUI[] slotCostTexts;     // 16개

    [Header("Confirm")]
    public Button confirmBtn; // 기본 interactable=false, 슬롯 하나라도 선택되면 true

    private readonly (string name, int cost, string desc)[] _marketingData =
    {
        ("전단지 돌리기",        500,    "광고할 돈이 없으면 전단지라도 돌리자"),
        ("PC방 광고",           1000,   "잼민이 모니터 구석 기습 광고"),
        ("카페 배너 광고",       4000,   "진짜 게이머를 대상으로 한 가성비 광고"),
        ("체험판 광고",         12000,   "맛보기 게임 배포로 입소문 내기"),
        ("지하철 광고",         60000,   "직장인 타겟의 무난한 홍보 방법"),
        ("게임쇼 대형 부스",    120000,   "화려한 천막으로 시선 강탈"),
        ("스트리머 방송",       300000,   "인기 방송 버프! 낮은 확률로 대박 발생"),
        ("영끌 마케팅",         400000,   "다음 신작 매출 감소하는 대신 이번 게임 매출 폭발"),
        ("야구 스폰서",         600000,   "가장 비싸지만 효과 만점"),
    };

    private int[] _slotCosts;      // 표시 비용 캐시 (marketingFree 반영)
    private int _selectedIndex = -1;
    private int _totalCost;
    private System.Action _onComplete;

    // 슬롯 버튼 직속 자식 "description"(TMP) 캐시 — 인스펙터 배선 없이 코드로 바인딩
    private TextMeshProUGUI[] _slotDescCache;
    TextMeshProUGUI GetSlotDescription(int i)
    {
        if (slotButtons == null || i < 0 || i >= slotButtons.Length) return null;
        _slotDescCache ??= new TextMeshProUGUI[slotButtons.Length];
        if (_slotDescCache[i] == null && slotButtons[i] != null)
        {
            var t = slotButtons[i].transform.Find("description");
            if (t != null) _slotDescCache[i] = t.GetComponent<TextMeshProUGUI>();
        }
        return _slotDescCache[i];
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        marketingPanel.SetActive(false);
    }

    public void Show(System.Action onComplete)
    {
        // 마케팅의 신 trait — 모든 슬롯 비용 0G + 차감 스킵 (이 세션 동안 캡처)
        bool marketingFree = TraitEffectApplier.HasMarketingFree();

        // 가장 저렴한 슬롯(전단지 돌리기, 500G)조차 못 낼 정도로 자금이 없으면 패널을 열 필요 없이 스킵.
        // marketingFree면 비용 자체가 0이라 이 체크는 무의미 — 통과.
        if (!marketingFree)
        {
            int minCost = _marketingData[0].cost;
            for (int i = 1; i < _marketingData.Length; i++)
                if (_marketingData[i].cost < minCost) minCost = _marketingData[i].cost;

            if (MoneyManager.Instance.Gold < minCost)
            {
                AlertUI.Instance.Show("마케팅할 자금이 부족합니다.", () => onComplete?.Invoke());
                return;
            }
        }

        GameTimeManager.Instance?.StopTime();
        ModalGate.I.Register(this);
        _onComplete = onComplete;
        _totalCost = 0;
        _selectedIndex = -1;
        if (confirmBtn != null) confirmBtn.interactable = false;

        _slotCosts = new int[_marketingData.Length];

        // 전체 슬롯 텍스트 초기화 (비활성화 없이 공백)
        for (int i = 0; i < slotButtons.Length; i++)
        {
            slotPlatformTexts[i].text = "";
            slotCostTexts[i].text = "";
            var descText = GetSlotDescription(i);
            if (descText != null) descText.text = "";
            slotButtons[i].onClick.RemoveAllListeners();
            slotButtons[i].interactable = false;
            SetSlotSelected(i, false);
        }

        // 데이터 있는 슬롯만 텍스트 + 클릭 설정
        for (int i = 0; i < _marketingData.Length; i++)
        {
            var (name, cost, desc) = _marketingData[i];

            int displayCost = marketingFree ? 0 : cost;
            _slotCosts[i] = displayCost;
            slotPlatformTexts[i].text = name;
            slotCostTexts[i].text = $"{displayCost:N0} G";
            var descText = GetSlotDescription(i);
            if (descText != null) descText.text = desc;
            slotButtons[i].interactable = true;

            int capturedIndex = i;
            slotButtons[i].onClick.AddListener(() => OnClickSlot(capturedIndex));
        }
        marketingPanel.SetActive(true);

        // 온보딩 튜토리얼 15-1/15-2 — 첫 프로젝트 한정, 마케팅 패널 열릴 때.
        if (!OnboardingState.Tutorial15Done
            && CompletedProjectManager.Instance != null && CompletedProjectManager.Instance.completedProjects.Count == 0
            && TutorialController.Instance != null)
        {
            TutorialController.Instance.StartCoroutine(TutorialController.Instance.PlayTutorial15());
        }
    }

    void OnClickSlot(int index)
    {
        _selectedIndex = (_selectedIndex == index) ? -1 : index;
        for (int i = 0; i < _marketingData.Length; i++)
            SetSlotSelected(i, i == _selectedIndex);
        if (confirmBtn != null) confirmBtn.interactable = _selectedIndex >= 0;
    }

    // 기본 alpha 0(안 보임) / 선택 시 255 — HiringUI.SetTierSelected, ProjectSetupUI 선택 표시와 동일 패턴.
    void SetSlotSelected(int index, bool selected)
    {
        if (slotButtons == null || index < 0 || index >= slotButtons.Length || slotButtons[index] == null) return;
        var img = slotButtons[index].image;
        if (img == null) return;
        var c = img.color;
        c.a = selected ? 1f : 0f;
        img.color = c;
    }

    public void OnClickComplete()
    {
        // confirmBtn이 미선택 상태(_selectedIndex<0)에선 interactable=false라 정상 흐름에선 여기 못 옴 —
        // 그래도 방어적으로 미선택이면 비용 0 처리.
        int cost = _selectedIndex >= 0 ? _slotCosts[_selectedIndex] : 0;

        if (cost > 0 && !MoneyManager.Instance.CanAfford(cost))
        {
            // MarketingUI 자신이 Show()에서 ModalGate.Register(this)로 게이트를 쥔 채 열려있는 상태라
            // bypassGate 없이 부르면 패널이 열려있는 동안 안 뜨고 대기만 함(ProjectSetupUI와 동일 버그). 즉시 표시.
            GameUIHelper.ShowLoanPrompt(bypassGate: true);
            return;
        }

        if (cost > 0)
            MoneyManager.Instance.SpendGold(cost, saveImmediately: false);
        _totalCost = cost;

        MoneyManager.Instance.SaveMoney(); // ← 완료 시 한 번만 저장
        marketingPanel.SetActive(false);
        GameTimeManager.Instance.StartTime();
        ModalGate.I.Unregister(this);
        _onComplete?.Invoke();
    }
    public int GetTotalCost() => _totalCost;
    public void TestMarketing()
    {
        MarketingUI.Instance.Show(() =>
        {
            Debug.Log("마케팅 완료");
        });
    }
}