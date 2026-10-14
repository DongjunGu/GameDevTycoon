using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 상점 패널 — 5 카테고리 (기간한정/뽑기/다이아/골드/패키지). 좌측 카테고리 바, 우측 카테고리별 컨텐츠.
// 현재는 뽑기 카테고리만 구현 — 직원 일반/스페셜, 특성 일반/스페셜 각각 1회/10회 (다이아).
// 등급을 가중치로 먼저 뽑고, 같은 등급 안에서는 균등. 스페셜은 천장(직원 10회=에픽, 특성 30회=S), 당첨 시 카운트 초기화.
public class ShopPanelUI : MonoBehaviour
{
    public enum Category { LimitedTime = 0, Gacha = 1, Diamond = 2, Gold = 3, Package = 4 }

    [Header("Category Buttons (인덱스 = Category enum)")]
    public Button[] categoryButtons = new Button[5];
    [Tooltip("선택 시 켜질 노란 강조 GameObject — 각 버튼별.")]
    public GameObject[] categoryHighlights = new GameObject[5];
    [Tooltip("선택 시 활성화될 우측 컨텐츠 — 각 카테고리별.")]
    public GameObject[] categoryContents = new GameObject[5];

    [Tooltip("선택된 카테고리 버튼 프레임 (Shop_Frame_Selected). 선택 시 버튼 자식 아이콘·텍스트는 흰색.")]
    public Sprite categorySelectedSprite;

    [Header("Default")]
    public Category defaultCategory = Category.Gacha;

    // 선택 해제 시 되돌릴 원래 프레임/색 (최초 1회 캐시)
    readonly Dictionary<Image, Sprite> _defaultFrames = new();
    readonly Dictionary<Graphic, Color> _defaultColors = new();

    [Header("Gacha Buttons (뽑기 카테고리)")]
    public Button employeeNormalBtn;
    public Button employeeSpecialBtn;
    [Tooltip("10연차 버튼 — 비워두면 해당 10연차 없음.")]
    public Button employeeNormal10Btn;
    public Button employeeSpecial10Btn;
    public Button traitNormalBtn;
    public Button traitSpecialBtn;
    public Button traitNormal10Btn;
    public Button traitSpecial10Btn;

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

    [Header("Employee Gacha Pity — 스페셜 (N회째 에픽 확정, 에픽 당첨 시 초기화)")]
    public int employeeSpecialPity = 10;

    [Header("Trait Gacha Cost (다이아)")]
    public int traitNormalCost    = 100;
    public int traitNormal10Cost  = 1000;
    public int traitSpecialCost   = 400;
    public int traitSpecial10Cost = 4000;

    [Header("Trait Gacha Weights — 일반 (S/A/B/C)")]
    public int traitNormalWeightS = 0;
    public int traitNormalWeightA = 5;
    public int traitNormalWeightB = 30;
    public int traitNormalWeightC = 65;

    [Header("Trait Gacha Weights — 스페셜 (S/A/B/C)")]
    public int traitSpecialWeightS = 3;
    public int traitSpecialWeightA = 25;
    public int traitSpecialWeightB = 45;
    public int traitSpecialWeightC = 27;

