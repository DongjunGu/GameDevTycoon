using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 좌측 "선택된 특성" 장착 슬롯
// 비어있을 때: "+" placeholder / 채워져있을 때: 특성 이름 + 등급 색상
// 잠금 여부는 OwnedTraitManager.UnlockedSlotCount 가 단일 소스 (기본 3슬롯)
//   잠긴 슬롯: 배경 lockedSprite(없으면 lockedColor) +(다음 해금 대상이면 ValuePanel 비용, 아니면 LockImage). 클릭하면 패널이 해금을 시도한다
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
    [Tooltip("잠긴 슬롯 배경색 (lockedSprite 가 없을 때만 사용)")]
    public Color lockedColor = new(0f, 0f, 0f, 0.961f);
    [Tooltip("잠긴 슬롯 배경 스프라이트 (Trait_Box_Veil). 지정하면 lockedColor 대신 흰색으로 이 스프라이트 표시")]
    public Sprite lockedSprite;
    [Tooltip("해금된 슬롯 배경 스프라이트 (Trait_Box_Default). 비우면 스프라이트를 바꾸지 않음")]
    public Sprite defaultSprite;
    [Tooltip("잠긴 슬롯 텍스트 포맷. {0} = 해금 비용(다이아)")]
    public string lockedFormat = "잠김\n다이아 {0:N0}";

    [Tooltip("다음 해금 대상 슬롯일 때 보이는 비용 패널 (다이아 아이콘+가격). 비우면 자식 ValuePanel 자동 탐색")]
    public GameObject valuePanel;
    [Tooltip("ValuePanel 안의 가격 텍스트. 비우면 ValuePanel 안 TMP 자동 탐색")]
    public TMP_Text valueText;
    [Tooltip("아직 해금 순서가 아닌 잠긴 슬롯에 보이는 자물쇠. 비우면 자식 LockImage 자동 탐색")]
    public GameObject lockImage;

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

    // 장착 슬롯이 아닌 "표시 전용" 용도 (조합 재료 슬롯 등) — row 가 null 이면 빈 칸(+)
    public void SetPreview(TraitChartRow row)
    {
        if (emptyPlaceholder == null) { var t = transform.Find("PlusText"); if (t != null) emptyPlaceholder = t.gameObject; }
        bool filled = row != null;
        if (lockedVeil != null)       lockedVeil.SetActive(false);
        if (emptyPlaceholder != null) emptyPlaceholder.SetActive(!filled);
        if (filledContent != null)    filledContent.SetActive(filled);
        if (gradeBackground != null)
        {
            if (defaultSprite != null) gradeBackground.sprite = defaultSprite;
            gradeBackground.color = filled ? TraitGradeColors.Get(row.grade) : emptyColor;
        }
        if (nameText != null) nameText.text = filled ? row.name : string.Empty;
    }

    public void Refresh()
    {
        // veil 은 비용 텍스트를 덮으므로 사용하지 않는다 (씬에 남아있어도 강제 비활성)
        if (lockedVeil != null) lockedVeil.SetActive(false);
        // 잠긴 슬롯도 클릭 가능 — 클릭 시 해금 시도
        if (button != null) button.interactable = true;

        if (valuePanel == null) { var t = transform.Find("ValuePanel"); if (t != null) valuePanel = t.gameObject; }
        if (valueText == null && valuePanel != null) valueText = valuePanel.GetComponentInChildren<TMP_Text>(true);
        if (lockImage == null) { var t = transform.Find("LockImage"); if (t != null) lockImage = t.gameObject; }
        if (emptyPlaceholder == null) { var t = transform.Find("PlusText"); if (t != null) emptyPlaceholder = t.gameObject; }

        bool locked = IsLocked;
        // 잠긴 슬롯 중 다음 해금 대상만 비용 패널, 나머지는 자물쇠 (slot4 해금 → slot5 가 비용 패널로 전환)
        bool nextToUnlock = locked && OwnedTraitManager.Instance.CanUnlockSlot(SlotIndex);
        if (valuePanel != null) valuePanel.SetActive(nextToUnlock);
        if (lockImage != null)  lockImage.SetActive(locked && !nextToUnlock);

        if (locked)
        {
            int cost = OwnedTraitManager.Instance.GetSlotUnlockCost(SlotIndex);
            if (valueText != null) valueText.text = cost.ToString("N0");
            // 잠긴 슬롯은 장착할 수 없으므로 "+" 숨김
            if (emptyPlaceholder != null) emptyPlaceholder.SetActive(false);
            // 비용 패널이 있는 슬롯은 이름 텍스트로 비용을 중복 표시하지 않는다
            if (filledContent != null)    filledContent.SetActive(valuePanel == null);
            if (gradeBackground != null)
            {
                if (lockedSprite != null) { gradeBackground.sprite = lockedSprite; gradeBackground.color = Color.white; }
                else gradeBackground.color = lockedColor;
            }
            if (nameText != null)         nameText.text = string.Format(lockedFormat, cost);
            return;
        }

        if (gradeBackground != null && defaultSprite != null) gradeBackground.sprite = defaultSprite;

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
