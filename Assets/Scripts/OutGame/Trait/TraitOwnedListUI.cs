using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// TraitPanel 내 "전체 특성" 버튼으로 열리는 보유 갤러리 패널.
// _owned dict(traitId→count) 의 count 만큼 카드를 펼쳐서 그리드 표시. (중복 포함)
// 정렬: 등급 내림차순(S→A→B→C), 같은 등급 내 이름 오름차순.
//
// 인스펙터 연결:
//   - openButton : TraitPanel 의 "전체 특성" 버튼
//   - closeButton: 갤러리 패널 닫기 버튼
//   - listPanel  : 갤러리 패널 루트 (켜고 끌 GameObject)
//   - gridContainer: GridLayoutGroup 컨테이너
//   - itemPrefab : TraitItem.prefab (기존 TraitItemUI 재사용, lockedVeil 은 보유 상태라 자동 비활성)
public class TraitOwnedListUI : MonoBehaviour
{
    [Header("Open/Close")]
    public Button openButton;
    public Button closeButton;
    public GameObject listPanel;

    [Header("Grid")]
    public Transform gridContainer;
    public TraitItemUI itemPrefab;

    private readonly List<TraitItemUI> _pool = new();
    private bool _subscribed;

    void Awake()
    {
        if (listPanel != null) listPanel.SetActive(false);
        if (openButton != null)  openButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    // TraitPanel 탭을 떠나면 listPanel 도 함께 닫기 (다른 탭에 잔재 노출 방지)
    void OnDisable()
    {
        Close();
    }

    public void Open()
    {
        if (listPanel == null) return;
        Subscribe();
        Refresh();
        listPanel.SetActive(true);
    }

    public void Close()
    {
        if (listPanel != null) listPanel.SetActive(false);
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

    void Refresh()
    {
        if (itemPrefab == null || gridContainer == null) return;
        var owned = OwnedTraitManager.Instance?.AllOwned;
        var cache = TraitChartLoader.Cache;
        if (owned == null || cache == null) return;

        // 보유 (count > 0) 만 모아서 정렬
        var entries = new List<(TraitChartRow row, int count)>();
        foreach (var kv in owned)
        {
            if (kv.Value <= 0) continue;
            if (!cache.TryGetValue(kv.Key, out var row) || row == null) continue;
            entries.Add((row, kv.Value));
        }
        entries.Sort((a, b) =>
        {
            int c = b.row.grade.CompareTo(a.row.grade); // 등급 내림차순 (S=3 우선)
            if (c != 0) return c;
            return string.Compare(a.row.name, b.row.name, System.StringComparison.Ordinal);
        });

        // 중복 카운트 만큼 펼친 row 리스트
        var flat = new List<TraitChartRow>();
        foreach (var e in entries)
            for (int i = 0; i < e.count; i++)
                flat.Add(e.row);

        // 풀 부족분 Instantiate
        for (int i = _pool.Count; i < flat.Count; i++)
        {
            var item = Instantiate(itemPrefab, gridContainer);
            _pool.Add(item);
        }

        // 채우기 / 남는 슬롯은 비활성화
        for (int i = 0; i < _pool.Count; i++)
        {
            if (i < flat.Count)
            {
                _pool[i].gameObject.SetActive(true);
                _pool[i].SetData(flat[i], true);
                // 갤러리에서는 "장착중" 뱃지 표시 안 함 — 단순 보유 카드 표시 용도
                if (_pool[i].equippedBadge != null)
                    _pool[i].equippedBadge.SetActive(false);
            }
            else
            {
                _pool[i].gameObject.SetActive(false);
            }
        }
    }
}
