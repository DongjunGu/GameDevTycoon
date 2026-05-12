using System.Collections.Generic;
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

    [Header("UI")]
    public TextMeshProUGUI totalCostText;

    private readonly (string name, int cost)[] _marketingData =
    {
        ("전단지 및 커뮤니티 홍보",          500),
        ("SNS 광고 및 포털 배너",           2000),
        ("유명 인플루언서 협찬",            3000),
        ("대도시 전광판 및 TV 광고",        5000),
        ("글로벌 게임 페스티벌 참가",       10000),
    };

    private Dictionary<string, int> _countMap = new();
    private int _totalCost;
    private System.Action _onComplete;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        marketingPanel.SetActive(false);
    }

    public void Show(System.Action onComplete)
    {
        GameTimeManager.Instance?.StopTime();
        _onComplete = onComplete;
        _totalCost = 0;
        _countMap.Clear();

        // 마케팅의 신 trait — 모든 슬롯 비용 0G + 차감 스킵 (이 세션 동안 캡처)
        bool marketingFree = TraitEffectApplier.HasMarketingFree();

        // 전체 슬롯 텍스트 초기화 (비활성화 없이 공백)
        for (int i = 0; i < slotButtons.Length; i++)
        {
            slotPlatformTexts[i].text = "";
            slotCostTexts[i].text = "";
            slotButtons[i].onClick.RemoveAllListeners();
            slotButtons[i].interactable = false;
        }

        // 데이터 있는 슬롯만 텍스트 + 클릭 설정
        for (int i = 0; i < _marketingData.Length; i++)
        {
            var (name, cost) = _marketingData[i];
            _countMap[name] = 0;

            int displayCost = marketingFree ? 0 : cost;
            slotPlatformTexts[i].text = name;
            slotCostTexts[i].text = $"{displayCost:N0}G";
            slotButtons[i].interactable = true;

            int capturedIndex = i;
            slotButtons[i].onClick.AddListener(() =>
                OnClickMarketing(_marketingData[capturedIndex].name, displayCost));
        }

        UpdateUI();
        marketingPanel.SetActive(true);
    }
    void OnClickMarketing(string name, int cost)
    {
        if (cost > 0 && !MoneyManager.Instance.CanAfford(cost))
        {
            GameUIHelper.ShowLoanPrompt();
            return;
        }

        if (cost > 0)
            MoneyManager.Instance.SpendGold(cost, saveImmediately: false); // ← 저장 안 함 (Complete 시 일괄)

        _countMap[name]++;
        _totalCost += cost;
        UpdateUI();
        MarketingNoticeUI.Instance.Show(name);
    }

    public void OnClickComplete()
    {
        MoneyManager.Instance.SaveMoney(); // ← 완료 시 한 번만 저장
        marketingPanel.SetActive(false);
        GameTimeManager.Instance.StartTime();
        _onComplete?.Invoke();
    }

    void UpdateUI()
    {
        totalCostText.text = $"총 비용: {_totalCost:N0}G";
    }

    public Dictionary<string, int> GetCountMap() => _countMap;
    public int GetTotalCost() => _totalCost;
    public void TestMarketing()
    {
        MarketingUI.Instance.Show(() =>
        {
            Debug.Log("마케팅 완료");
        });
    }
}