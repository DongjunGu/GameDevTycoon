using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 직원 카드 합성 UI
// 슬롯: 메인 1 + 재료 2 (재료 슬롯 사용 개수는 메인 카드 grade/stage에 따라 자동 결정)
//
// 합성 규칙 (메인 → 결과):
//   Normal s0  → Rare s0            | 같은 직원 Normal s0 × 2  (메인 포함 총 3장)
//   Rare s0    → Epic s0            | 같은 직원 Rare s0 × 2    (메인 포함 총 3장)
//   Epic s0    → Epic s1 (1단계)     | 아무 직원 Epic s0 × 1
//   Epic s1    → Epic s2 (2단계)     | 아무 직원 Epic s1 × 1
//   Epic s2    → Unique s0          | 같은 직원 Epic s2 × 1    (메인 포함 총 2장)
//   Unique s0  → Unique s1 (1단계)   | 아무 직원 Unique s0 × 1
//   Unique s1  → Unique s2 (2단계)   | 아무 직원 Unique s1 × 1
//   Unique s2  → Legendary s0       | 같은 직원 Unique s2 × 1  (메인 포함 총 2장)
//
// 등급 승급(Rare/Epic/Unique/Legendary)은 같은 직원 카드만 재료로 쓸 수 있고,
// 단계 승급(s0→s1→s2)은 같은 등급/단계면 아무 직원 카드나 재료로 쓸 수 있다.
public class EmployeeMergeUI : MonoBehaviour
{
    [Header("References")]
    public OwnedCardContainerUI containerUI;
    [Tooltip("합성 진행 중(메인 슬롯 채워짐) 비활성화할 필터 UI")]
    public OwnedCardFilterUI filterUI;
    public MergeSlotUI mainSlot;
    public MergeSlotUI mat1Slot;
    public MergeSlotUI mat2Slot;
    [Tooltip("결과 미리보기 슬롯 — 메인이 채워지면 결과 카드 표시 (실제 차감/추가 X)")]
    public MergeSlotUI resultSlot;

    [Header("Merge Button")]
    public Button mergeButton;
    public TMP_Text mergeButtonLabel;

    [Header("Result Preview (선택)")]
    public TMP_Text resultLabel;
    [Tooltip("ResultSlot/PortraitDescription/PortraitName — 결과 직원 이름")]
    public TMP_Text resultNameText;
    [Tooltip("ResultSlot/PortraitDescription/PortraitDescText — 합성 후 얻는 능력")]
    public TMP_Text resultDescText;

    [Header("일괄 합성")]
    public Button mergeOnceButton;
    [Tooltip("일괄 합성 결과 팝업 루트 (기본 비활성)")]
    public GameObject mergeOncePanel;
    [Tooltip("결과 카드가 배치될 부모 (GridLayoutGroup)")]
    public Transform mergeOnceContent;
    [Tooltip("결과 카드 프리팹 — ItemPrefabOwnedCard")]
    public GameObject mergeOnceItemPrefab;
    [Tooltip("합성할 카드가 없을 때 표시")]
    public GameObject mergeOnceEmptyText;
    public Button mergeOnceConfirmButton;

    const string Red = "<color=red>";

    void Awake()
    {
        if (mainSlot != null) mainSlot.OnClicked += HandleSlotClicked;
        if (mat1Slot != null) mat1Slot.OnClicked += HandleSlotClicked;
        if (mat2Slot != null) mat2Slot.OnClicked += HandleSlotClicked;
        if (mergeButton != null)
        {
            mergeButton.onClick.RemoveAllListeners();
            mergeButton.onClick.AddListener(TryMerge);
        }
        if (mergeOnceButton != null)
        {
            mergeOnceButton.onClick.RemoveAllListeners();
            mergeOnceButton.onClick.AddListener(MergeOnce);
        }
        if (mergeOnceConfirmButton != null)
        {
            mergeOnceConfirmButton.onClick.RemoveAllListeners();
            mergeOnceConfirmButton.onClick.AddListener(CloseMergeOncePanel);
        }
        CloseMergeOncePanel();
    }

