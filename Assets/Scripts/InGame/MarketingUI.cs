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
        ("SNS 바이럴",          5000),
        ("전문 잡지/웹진 광고",  20000),
        ("유명 인플루언서 협찬", 30000),
        ("TV 및 옥외 광고",      50000),
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
    _onComplete = onComplete;
    _totalCost  = 0;
    _countMap.Clear();

    // 전체 슬롯 텍스트 초기화 (비활성화 없이 공백)
    for (int i = 0; i < slotButtons.Length; i++)
    {
        slotPlatformTexts[i].text = "";
        slotCostTexts[i].text     = "";
        slotButtons[i].onClick.RemoveAllListeners();
        slotButtons[i].interactable = false;
    }

    // 데이터 있는 슬롯만 텍스트 + 클릭 설정
    for (int i = 0; i < _marketingData.Length; i++)
    {
        var (name, cost) = _marketingData[i];
        _countMap[name]  = 0;

        slotPlatformTexts[i].text   = name;
        slotCostTexts[i].text       = $"{cost:N0}원";
        slotButtons[i].interactable = true;

        int capturedIndex = i;
        slotButtons[i].onClick.AddListener(() =>
            OnClickMarketing(_marketingData[capturedIndex].name,
                             _marketingData[capturedIndex].cost));
    }

    UpdateUI();
    marketingPanel.SetActive(true);
}
    void OnClickMarketing(string name, int cost)
    {
        _countMap[name]++;
        _totalCost += cost;
        UpdateUI();

        MarketingNoticeUI.Instance.Show(name);
    }

    public void OnClickComplete()
    {
        marketingPanel.SetActive(false);
        _onComplete?.Invoke();
    }

    void UpdateUI()
    {
        totalCostText.text = $"총 비용: {_totalCost:N0}원";
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