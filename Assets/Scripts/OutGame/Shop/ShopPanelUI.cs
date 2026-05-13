using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 상점 패널 — 5 카테고리 (기간한정/뽑기/다이아/골드/패키지). 좌측 카테고리 바, 우측 카테고리별 컨텐츠.
// 현재는 뽑기 카테고리만 구현 (직원 일반/스페셜, 특성 일반/스페셜/프리미엄 = 5 버튼).
// 5 버튼 모두 임시로 동일 기능 — GachaTestUI / TraitGachaTestUI 와 같은 1회 무료 가챠.
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
    public Button traitNormalBtn;
    public Button traitSpecialBtn;
    public Button traitPremiumBtn;

    [Header("Result")]
    public ShopGachaResultPanelUI resultPanel;

    [Header("Employee Gacha Weights (임시 — 모든 직원 가챠 공통)")]
    [Range(0, 100)] public int weightNormal = 60;
    [Range(0, 100)] public int weightRare   = 30;
    [Range(0, 100)] public int weightEpic   = 10;

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
        WireBtn(employeeNormalBtn,  OnEmployeeGacha);
        WireBtn(employeeSpecialBtn, OnEmployeeGacha);
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
    void OnEmployeeGacha()
    {
        var pool = EmployeeManager.Instance?.poolEmployees;
        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning("[Shop] 직원 pool 비어있음");
            return;
        }
        if (OwnedCardManager.Instance == null) return;

        var emp   = pool[Random.Range(0, pool.Count)];
        var grade = RollEmployeeGrade();
        OwnedCardManager.Instance.AddCard(emp.id, grade, stage: 0, save: true);
        Debug.Log($"[Shop] 직원 가챠 → {emp.employeeName} ({grade})");

        if (resultPanel != null) resultPanel.ShowEmployee(emp, grade);
    }

    EmployeeGrade RollEmployeeGrade()
    {
        int total = Mathf.Max(1, weightNormal + weightRare + weightEpic);
        int r = Random.Range(0, total);
        if (r < weightNormal) return EmployeeGrade.Normal;
        if (r < weightNormal + weightRare) return EmployeeGrade.Rare;
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