    // 일괄 합성 — 슬롯 예약 해제 후 보유 카드 전체를 합성하고 새로 생긴 카드를 팝업에 표시
    void MergeOnce()
    {
        var mgr = OwnedCardManager.Instance;
        if (mgr == null) return;

        ResetSlots();
        var gained = mgr.MergeAll();
        UpdateButton();

        if (mergeOnceContent != null)
        {
            for (int i = mergeOnceContent.childCount - 1; i >= 0; i--)
                Destroy(mergeOnceContent.GetChild(i).gameObject);

            var keys = new System.Collections.Generic.List<string>(gained.Keys);
            keys.Sort((a, b) =>
            {
                OwnedCardManager.ParseKey(a, out var ea, out var ga, out var sa);
                OwnedCardManager.ParseKey(b, out var eb, out var gb, out var sb);
                int c = ((int)gb).CompareTo((int)ga);
                if (c != 0) return c;
                c = sb.CompareTo(sa);
                return c != 0 ? c : string.CompareOrdinal(ea, eb);
            });

            if (mergeOnceItemPrefab != null)
                foreach (var key in keys)
                {
                    OwnedCardManager.ParseKey(key, out var e, out var g, out var s);
                    var master = FindMaster(e);
                    for (int i = 0; i < gained[key]; i++)
                    {
                        var item = Instantiate(mergeOnceItemPrefab, mergeOnceContent).GetComponent<OwnedCardItemUI>();
                        if (item != null) item.SetData(e, g, s, master);
                    }
                }
        }
        if (mergeOnceEmptyText != null) mergeOnceEmptyText.SetActive(gained.Count == 0);
        if (mergeOncePanel != null) mergeOncePanel.SetActive(true);
    }

    void CloseMergeOncePanel()
    {
        if (mergeOncePanel != null) mergeOncePanel.SetActive(false);
    }

    void OnEnable()
    {
        if (containerUI != null) containerUI.OnCardClicked += HandleCardClicked;
        ResetSlots();
        UpdateButton();
    }

    void OnDisable()
    {
        if (containerUI != null) containerUI.OnCardClicked -= HandleCardClicked;
        ResetSlots();
        CloseMergeOncePanel();
    }

    void ResetSlots()
    {
        mainSlot?.Clear();
        mat1Slot?.Clear();
        mat2Slot?.Clear();
        resultSlot?.Clear();
        SetResultDescription(null, default);
        // 기본: main + result만 활성, mat1/mat2 비활성. 메인 채워지면 OnMainChanged에서 활성화.
        SetSlotActive(mat1Slot, false);
        SetSlotActive(mat2Slot, false);
        SetSlotActive(mainSlot, true);
        SetSlotActive(resultSlot, true);
        if (containerUI != null) containerUI.ClearMergeHighlight();
        if (filterUI != null) filterUI.SetInteractable(true);
    }

    static void SetSlotActive(MergeSlotUI slot, bool on)
    {
        if (slot != null && slot.gameObject.activeSelf != on)
            slot.gameObject.SetActive(on);
    }

    void HandleCardClicked(OwnedCardItemUI item)
    {
        if (item == null || item.IsSelected) return;
        // 빈 슬롯 우선순위: main → mat1(active) → mat2(active)
        MergeSlotUI target = null;
        if (mainSlot != null && mainSlot.IsEmpty) target = mainSlot;
        else if (mat1Slot != null && mat1Slot.gameObject.activeSelf && mat1Slot.IsEmpty) target = mat1Slot;
        else if (mat2Slot != null && mat2Slot.gameObject.activeSelf && mat2Slot.IsEmpty) target = mat2Slot;
        if (target == null) return;

        var master = FindMaster(item.EmployeeId);
        target.SetCard(item, master);

        if (target == mainSlot) OnMainChanged();
        UpdateButton();
        if (containerUI != null) containerUI.ScrollToTop();
    }

    void HandleSlotClicked(MergeSlotUI slot)
    {
        if (slot == null) return;
        // 메인 비우면 재료도 같이 비움 + 재료/결과 슬롯 비활성
        if (slot == mainSlot)
        {
            ResetSlots();
        }
        else
        {
            slot.Clear();
        }
        UpdateButton();
    }

