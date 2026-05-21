using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemDetailUI : MonoBehaviour
{
    public static ItemDetailUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject detailPanel;

    [Header("UI")]
    public Image              itemImage;
    public TextMeshProUGUI    nameText;
    public TextMeshProUGUI    descriptionText;
    public TextMeshProUGUI    effectText;
    public TextMeshProUGUI    countText;
    public Button             useButton;

    private ItemChartRow _currentRow;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        detailPanel.SetActive(false);
    }

    public void Show(ItemChartRow row)
    {
        _currentRow = row;

        nameText.text        = row.name;
        descriptionText.text = row.description;
        effectText.text      = row.effectType switch
        {
            "satisfaction" => $"만족도 +{row.effectValue}",
            _ => ""
        };

        int count = ItemManager.Instance.GetCount(row.itemId);
        countText.text         = $"보유: {count}개";
        useButton.interactable = count > 0 && ItemManager.IsUsableNow(row) && !IsGameUpgradeAlreadyUsed(row) && !IsRelaxButNoDebuffedEmployee(row) && !IsEventReadyCategory(row);

        var sprite = Resources.Load<Sprite>($"Items/{row.imageId}");
        if (sprite != null && itemImage != null)
            itemImage.sprite = sprite;

        ItemPanelUI.Instance.itemListPanel.SetActive(false);
        detailPanel.SetActive(true);
    }

    public void RefreshCount()
    {
        if (_currentRow == null) return;
        int count = ItemManager.Instance.GetCount(_currentRow.itemId);
        countText.text         = $"보유: {count}개";
        useButton.interactable = count > 0 && ItemManager.IsUsableNow(_currentRow) && !IsGameUpgradeAlreadyUsed(_currentRow) && !IsRelaxButNoDebuffedEmployee(_currentRow);
    }

    public void OnClickUse()
    {
        if (_currentRow == null) return;

        // 무대상 아이템 (창의성 블록 등): 직원 선택 안 거치고 즉시 사용
        if (!NeedsEmployeeTarget(_currentRow))
        {
            if (ItemManager.Instance.UseItemNoTarget(_currentRow.itemId))
            {
                RefreshCount();
                ItemPanelUI.Instance?.Refresh();
            }
            return;
        }

        // 카드 컨텍스트(특정 직원 대상으로 열림): 직원 선택 단계 건너뛰고 즉시 사용
        string targetId = ItemPanelUI.Instance != null ? ItemPanelUI.Instance.TargetEmployeeId : null;
        if (!string.IsNullOrEmpty(targetId))
        {
            var emp = EmployeeManager.Instance?.GetEmployee(targetId);
            if (emp != null && ItemManager.Instance.UseItem(_currentRow.itemId, emp))
            {
                detailPanel.SetActive(false);
                ItemPanelUI.Instance.OnClickClose(); // 패널 닫기 + StartTime + 카드 콜백 호출
            }
            return;
        }

        // 일반 플로우: 직원 선택 패널로 이동 — 게임 카테고리 직군 아이템은 role 필터링
        EmployeeRole? roleFilter = _currentRow.itemId switch
        {
            "upgradeDevelop" => EmployeeRole.Programmer,
            "upgradeArt"     => EmployeeRole.Artist,
            "upgradePlan"    => EmployeeRole.Planner,
            _ => (EmployeeRole?)null
        };
        ItemEmployeeSelectUI.Instance.Open(_currentRow, roleFilter);
    }

    static bool NeedsEmployeeTarget(ItemChartRow row)
    {
        // 무대상 아이템 — 직원 선택 스킵
        if (row.category == "창의성 블록") return false;
        if (row.category == "테크트리 포인트") return false;
        if (row.itemId == "upgradeRandom") return false;
        return true;
    }

    static bool IsGameUpgradeAlreadyUsed(ItemChartRow row)
    {
        if (row == null || row.category != "게임") return false;
        return DevelopmentManager.Instance != null && DevelopmentManager.Instance.IsGameUpgradeUsed(row.itemId);
    }

    // 이벤트 대비 카테고리: 아이템창에서는 항상 비활성. 실제 사용은 해당 이벤트 발생 시 선택지 흐름에서 처리.
    static bool IsEventReadyCategory(ItemChartRow row)
        => row != null && row.category == "이벤트 대비";

    // 라꾸라꾸: 회사 직원 중 디버프 걸린 사람이 한 명도 없으면 사용 버튼 비활성
    static bool IsRelaxButNoDebuffedEmployee(ItemChartRow row)
    {
        if (row == null || row.itemId != "relax") return false;
        var employees = EmployeeManager.Instance?.ownedEmployees;
        if (employees == null || employees.Count == 0) return true;
        foreach (var emp in employees)
            if (emp.HasAnyStatDebuff()) return false;
        return true;
    }

    public void OnClickBack()
    {
        detailPanel.SetActive(false);
        ItemPanelUI.Instance.itemListPanel.SetActive(true);
        ItemPanelUI.Instance.Refresh();
    }
}