    [Header("Trait Gacha Pity — 스페셜 (N회째 S 확정, S 당첨 시 초기화)")]
    public int traitSpecialPity = 30;

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
        WireBtn(traitNormalBtn,      () => OnTraitGacha(false, 1));
        WireBtn(traitSpecialBtn,     () => OnTraitGacha(true,  1));
        WireBtn(traitNormal10Btn,    () => OnTraitGacha(false, 10));
        WireBtn(traitSpecial10Btn,   () => OnTraitGacha(true,  10));
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
            if (categoryButtons != null && i < categoryButtons.Length && categoryButtons[i] != null)
                SetCategoryVisual(categoryButtons[i], on);
        }
    }

    // 선택: 프레임 교체 + 자식 아이콘·텍스트 흰색 / 해제: 원래 프레임·색 복원
    void SetCategoryVisual(Button button, bool on)
    {
        if (button.TryGetComponent(out Image frame))
        {
            if (!_defaultFrames.ContainsKey(frame)) _defaultFrames[frame] = frame.sprite;
            frame.sprite = on && categorySelectedSprite != null ? categorySelectedSprite : _defaultFrames[frame];
        }
        foreach (var g in button.GetComponentsInChildren<Graphic>(true))
        {
            if (g.gameObject == button.gameObject) continue; // 프레임 자신은 제외
            if (!_defaultColors.ContainsKey(g)) _defaultColors[g] = g.color;
            g.color = on ? Color.white : _defaultColors[g];
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
        if (!CheckDiamond(wallet, cost)) return;

        string label = special ? "스페셜" : "일반";
        var draws = new List<(EmployeeData emp, EmployeeGrade grade)>(count);
        for (int i = 0; i < count; i++)
        {
            var emp   = pool[Random.Range(0, pool.Count)];
            var grade = RollEmployeeGrade(special);
            if (special)
            {
                // 천장: 에픽 없이 (pity-1)회 연속이면 이번 회차는 에픽 확정
                if (grade != EmployeeGrade.Epic && wallet.pityEmployeeSpecial + 1 >= employeeSpecialPity)
                    grade = EmployeeGrade.Epic;
                wallet.pityEmployeeSpecial = grade == EmployeeGrade.Epic ? 0 : wallet.pityEmployeeSpecial + 1;
            }
            OwnedCardManager.Instance.AddCard(emp.id, grade, stage: 0, save: i == count - 1);
            Debug.Log($"[Shop] 직원 가챠({label}) → {emp.employeeName} ({grade})");
            draws.Add((emp, grade));
        }
        wallet.SpendDiamond(cost); // 천장 카운트도 같이 저장
        Debug.Log($"[Shop] 직원 가챠({label}) x{count} -{cost}D (천장 {wallet.pityEmployeeSpecial}/{employeeSpecialPity})");

        ShowDrawsSequential(draws, 0);
    }

    bool CheckDiamond(OutGameCurrencyManager wallet, int cost)
    {
        if (wallet.CanAffordDiamond(cost)) return true;
        // ponytail: 아웃게임 알림 UI 없음. 다이아 부족 팝업 생기면 교체.
        Debug.LogWarning($"[Shop] 다이아 부족 — 필요 {cost}D / 보유 {wallet.Diamond}D");
        return false;
    }

    // 닫기 버튼을 누를 때마다 다음 장. 1회 뽑기도 같은 경로.
    void ShowDrawsSequential(List<(EmployeeData emp, EmployeeGrade grade)> draws, int index)
    {
        if (resultPanel == null || index >= draws.Count) return;
        resultPanel.onClosed = () => ShowDrawsSequential(draws, index + 1);
        resultPanel.ShowEmployee(draws[index].emp, draws[index].grade);
    }

    void ShowTraitsSequential(List<TraitChartRow> draws, int index)
    {
        if (resultPanel == null || index >= draws.Count) return;
        resultPanel.onClosed = () => ShowTraitsSequential(draws, index + 1);
        resultPanel.ShowTrait(draws[index]);
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
    void OnTraitGacha(bool special, int count)
    {
        if (OwnedTraitManager.Instance == null) return;
        var cache = TraitChartLoader.Cache;
        if (cache == null || cache.Count == 0)
        {
            Debug.LogWarning("[Shop] 특성 차트 비어있음");
            return;
        }
        var wallet = OutGameCurrencyManager.Instance;
        if (wallet == null) return;

        int cost = special
            ? (count > 1 ? traitSpecial10Cost : traitSpecialCost)
            : (count > 1 ? traitNormal10Cost  : traitNormalCost);
        if (!CheckDiamond(wallet, cost)) return;

        // 등급별 버킷 (TraitGrade C=0 ~ S=3)
        var byGrade = new List<TraitChartRow>[4];
        for (int g = 0; g < 4; g++) byGrade[g] = new List<TraitChartRow>();
        foreach (var row in cache.Values) byGrade[(int)row.grade].Add(row);

        int[] w = special
            ? new[] { traitSpecialWeightC, traitSpecialWeightB, traitSpecialWeightA, traitSpecialWeightS }
            : new[] { traitNormalWeightC,  traitNormalWeightB,  traitNormalWeightA,  traitNormalWeightS };
        // 차트에 없는 등급은 가중치 0 — 빈 버킷 당첨 방지
        for (int g = 0; g < 4; g++) if (byGrade[g].Count == 0) w[g] = 0;

        int sIdx = (int)TraitGrade.S;
        string label = special ? "스페셜" : "일반";
        var draws = new List<TraitChartRow>(count);
        for (int i = 0; i < count; i++)
        {
            int grade = RollIndex(w);
            if (special)
            {
                // 천장: S 없이 (pity-1)회 연속이면 이번 회차는 S 확정
                if (grade != sIdx && byGrade[sIdx].Count > 0 && wallet.pityTraitSpecial + 1 >= traitSpecialPity)
                    grade = sIdx;
                wallet.pityTraitSpecial = grade == sIdx ? 0 : wallet.pityTraitSpecial + 1;
            }
            var bucket = byGrade[grade];
            if (bucket.Count == 0) continue; // 가중치 전부 0인 차트 — 방어
            var pick = bucket[Random.Range(0, bucket.Count)];
            OwnedTraitManager.Instance.AddTrait(pick.traitId);
            Debug.Log($"[Shop] 특성 가챠({label}) → {pick.name} ({pick.grade})");
            draws.Add(pick);
        }
        wallet.SpendDiamond(cost); // 천장 카운트도 같이 저장
        Debug.Log($"[Shop] 특성 가챠({label}) x{count} -{cost}D (천장 {wallet.pityTraitSpecial}/{traitSpecialPity})");

        ShowTraitsSequential(draws, 0);
    }

    // 가중치 비례 추첨 → 인덱스
    static int RollIndex(int[] weights)
    {
        int total = 0;
        foreach (var x in weights) total += Mathf.Max(0, x);
        if (total <= 0) return 0;
        int r = Random.Range(0, total);
        for (int i = 0; i < weights.Length; i++)
        {
            r -= Mathf.Max(0, weights[i]);
            if (r < 0) return i;
        }
        return weights.Length - 1;
    }
}