    // 메인 슬롯 카드가 새로 들어왔을 때 — 재료 슬롯 활성/비활성 + 결과 미리보기 갱신
    void OnMainChanged()
    {
        if (mainSlot == null || mainSlot.IsEmpty)
        {
            SetSlotActive(mat1Slot, false);
            SetSlotActive(mat2Slot, false);
            resultSlot?.Clear();
            SetResultDescription(null, default);
            return;
        }

        if (TryGetRecipe(out var r))
        {
            SetSlotActive(mat1Slot, r.MatCount >= 1);
            SetSlotActive(mat2Slot, r.MatCount >= 2);
            // 결과 미리보기는 메인 직원 기준
            var master = FindMaster(mainSlot.EmployeeId);
            resultSlot?.SetPreviewCard(mainSlot.EmployeeId, r.OutGrade, r.OutStage, master);
            SetResultDescription(master, r);
            // 풀에서 매칭 카드 sorting + 비매칭 dim
            if (containerUI != null)
                containerUI.ApplyMergeHighlight(r.MatGrade, r.MatStage, r.SameEmp ? mainSlot.EmployeeId : null);
            // 합성 진행 중 필터 잠금
            if (filterUI != null) filterUI.SetInteractable(false);
        }
        else
        {
            // 합성 불가 메인 (Legendary 등) — 재료/결과 모두 비움 + dim 해제 + 필터 활성
            SetSlotActive(mat1Slot, false);
            SetSlotActive(mat2Slot, false);
            resultSlot?.Clear();
            SetResultDescription(null, default);
            if (containerUI != null) containerUI.ClearMergeHighlight();
            if (filterUI != null) filterUI.SetInteractable(true);
        }
    }

    // 결과 미리보기 — 직원 이름 + 합성 후 얻는 능력. emp==null 이면 비움.
    // 마스터 데이터라 grade 가 Normal → 등급 게이팅 없는 AnyGrade 이름 조회 사용
    void SetResultDescription(EmployeeData emp, Recipe r)
    {
        if (resultNameText != null) resultNameText.text = emp != null ? emp.employeeName : "";
        if (resultDescText == null) return;
        if (emp == null) { resultDescText.text = ""; return; }

        resultDescText.text = r.OutGrade switch
        {
            EmployeeGrade.Rare      => $"모든 능력치 {Red}+50</color>",
            EmployeeGrade.Epic      => $"{Red}{CharacterTraitApplier.GetTraitNameAnyGrade(emp)}</color> " + (r.OutStage > 0 ? "강화" : "특성 획득"),
            EmployeeGrade.Unique    => $"{Red}{CharacterUniqueEvents.GetEventNameAnyGrade(emp)}</color> " + (r.OutStage > 0 ? "강화" : "획득"),
            EmployeeGrade.Legendary => $"20성 달성 시 추가 능력치 {Red}+10%</color>",
            _                       => "",
        };
    }

    EmployeeData FindMaster(string empId)
    {
        if (EmployeeManager.Instance?.poolEmployees == null) return null;
        foreach (var e in EmployeeManager.Instance.poolEmployees)
            if (e != null && e.id == empId) return e;
        return null;
    }

    bool TryGetRecipe(out Recipe r)
    {
        r = default;
        if (mainSlot == null || mainSlot.IsEmpty) return false;
        return TryGetRecipe(mainSlot.Grade, mainSlot.Stage, out r);
    }

    // 합성표 단일 소스 — 메인 grade/stage 기준. 재료 grade/stage 는 항상 메인과 같다. (OwnedCardManager.MergeAll 도 사용)
    public static bool TryGetRecipe(EmployeeGrade g, int s, out Recipe r)
    {
        r = default;

        //                                                     결과등급              결과s  재료수  같은직원  재료등급              재료s
        if (g == EmployeeGrade.Normal && s == 0) { r = new Recipe(EmployeeGrade.Rare,      0,     2,   true,  EmployeeGrade.Normal, 0); return true; }
        if (g == EmployeeGrade.Rare   && s == 0) { r = new Recipe(EmployeeGrade.Epic,      0,     2,   true,  EmployeeGrade.Rare,   0); return true; }
        if (g == EmployeeGrade.Epic   && s == 0) { r = new Recipe(EmployeeGrade.Epic,      1,     1,   false, EmployeeGrade.Epic,   0); return true; }
        if (g == EmployeeGrade.Epic   && s == 1) { r = new Recipe(EmployeeGrade.Epic,      2,     1,   false, EmployeeGrade.Epic,   1); return true; }
        if (g == EmployeeGrade.Epic   && s == 2) { r = new Recipe(EmployeeGrade.Unique,    0,     1,   true,  EmployeeGrade.Epic,   2); return true; }
        if (g == EmployeeGrade.Unique && s == 0) { r = new Recipe(EmployeeGrade.Unique,    1,     1,   false, EmployeeGrade.Unique, 0); return true; }
        if (g == EmployeeGrade.Unique && s == 1) { r = new Recipe(EmployeeGrade.Unique,    2,     1,   false, EmployeeGrade.Unique, 1); return true; }
        if (g == EmployeeGrade.Unique && s == 2) { r = new Recipe(EmployeeGrade.Legendary, 0,     1,   true,  EmployeeGrade.Unique, 2); return true; }
        return false;
    }

