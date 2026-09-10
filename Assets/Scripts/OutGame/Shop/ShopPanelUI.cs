using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 상점 패널 — 5 카테고리 (기간한정/뽑기/다이아/골드/패키지). 좌측 카테고리 바, 우측 카테고리별 컨텐츠.
// 현재는 뽑기 카테고리만 구현 (직원 일반/스페셜, 특성 일반/스페셜/프리미엄 = 5 버튼).
// 직원 일반/스페셜은 등급 가중치만 다름. 특성 3버튼은 아직 동일 기능. 전부 무료 1회 뽑기.
public class ShopPanelUI : MonoBehaviour
{
    public enum Category { LimitedTime = 0, Gacha = 1, Diamond = 2, Gold = 3, Package = 4 }

    [Header("Category Buttons (인덱스 = Category enum)")]
    public Button[] categoryButtons = new Button[5];
    [Tooltip("선택 시 켜질 노란 강조 GameObject — 각 버튼별.")]
    public GameObject[] categoryHighlights = new GameObject[5];
    [Tooltip("선택 시 활성화될 우측 컨텐츠 — 각 카테고리별.")]
    public GameObject[] categoryContents = new GameObject[5];

    [Header("Default")]
    public Category defaultCategory = Category.Gacha;

    [Header("Gacha Buttons (뽑기 카테고리)")]
    public Button employeeNormalBtn;
    public Button employeeSpecialBtn;
    [Tooltip("10연차 버튼 — 비워두면 해당 10연차 없음.")]
    public Button employeeNormal10Btn;
    public Button employeeSpecial10Btn;
    public Button traitNormalBtn;
    public Button traitSpecialBtn;
    public Button traitPremiumBtn;

    [Header("Result")]
    public ShopGachaResultPanelUI resultPanel;

    [Header("Employee Gacha Cost (다이아)")]
    public int normalGachaCost    = 60;
    public int normalGacha10Cost  = 600;
    public int specialGachaCost   = 250;
    public int specialGacha10Cost = 2500;

    [Header("Employee Gacha Weights — 일반 (합산 후 비례 추첨)")]
    [Range(0, 100)] public int normalWeightNormal = 70;
    [Range(0, 100)] public int normalWeightRare   = 30;
    [Range(0, 100)] public int normalWeightEpic   = 0;

    [Header("Employee Gacha Weights — 스페셜")]
    [Range(0, 100)] public int specialWeightNormal = 65;
    [Range(0, 100)] public int specialWeightRare   = 30;
    [Range(0, 100)] public int specialWeightEpic   = 5;

    Category _selected;

    void OnEnable()
    {
        WireCategories();
        WireGachaButtons();
        ApplyCategory(defaultCategory);
    }

    void WireCategories()
    {
        if (categoryButtons == null) return;
        for (int i = 0; i < categoryButtons.Length; i++)
        {
            int idx = i;
            if (categoryButtons[i] == null) continue;
            categoryButtons[i].onClick.RemoveAllListeners();
            categoryButtons[i].onClick.AddListener(() => ApplyCategory((Category)idx));
        }
    }

    void WireGachaButtons()
    {
        WireBtn(employeeNormalBtn,   () => OnEmployeeGacha(false, 1));
        WireBtn(employeeSpecialBtn,  () => OnEmployeeGacha(true,  1));
        WireBtn(employeeNormal10Btn, () => OnEmployeeGacha(false, 10));
        WireBtn(employeeSpecial10Btn,() => OnEmployeeGacha(true,  10));
        WireBtn(traitNormalBtn,     OnTraitGacha);
        WireBtn(traitSpecialBtn,    OnTraitGacha);
        WireBtn(traitPremiumBtn,    OnTraitGacha);
    }

    void WireBtn(Button b, UnityEngine.Events.UnityAction handler)
    {
        if (b == null) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(handler);
    }

    void ApplyCategory(Category cat)
    {
        _selected = cat;
        for (int i = 0; i < 5; i++)
        {
            bool on = (i == (int)cat);
            if (categoryHighlights != null && i < categoryHighlights.Length && categoryHighlights[i] != null)
                categoryHighlights[i].SetActive(on);
            if (categoryContents != null && i < categoryContents.Length && categoryContents[i] != null)
                categoryContents[i].SetActive(on);
        }
    }

    // ──────────── 직원 가챠 ────────────
    void OnEmployeeGacha(bool special, int count)
    {
        var pool = EmployeeManager.Instance?.poolEmployees;
        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning("[Shop] 직원 pool 비어있음");
            return;
        }
        if (OwnedCardManager.Instance == null) return;

        int cost = special
            ? (count > 1 ? specialGacha10Cost : specialGachaCost)
            : (count > 1 ? normalGacha10Cost  : normalGachaCost);
        var wallet = OutGameCurrencyManager.Instance;
        if (wallet == null) return;
        if (!wallet.SpendDiamond(cost))
        {
            // ponytail: 아웃게임 알림 UI 없음. 다이아 부족 팝업 생기면 교체.
            Debug.LogWarning($"[Shop] 다이아 부족 — 필요 {cost}D / 보유 {wallet.Diamond}D");
            return;
        }

        string label = special ? "스페셜" : "일반";
        var draws = new List<(EmployeeData emp, EmployeeGrade grade)>(count);
        for (int i = 0; i < count; i++)
        {
            var emp   = pool[Random.Range(0, pool.Count)];
            var grade = RollEmployeeGrade(special);
            OwnedCardManager.Instance.AddCard(emp.id, grade, stage: 0, save: i == count - 1);
            Debug.Log($"[Shop] 직원 가챠({label}) → {emp.employeeName} ({grade})");
            draws.Add((emp, grade));
        }
        Debug.Log($"[Shop] 직원 가챠({label}) x{count} -{cost}D");

        ShowDrawsSequential(draws, 0);
    }

    // 닫기 버튼을 누를 때마다 다음 장. 1회 뽑기도 같은 경로.
    void ShowDrawsSequential(List<(EmployeeData emp, EmployeeGrade grade)> draws, int index)
    {
        if (resultPanel == null || index >= draws.Count) return;
        resultPanel.onClosed = () => ShowDrawsSequential(draws, index + 1);
        resultPanel.ShowEmployee(draws[index].emp, draws[index].grade);
    }

    EmployeeGrade RollEmployeeGrade(bool special)
    {
        int wn = special ? specialWeightNormal : normalWeightNormal;
        int wr = special ? specialWeightRare   : normalWeightRare;
        int we = special ? specialWeightEpic   : normalWeightEpic;
        int total = Mathf.Max(1, wn + wr + we);
        int r = Random.Range(0, total);
        if (r < wn) return EmployeeGrade.Normal;
        if (r < wn + wr) return EmployeeGrade.Rare;
        return EmployeeGrade.Epic;
    }

    // ──────────── 특성 가챠 ────────────
    void OnTraitGacha()
    {
        if (OwnedTraitManager.Instance == null) return;
        var cache = TraitChartLoader.Cache;
        if (cache == null || cache.Count == 0)
        {
            Debug.LogWarning("[Shop] 특성 차트 비어있음");
            return;
        }

        var ids = new List<string>(cache.Keys);
        string id = ids[Random.Range(0, ids.Count)];
        OwnedTraitManager.Instance.AddTrait(id, save: true);
        cache.TryGetValue(id, out var row);
        Debug.Log($"[Shop] 특성 가챠 → {row?.name ?? id} ({row?.grade})");

        if (resultPanel != null) resultPanel.ShowTrait(row);
    }
}
