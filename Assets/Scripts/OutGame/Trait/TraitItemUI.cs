using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 우측 "특성 목록" 그리드 카드
// - 모든 특성 표시: 미보유는 lockedVeil(검정 알파245) 오버레이, 보유는 베일 제거
// - 클릭: 보유+미장착 → 빈 슬롯 자동 장착 / 보유+장착중 → 해제 / 미보유 → 무동작
public class TraitItemUI : MonoBehaviour
{
    [Header("References")]
    public Image gradeBackground;
    public TMP_Text nameText;
    public TMP_Text descriptionText;
    public GameObject equippedBadge;   // 장착 중 표시(선택)
    public GameObject lockedVeil;      // 미보유 오버레이(검정 알파)
    public Button button;

    public TraitChartRow Data { get; private set; }
    public bool IsOwned { get; private set; }

    public event Action<TraitItemUI> OnClicked;

    public void SetData(TraitChartRow row, bool owned)
    {
        Data = row;
        IsOwned = owned;
        if (row == null) return;

        if (gradeBackground != null) gradeBackground.color = TraitGradeColors.Get(row.grade);
        if (nameText != null) nameText.text = row.name;
        if (descriptionText != null) descriptionText.text = row.description;

        if (lockedVeil != null) lockedVeil.SetActive(!owned);
        RefreshEquippedState();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnClicked?.Invoke(this));
        }
    }

    public void RefreshEquippedState()
    {
        if (equippedBadge == null || Data == null || OwnedTraitManager.Instance == null) return;
        // 미보유는 베일이 가리므로 뱃지도 숨김
        equippedBadge.SetActive(IsOwned && OwnedTraitManager.Instance.IsEquipped(Data.traitId));
    }
}
