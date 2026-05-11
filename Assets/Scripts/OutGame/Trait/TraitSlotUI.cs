using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 좌측 "선택된 특성" 장착 슬롯
// 비어있을 때: "+" placeholder / 채워져있을 때: 특성 이름 + 등급 색상
// 잠금 시(Slot4/Slot5 — 다이아 해금 예정): lockedVeil 활성, 버튼 비활성
// 클릭: 장착 해제 (비어있으면 무동작)
public class TraitSlotUI : MonoBehaviour
{
    [Header("References")]
    public Image gradeBackground;
    public TMP_Text nameText;
    public GameObject emptyPlaceholder; // "+" 표시 (선택)
    public GameObject filledContent;    // 이름 등 채워졌을 때 보일 영역 (선택)
    public Button button;

    [Header("Lock")]
    [Tooltip("잠긴 슬롯 (다이아 해금 예정). lockedVeil 활성 + 버튼 비활성")]
    public bool isLocked;
    public GameObject lockedVeil;

    [Header("Empty Visual")]
    public Color emptyColor = new(1f, 1f, 1f, 0.15f);

    public int SlotIndex { get; private set; } = -1;

    public event Action<int> OnClicked; // slotIndex

    void Awake() => ApplyLockState();

    public void SetLocked(bool locked)
    {
        isLocked = locked;
        ApplyLockState();
        Refresh();
    }

    void ApplyLockState()
    {
        if (lockedVeil != null) lockedVeil.SetActive(isLocked);
        if (button != null) button.interactable = !isLocked;
    }

    public void Bind(int slotIndex)
    {
        SlotIndex = slotIndex;
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnClicked?.Invoke(SlotIndex));
        }
        ApplyLockState();
        Refresh();
    }

    public void Refresh()
    {
        ApplyLockState();
        if (isLocked)
        {
            if (emptyPlaceholder != null) emptyPlaceholder.SetActive(false);
            if (filledContent != null)    filledContent.SetActive(false);
            if (gradeBackground != null)  gradeBackground.color = emptyColor;
            if (nameText != null)         nameText.text = string.Empty;
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
