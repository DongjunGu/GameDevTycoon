using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 직원 카드 합성 UI
// 슬롯: 메인 1 + 재료 2 (재료 슬롯 사용 개수는 메인 카드 grade/stage에 따라 자동 결정)
//
// 합성 규칙 (메인 → 결과):
//   Normal s0  → Rare s0          | 같은 직원 Normal s0 × 1
//   Rare s0    → Epic s0           | 같은 직원 Rare s0 × 1
//   Epic s0    → Epic s1 (1단계)    | 아무 Epic s0 × 2
//   Epic s1    → Unique s0          | 같은 직원 Epic s1 × 1
//   Unique s0  → Unique s1 (1단계)  | 아무 Unique s0 × 2
//   Unique s1  → Legendary s0       | 같은 직원 Unique s1 × 1
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
    }

    void ResetSlots()
    {
        mainSlot?.Clear();
        mat1Slot?.Clear();
        mat2Slot?.Clear();
        resultSlot?.Clear();
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
        if (item == null) return;
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
            return;
        }

        if (TryGetRecipe(out var r))
        {
            SetSlotActive(mat1Slot, r.MatCount >= 1);
            SetSlotActive(mat2Slot, r.MatCount >= 2);
            // 결과 미리보기는 메인 직원 기준
            var master = FindMaster(mainSlot.EmployeeId);
            resultSlot?.SetPreviewCard(mainSlot.EmployeeId, r.OutGrade, r.OutStage, master);
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
            if (containerUI != null) containerUI.ClearMergeHighlight();
            if (filterUI != null) filterUI.SetInteractable(true);
        }
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
        var g = mainSlot.Grade; var s = mainSlot.Stage;

        if (g == EmployeeGrade.Normal && s == 0) { r = new Recipe(EmployeeGrade.Rare, 0,      1, true,  EmployeeGrade.Normal, 0); return true; }
        if (g == EmployeeGrade.Rare   && s == 0) { r = new Recipe(EmployeeGrade.Epic, 0,      1, true,  EmployeeGrade.Rare,   0); return true; }
        if (g == EmployeeGrade.Epic   && s == 0) { r = new Recipe(EmployeeGrade.Epic, 1,      2, false, EmployeeGrade.Epic,   0); return true; }
        if (g == EmployeeGrade.Epic   && s == 1) { r = new Recipe(EmployeeGrade.Unique, 0,    1, true,  EmployeeGrade.Epic,   1); return true; }
        if (g == EmployeeGrade.Unique && s == 0) { r = new Recipe(EmployeeGrade.Unique, 1,    2, false, EmployeeGrade.Unique, 0); return true; }
        if (g == EmployeeGrade.Unique && s == 1) { r = new Recipe(EmployeeGrade.Legendary, 0, 1, true,  EmployeeGrade.Unique, 1); return true; }
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
            if (hasRecipe)
            {
                string name = GradeName(r.OutGrade) + (r.OutStage > 0 ? $" +{r.OutStage}" : "");
                resultLabel.text = ok ? $"결과: {name}" : $"필요: {GradeName(r.MatGrade)}{(r.MatStage > 0 ? $" +{r.MatStage}" : "")} {r.MatCount}장{(r.SameEmp ? " (같은 직원)" : "")}";
            }
            else resultLabel.text = "";
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

    static string GradeName(EmployeeGrade g) => g switch
    {
        EmployeeGrade.Normal    => "노말",
        EmployeeGrade.Rare      => "레어",
        EmployeeGrade.Epic      => "에픽",
        EmployeeGrade.Unique    => "유니크",
        EmployeeGrade.Legendary => "레전더리",
        _                       => g.ToString()
    };

    readonly struct Recipe
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
