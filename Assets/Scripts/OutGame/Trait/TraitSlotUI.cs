using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 좌측 "선택된 특성" 장착 슬롯
// 비어있을 때: "+" placeholder / 채워져있을 때: 특성 이름 + 등급 색상
// 잠금 여부는 OwnedTraitManager.UnlockedSlotCount 가 단일 소스 (기본 2슬롯)
//   잠긴 슬롯: 배경 lockedColor + 해금 비용 텍스트. 클릭하면 패널이 해금을 시도한다
// 클릭: 해금된 슬롯이면 장착 해제 (비어있으면 무동작)
public class TraitSlotUI : MonoBehaviour
{
    [Header("References")]
    public Image gradeBackground;
    public TMP_Text nameText;
    public GameObject emptyPlaceholder; // "+" 표시 (선택)
    public GameObject filledContent;    // 이름 등 채워졌을 때 보일 영역 (선택)
    public Button button;

    [Header("Lock")]
    [Tooltip("(구) 검은 덮개. 비용 텍스트를 가리므로 항상 비활성으로 강제된다")]
    public GameObject lockedVeil;
    [Tooltip("잠긴 슬롯 배경색")]
    public Color lockedColor = new(0f, 0f, 0f, 0.961f);
    [Tooltip("잠긴 슬롯 텍스트 포맷. {0} = 해금 비용(다이아)")]
    public string lockedFormat = "잠김\n다이아 {0:N0}";

    [Header("Empty Visual")]
    public Color emptyColor = new(1f, 1f, 1f, 0.15f);

    public int SlotIndex { get; private set; } = -1;

    public event Action<int> OnClicked; // slotIndex

    // 잠긴 슬롯인지 (해금 슬롯 수보다 뒤면 잠김)
    public bool IsLocked
    {
        get
        {
            var mgr = OwnedTraitManager.Instance;
            return SlotIndex >= 0 && mgr != null && !mgr.IsSlotUnlocked(SlotIndex);
        }
    }

    public void Bind(int slotIndex)
    {
        SlotIndex = slotIndex;
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnClicked?.Invoke(SlotIndex));
        }
        Refresh();
    }

    public void Refresh()
    {
        // veil 은 비용 텍스트를 덮으므로 사용하지 않는다 (씬에 남아있어도 강제 비활성)
        if (lockedVeil != null) lockedVeil.SetActive(false);
        // 잠긴 슬롯도 클릭 가능 — 클릭 시 해금 시도
        if (button != null) button.interactable = true;

        if (IsLocked)
        {
            int cost = OwnedTraitManager.Instance.GetSlotUnlockCost(SlotIndex);
            if (emptyPlaceholder != null) emptyPlaceholder.SetActive(false);
            if (filledContent != null)    filledContent.SetActive(true);
            if (gradeBackground != null)  gradeBackground.color = lockedColor;
            if (nameText != null)         nameText.text = string.Format(lockedFormat, cost);
            return;
        }

        string traitId = OwnedTraitManager.Instance?.GetEquipped(SlotIndex);
        bool filled = !string.IsNullOrEmpty(traitId);

        if (emptyPlaceholder != null) emptyPlaceholder.SetActive(!filled);
        if (filledContent != null)    filledContent.SetActive(filled);

        if (!filled)
        {
            if (gradeBackground != null) gradeBackground.color = emptyColor;
            if (nameText != null) nameText.text = string.Empty;
            return;
        }

        var cache = TraitChartLoader.Cache;
        if (cache != null && cache.TryGetValue(traitId, out var row))
        {
            if (gradeBackground != null) gradeBackground.color = TraitGradeColors.Get(row.grade);
            if (nameText != null) nameText.text = row.name;
        }
        else
        {
            if (gradeBackground != null) gradeBackground.color = emptyColor;
            if (nameText != null) nameText.text = traitId;
        }
    }
}