    bool ValidateMaterials(Recipe r)
    {
        var slots = new[] { mat1Slot, mat2Slot };
        for (int i = 0; i < r.MatCount; i++)
        {
            var s = slots[i];
            if (s == null || s.IsEmpty) return false;
            if (s.Grade != r.MatGrade || s.Stage != r.MatStage) return false;
            if (r.SameEmp && s.EmployeeId != mainSlot.EmployeeId) return false;
        }
        // 초과 슬롯이 채워져 있으면 불가
        for (int i = r.MatCount; i < slots.Length; i++)
            if (slots[i] != null && !slots[i].IsEmpty) return false;
        return true;
    }

    void UpdateButton()
    {
        bool hasRecipe = TryGetRecipe(out var r);
        bool ok = hasRecipe && ValidateMaterials(r);

        if (mergeButton != null) mergeButton.interactable = ok;
        if (mergeButtonLabel != null) mergeButtonLabel.text = ok ? "합성하기" : "합성하기";

        if (resultLabel != null)
        {
            // 합성표 표기: 같은 직원 = 메인 포함 총 장수, 아무 직원 = 재료 장수
            if (!hasRecipe) resultLabel.text = "";
            else if (ok) resultLabel.text = $"결과: {CardName(r.OutGrade, r.OutStage)}";
            else if (r.SameEmp) resultLabel.text = $"필요: 같은 직원 {CardName(r.MatGrade, r.MatStage)} 총 {r.MatCount + 1}장";
            else resultLabel.text = $"필요: 아무 직원 {CardName(r.MatGrade, r.MatStage)} {r.MatCount}장";
        }
    }

    void TryMerge()
    {
        if (!TryGetRecipe(out var r)) return;
        if (!ValidateMaterials(r)) return;

        var mgr = OwnedCardManager.Instance;
        if (mgr == null) return;

        // 메인 카드 차감 (재료 슬롯들과 같은 (e,g,s)일 수 있어도 RemoveCard는 1장씩 처리)
        mgr.RemoveCard(mainSlot.EmployeeId, mainSlot.Grade, mainSlot.Stage, 1, save: false);
        if (r.MatCount >= 1) mgr.RemoveCard(mat1Slot.EmployeeId, mat1Slot.Grade, mat1Slot.Stage, 1, save: false);
        if (r.MatCount >= 2) mgr.RemoveCard(mat2Slot.EmployeeId, mat2Slot.Grade, mat2Slot.Stage, 1, save: false);

        // 결과 카드 추가 (메인 직원의 새 grade/stage)
        mgr.AddCard(mainSlot.EmployeeId, r.OutGrade, r.OutStage, save: true);

        // OnChanged → Container Rebuild → source GameObject들이 destroy됨. ResetSlots는 Clear 호출만.
        ResetSlots();
        UpdateButton();
    }

    static string CardName(EmployeeGrade g, int stage) => stage > 0 ? $"{GradeName(g)} {stage}단계" : GradeName(g);

    static string GradeName(EmployeeGrade g) => g switch
    {
        EmployeeGrade.Normal    => "노말",
        EmployeeGrade.Rare      => "레어",
        EmployeeGrade.Epic      => "에픽",
        EmployeeGrade.Unique    => "유니크",
        EmployeeGrade.Legendary => "레전더리",
        _                       => g.ToString()
    };

    public readonly struct Recipe
    {
        public readonly EmployeeGrade OutGrade;
        public readonly int OutStage;
        public readonly int MatCount;
        public readonly bool SameEmp;
        public readonly EmployeeGrade MatGrade;
        public readonly int MatStage;

        public Recipe(EmployeeGrade outGrade, int outStage, int matCount, bool sameEmp, EmployeeGrade matGrade, int matStage)
        {
            OutGrade = outGrade; OutStage = outStage; MatCount = matCount;
            SameEmp = sameEmp; MatGrade = matGrade; MatStage = matStage;
        }
    }
}
