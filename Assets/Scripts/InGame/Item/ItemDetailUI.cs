using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemDetailUI : MonoBehaviour
{
    public static ItemDetailUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject detailPanel;
    [Tooltip("디테일 열릴 때 터치 차단할 리스트 패널 (ItemPanel)")]
    public GameObject listBlockTarget;

    [Header("UI")]
    public Image              itemImage;
    public Image              frameImage;
    public ItemGradeSet       gradeSet;
    public TextMeshProUGUI    nameText;
    public TextMeshProUGUI    descriptionText;
    public Button             useButton;

    private ItemChartRow _currentRow;
    private CanvasGroup  _listCG;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        detailPanel.SetActive(false);

        if (listBlockTarget != null)
        {
            _listCG = listBlockTarget.GetComponent<CanvasGroup>();
            if (_listCG == null) _listCG = listBlockTarget.AddComponent<CanvasGroup>();
        }
    }

    void SetListInteractable(bool v)
    {
        if (_listCG == null) return;
        _listCG.interactable   = v;
        _listCG.blocksRaycasts = v;
    }

    public void Show(ItemChartRow row)
    {
        _currentRow = row;

        nameText.text        = row.name;
        descriptionText.text = row.description;

        int count = ItemManager.Instance.GetCount(row.itemId);
        useButton.interactable = count > 0 && ItemManager.IsUsableNow(row) && !IsGameUpgradeAlreadyUsed(row) && !IsRelaxButNoDebuffedEmployee(row) && !IsEventReadyCategory(row) && !IsUpgradeButNoRoleEmployee(row) && !IsCreativityBlockCategory(row) && !IsEnhanceProtectItem(row);

        var sprite = Resources.Load<Sprite>($"Items/{row.imageId}");
        if (sprite != null && itemImage != null)
            itemImage.sprite = sprite;

        ItemGradeSet.Apply(frameImage, gradeSet, row.grade);

        SetListInteractable(false);
        detailPanel.SetActive(true);
    }

    public void RefreshCount()
    {
        if (_currentRow == null) return;
        int count = ItemManager.Instance.GetCount(_currentRow.itemId);
        useButton.interactable = count > 0 && ItemManager.IsUsableNow(_currentRow) && !IsGameUpgradeAlreadyUsed(_currentRow) && !IsRelaxButNoDebuffedEmployee(_currentRow) && !IsUpgradeButNoRoleEmployee(_currentRow) && !IsCreativityBlockCategory(_currentRow) && !IsEnhanceProtectItem(_currentRow);
    }

    public void OnClickUse()
    {
        if (_currentRow == null) return;

        // 무대상 아이템 (창의성 블록 등): 직원 선택 안 거치고 즉시 사용
        if (!NeedsEmployeeTarget(_currentRow))
        {
            if (ItemManager.Instance.UseItemNoTarget(_currentRow.itemId))
            {
                HideDetail();
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
                HideDetail();
                ItemPanelUI.Instance.OnClickClose(); // 패널 닫기 + StartTime + 카드 콜백 호출
            }
            return;
        }

        // 강화권/초심회복기 — 아이템 사용 모드(선택→확인버튼)를 거치지 않고 곧장 직원목록+TrainingPanel로.
        if (EmployeeListUI.IsEnhanceBoostItem(_currentRow))
        {
            EmployeeListUI.Instance?.OpenForEnhanceWithBoost(_currentRow);
            return;
        }

        // 일반 플로우: 직원 리스트를 UseItem 모드로 열기 — 게임 카테고리 직군 아이템은 role 필터링
        EmployeeRole? roleFilter = _currentRow.itemId switch
        {
            "upgradeDevelop" => EmployeeRole.Programmer,
            "upgradeArt"     => EmployeeRole.Artist,
            "upgradePlan"    => EmployeeRole.Planner,
            _ => (EmployeeRole?)null
        };
        EmployeeListUI.Instance?.OpenForUseItem(_currentRow, roleFilter);
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

    // 창의성 블록 카테고리(랜덤/전설의 블록): 아이템창에서는 항상 비활성. 실제 사용은 창의성 미니게임의 BlockItemPanel에서만.
    static bool IsCreativityBlockCategory(ItemChartRow row)
        => row != null && row.category == "창의성 블록";

    // 하락 방어권: 수동 사용 불가 — 아이템창에서는 항상 비활성. EmployeeEnhancement.EnhanceOnce가 하락
    // 판정이 났을 때 자동으로 소모한다.
    static bool IsEnhanceProtectItem(ItemChartRow row)
        => row != null && row.itemId == "enhanceProtect";

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

    // 직군 업그레이드권: 회사에 해당 직군(role) 직원이 한 명도 없으면 사용 버튼 비활성
    static bool IsUpgradeButNoRoleEmployee(ItemChartRow row)
    {
        if (row == null) return false;
        EmployeeRole? role = row.itemId switch
        {
            "upgradeDevelop" => EmployeeRole.Programmer,
            "upgradeArt"     => EmployeeRole.Artist,
            "upgradePlan"    => EmployeeRole.Planner,
            _ => (EmployeeRole?)null
        };
        if (!role.HasValue) return false;

        var employees = EmployeeManager.Instance?.ownedEmployees;
        if (employees == null || employees.Count == 0) return true;
        foreach (var emp in employees)
            if (emp.role == role.Value) return false;
        return true;
    }

    public void HideDetail()
    {
        detailPanel.SetActive(false);
        SetListInteractable(true);
    }

    public void OnClickBack() => HideDetail();
}
