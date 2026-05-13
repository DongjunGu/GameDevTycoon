using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 돌 보관함 패널 — PiecePanel 의 listButton 으로 토글.
//
// 인스펙터 연결:
//   - slotPrefab: StoneSlotUI 가 붙은 사각형 prefab
//   - contentParent: ScrollRect content (또는 GridLayout/HorizontalLayout 부착된 부모)
//   - selectButton/deleteButton/addButton: 우하단 3 버튼
//
// 동작:
//   - StoneManager.Stones 를 풀어 슬롯 인스턴스화
//   - 슬롯 클릭 → 선택 상태 갱신 (단일 선택)
//   - 선택하기: StoneManager.SwitchActive(_selected) — CEO 활성 돌과 스위치
//   - 삭제하기: StoneManager.Remove(_selected)
//   - + : StoneManager.AddNew()
//   - StoneManager.OnChanged 구독 → 자동 Rebuild
public class StoneListPanelUI : MonoBehaviour
{
    [Header("Slot Spawn")]
    public StoneSlotUI slotPrefab;
    public Transform   contentParent;

    [Header("Buttons (우하단)")]
    public Button selectButton;
    public Button deleteButton;
    public Button addButton;
    // "보관함에 넣기" 는 PiecePanelUI 의 BottomButtonRow 로 이전. 여기 아님.

    [Header("Close (우상단 X)")]
    public Button closeButton;

    [Header("Sort Dropdown")]
    [Tooltip("정렬 TMP_Dropdown. 6 옵션 자동 채움: 0=기획높 1=기획낮 2=개발높 3=개발낮 4=아트높 5=아트낮.")]
    public TMP_Dropdown sortDropdown;

    enum SortMode
    {
        PlanningDesc = 0,
        PlanningAsc = 1,
        DevelopDesc = 2,
        DevelopAsc = 3,
        ArtDesc = 4,
        ArtAsc = 5,
    }
    SortMode _sortMode = SortMode.PlanningDesc;

    readonly List<StoneSlotUI> _slots = new();
    Stone _selected;
    bool _subscribed;

    void OnEnable()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnClickSelect);
        }
        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(OnClickDelete);
        }
        if (addButton != null)
        {
            addButton.onClick.RemoveAllListeners();
            addButton.onClick.AddListener(OnClickAdd);
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnClickClose);
        }
        if (sortDropdown != null)
        {
            sortDropdown.onValueChanged.RemoveAllListeners();
            sortDropdown.ClearOptions();
            sortDropdown.AddOptions(new List<string>
            {
                "기획 높은순", "기획 낮은순",
                "개발 높은순", "개발 낮은순",
                "아트 높은순", "아트 낮은순",
            });
            sortDropdown.SetValueWithoutNotify((int)_sortMode);
            sortDropdown.RefreshShownValue();
            sortDropdown.onValueChanged.AddListener(OnSortChanged);
        }

        Subscribe();
        Rebuild();
    }

    void OnSortChanged(int idx)
    {
        _sortMode = (SortMode)idx;
        Rebuild();
    }

    void OnClickClose() => gameObject.SetActive(false);

    void OnDisable() => Unsubscribe();

    void Subscribe()
    {
        if (_subscribed || StoneManager.Instance == null) return;
        StoneManager.Instance.OnChanged += Rebuild;
        _subscribed = true;
    }

    void Unsubscribe()
    {
        if (!_subscribed || StoneManager.Instance == null) return;
        StoneManager.Instance.OnChanged -= Rebuild;
        _subscribed = false;
    }

    public void Rebuild()
    {
        if (slotPrefab == null || contentParent == null)
        {
            Debug.LogWarning($"[StoneListPanel] Rebuild 스킵 - slotPrefab={(slotPrefab==null?"null":"ok")} contentParent={(contentParent==null?"null":"ok")}");
            return;
        }

        foreach (var s in _slots) if (s != null) Destroy(s.gameObject);
        _slots.Clear();

        var mgr = StoneManager.Instance;
        if (mgr == null) { Debug.LogWarning("[StoneListPanel] Rebuild - StoneManager.Instance null"); RefreshButtons(); return; }
        Debug.Log($"[StoneListPanel] Rebuild - 보관함 {mgr.Stones.Count} 개 슬롯 생성");

        // 선택 돌이 리스트에서 사라졌으면 해제
        if (_selected != null && !mgr.Stones.Contains(_selected)) _selected = null;

        foreach (var stone in SortedStones(mgr.Stones))
        {
            var slot = Instantiate(slotPrefab, contentParent);
            slot.Bind(stone, OnSlotClicked);
            slot.SetSelected(stone == _selected);
            slot.SetApplied(stone.id == mgr.ActiveStoneId);
            _slots.Add(slot);
        }

        RefreshButtons();
    }

    IEnumerable<Stone> SortedStones(List<Stone> src)
    {
        switch (_sortMode)
        {
            case SortMode.PlanningAsc:  return src.OrderBy(s => s.planningStage);
            case SortMode.PlanningDesc: return src.OrderByDescending(s => s.planningStage);
            case SortMode.DevelopAsc:   return src.OrderBy(s => s.developStage);
            case SortMode.DevelopDesc:  return src.OrderByDescending(s => s.developStage);
            case SortMode.ArtAsc:       return src.OrderBy(s => s.artStage);
            case SortMode.ArtDesc:      return src.OrderByDescending(s => s.artStage);
        }
        return src;
    }

    void OnSlotClicked(Stone stone)
    {
        _selected = (stone == _selected) ? null : stone; // 두 번 클릭 시 해제
        foreach (var slot in _slots)
            slot.SetSelected(slot.Stone == _selected);
        RefreshButtons();
    }

    void OnClickSelect()
    {
        if (_selected == null) return;
        if (StoneManager.Instance == null) return;
        StoneManager.Instance.SetActive(_selected);
        // 선택돌이 active 가 됨 → 시각적으로는 그 돌에 적용중 배지가 켜짐. 선택 해제는 안 함.
    }

    void OnClickDelete()
    {
        if (_selected == null) return;
        if (StoneManager.Instance == null) return;
        StoneManager.Instance.Remove(_selected);
        _selected = null;
    }

    void OnClickAdd()
    {
        if (StoneManager.Instance == null) return;
        StoneManager.Instance.AddNew();
    }

    void RefreshButtons()
    {
        var mgr = StoneManager.Instance;
        bool hasSel = _selected != null;
        bool selIsActive = (mgr != null && _selected != null && _selected.id == mgr.ActiveStoneId);
        // 선택하기: 선택돌 있고 그게 active 가 아닐 때만
        if (selectButton != null) selectButton.interactable = hasSel && !selIsActive;
        // 삭제하기: 선택돌 있을 때 (active 여도 가능 — 삭제하면 active 가 비워짐)
        if (deleteButton != null) deleteButton.interactable = hasSel;
        bool underCap = (mgr == null || mgr.maxStones <= 0 || mgr.Stones.Count < mgr.maxStones);
        if (addButton  != null) addButton.interactable  = underCap;
    }
}
