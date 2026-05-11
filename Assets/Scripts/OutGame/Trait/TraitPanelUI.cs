using System.Collections.Generic;
using TMPro;
using UnityEngine;

// TraitPanel 루트 컨트롤러
// - 좌측 장착 슬롯 3개 갱신
// - 우측 보유 특성을 등급별(S/A/B/C) 섹션 그리드에 빌드
//   - 모든 특성 표시 (미보유는 TraitItemUI.lockedVeil 활성화)
// - "선택된 특성 (n/3)" 카운트 텍스트 갱신
// - OwnedTraitManager.OnChanged 구독해서 자동 갱신
public class TraitPanelUI : MonoBehaviour
{
    [Header("Equipped Slots (좌측)")]
    [Tooltip("길이는 OwnedTraitManager.EquipSlotCount (3) 와 동일해야 함")]
    public TraitSlotUI[] slots;
    public TMP_Text equippedCountText; // 예: "선택된 특성 (2/3)"

    [Header("Grade Sections (S/A/B/C — 우측)")]
    [Tooltip("등급별 섹션 루트 (헤더+그리드 묶음). 해당 등급에 특성이 없으면 비활성화")]
    public GameObject sectionRootS;
    public GameObject sectionRootA;
    public GameObject sectionRootB;
    public GameObject sectionRootC;

    [Tooltip("등급별 카드 컨테이너 (GridLayoutGroup, 3열 권장)")]
    public Transform gridContainerS;
    public Transform gridContainerA;
    public Transform gridContainerB;
    public Transform gridContainerC;

    [Header("Item Prefab")]
    public TraitItemUI itemPrefab;

    // grade → pool (재사용용)
    private readonly Dictionary<TraitGrade, List<TraitItemUI>> _pools = new()
    {
        { TraitGrade.S, new List<TraitItemUI>() },
        { TraitGrade.A, new List<TraitItemUI>() },
        { TraitGrade.B, new List<TraitItemUI>() },
        { TraitGrade.C, new List<TraitItemUI>() },
    };

    private bool _subscribed;

    void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void Subscribe()
    {
        if (_subscribed || OwnedTraitManager.Instance == null) return;
        OwnedTraitManager.Instance.OnChanged += Refresh;
        _subscribed = true;
    }

    void Unsubscribe()
    {
        if (!_subscribed || OwnedTraitManager.Instance == null) return;
        OwnedTraitManager.Instance.OnChanged -= Refresh;
        _subscribed = false;
    }

    public void Refresh()
    {
        BindSlots();
        BuildSections();
        UpdateCountText();
    }

    void BindSlots()
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            slots[i].Bind(i);
            slots[i].OnClicked -= OnSlotClicked;
            slots[i].OnClicked += OnSlotClicked;
            slots[i].Refresh();
        }
    }

    void OnSlotClicked(int slotIndex)
    {
        OwnedTraitManager.Instance?.UnequipSlot(slotIndex);
    }

    void BuildSections()
    {
        if (itemPrefab == null || OwnedTraitManager.Instance == null) return;

        // 등급별 분류 (이름 오름차순)
        var byGrade = new Dictionary<TraitGrade, List<TraitChartRow>>
        {
            { TraitGrade.S, new List<TraitChartRow>() },
            { TraitGrade.A, new List<TraitChartRow>() },
            { TraitGrade.B, new List<TraitChartRow>() },
            { TraitGrade.C, new List<TraitChartRow>() },
        };
        var cache = TraitChartLoader.Cache;
        if (cache != null)
        {
            foreach (var kv in cache)
            {
                if (kv.Value == null) continue;
                byGrade[kv.Value.grade].Add(kv.Value);
            }
        }
        foreach (var list in byGrade.Values)
            list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

        BuildSection(TraitGrade.S, sectionRootS, gridContainerS, byGrade[TraitGrade.S]);
        BuildSection(TraitGrade.A, sectionRootA, gridContainerA, byGrade[TraitGrade.A]);
        BuildSection(TraitGrade.B, sectionRootB, gridContainerB, byGrade[TraitGrade.B]);
        BuildSection(TraitGrade.C, sectionRootC, gridContainerC, byGrade[TraitGrade.C]);
    }

    void BuildSection(TraitGrade grade, GameObject root, Transform container, List<TraitChartRow> rows)
    {
        // 빈 등급은 섹션 비활성화
        bool hasItems = rows != null && rows.Count > 0;
        if (root != null) root.SetActive(hasItems);
        if (container == null || !hasItems) return;

        var pool = _pools[grade];
        // 부족분 Instantiate
        for (int i = pool.Count; i < rows.Count; i++)
        {
            var item = Instantiate(itemPrefab, container);
            item.OnClicked += OnItemClicked;
            pool.Add(item);
        }
        // 채우기 / 남는 건 비활성화
        for (int i = 0; i < pool.Count; i++)
        {
            if (i < rows.Count)
            {
                var row = rows[i];
                pool[i].gameObject.SetActive(true);
                pool[i].SetData(row, OwnedTraitManager.Instance.IsOwned(row.traitId));
            }
            else
            {
                pool[i].gameObject.SetActive(false);
            }
        }
    }

    void OnItemClicked(TraitItemUI item)
    {
        if (item == null || item.Data == null || OwnedTraitManager.Instance == null) return;
        if (!item.IsOwned) return; // 미보유는 무동작

        var mgr = OwnedTraitManager.Instance;
        var id = item.Data.traitId;

        if (mgr.IsEquipped(id)) mgr.Unequip(id);
        else if (!mgr.TryEquip(id))
            Debug.Log("[TraitPanel] 빈 슬롯 없음 — 기존 특성 해제 후 다시 시도");
    }

    void UpdateCountText()
    {
        if (equippedCountText == null || OwnedTraitManager.Instance == null) return;
        int n = 0;
        for (int i = 0; i < OwnedTraitManager.EquipSlotCount; i++)
            if (!string.IsNullOrEmpty(OwnedTraitManager.Instance.GetEquipped(i))) n++;
        equippedCountText.text = $"선택된 특성 ({n}/{OwnedTraitManager.EquipSlotCount})";
    }
}
