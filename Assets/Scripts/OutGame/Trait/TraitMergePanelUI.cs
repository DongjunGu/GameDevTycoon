using System.Collections.Generic;
using System.Text.RegularExpressions;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// TraitPanel 의 "특성 조합" 모드 전환 (TraitPanel 루트에 부착 — 패널들이 꺼졌다 켜지므로 항상 켜진 루트에 둔다)
// - MergeTraitBtn: LeftPanel 이 왼쪽으로 빠지고 TraitMergePanel 이 왼쪽에서 들어온다. 다시 누르면 반대로 복귀
// - 조합 모드 동안 RightPanel/Content 의 등급 섹션을 숨기고, 보유 특성(중복 포함)을 그리드로 표시
// - 카드 클릭 → 조합 슬롯(Top1Panel → Bottom2Panel 순)에 채움 / 채워진 슬롯 클릭 → 되돌림
// - 3칸이 다 차면 MergeRatePanel 에 해당 조합의 결과 등급 확률 표시
//
// 참조는 비워두면 하이어라키 이름으로 자동 탐색한다. 이름을 바꿨다면 인스펙터에 직접 연결할 것.
public class TraitMergePanelUI : MonoBehaviour
{
    [Header("Panels (비우면 이름으로 탐색)")]
    public GameObject leftPanel;        // LeftPanel
    public GameObject mergePanel;       // TraitMergePanel
    public RectTransform rightPanel;    // RightPanel — 좌측 폭이 바뀔 때 같이 밀려 이동
    public Button toggleButton;         // MergeTraitBtn
    public Transform rightContent;      // RightPanel/ScrollView/Viewport/Content

    [Header("Merge Slots — Top1Panel 1칸, Bottom2Panel 2칸 순 (비우면 TraitMergePanel 아래 TraitSlotUI 탐색)")]
    public TraitSlotUI[] mergeSlots;

    [Header("Rate (비우면 MergeRatePanel / Rank?Rate 탐색)")]
    public GameObject ratePanel;
    public TMP_Text rateS, rateA, rateB, rateC;

    [Header("Slide")]
    public float slideDuration = 0.25f;
    [Tooltip("화면 밖으로 빼는 여유 거리 (패널 폭에 더함)")]
    public float slideMargin = 400f;

    private HorizontalLayoutGroup _layout;
    private TraitOwnedListUI _ownedList;
    private TraitPanelUI _traitPanel;
    private CanvasGroup _rateGroup;
    private Transform _grid;                                   // 조합 모드용 카드 그리드 (Content 아래 런타임 생성)
    private readonly List<TraitItemUI> _pool = new();
    private readonly List<GameObject> _hiddenSections = new();
    private readonly TraitItemUI[] _picked = new TraitItemUI[TraitMerge.MaterialCount];
    private readonly Dictionary<TMP_Text, string> _rateTemplates = new();
    private bool _mergeMode;
    private bool _sliding;
    private bool _subscribed;

    public bool IsMergeMode => _mergeMode;

    void Awake()
    {
        _layout = GetComponent<HorizontalLayoutGroup>();
        _ownedList = GetComponent<TraitOwnedListUI>();
        _traitPanel = GetComponent<TraitPanelUI>();

        if (leftPanel == null)    leftPanel = FindChild("LeftPanel");
        if (mergePanel == null)   mergePanel = FindChild("TraitMergePanel");
        if (rightPanel == null)   { var rp = FindChild("RightPanel"); if (rp != null) rightPanel = rp.transform as RectTransform; }
        if (rightContent == null) rightContent = transform.Find("RightPanel/ScrollView/Viewport/Content");
        if (toggleButton == null) { var tb = FindChild("MergeTraitBtn"); if (tb != null) toggleButton = tb.GetComponent<Button>(); }

        if (mergePanel != null)
        {
            if (mergeSlots == null || mergeSlots.Length == 0) mergeSlots = mergePanel.GetComponentsInChildren<TraitSlotUI>(true);
            foreach (var t in mergePanel.GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "MergeRatePanel": if (ratePanel == null) ratePanel = t.gameObject; break;
                    case "RankSRate": if (rateS == null) rateS = t.GetComponent<TMP_Text>(); break;
                    case "RankARate": if (rateA == null) rateA = t.GetComponent<TMP_Text>(); break;
                    case "RankBRate": if (rateB == null) rateB = t.GetComponent<TMP_Text>(); break;
                    case "RankCRate": if (rateC == null) rateC = t.GetComponent<TMP_Text>(); break;
                }
            }
        }

        // 씬에 써둔 문구("S  100% " 등)의 숫자만 갈아끼우도록 원문을 보관
        foreach (var t in new[] { rateS, rateA, rateB, rateC })
            if (t != null) _rateTemplates[t] = t.text;

        if (ratePanel != null && !ratePanel.TryGetComponent(out _rateGroup)) _rateGroup = ratePanel.AddComponent<CanvasGroup>();

        if (toggleButton != null) toggleButton.onClick.AddListener(Toggle);
        if (mergeSlots != null)
            for (int i = 0; i < mergeSlots.Length && i < _picked.Length; i++)
            {
                int index = i;
                if (mergeSlots[i] != null && mergeSlots[i].button != null)
                    mergeSlots[i].button.onClick.AddListener(() => Unpick(index));
            }

        if (leftPanel == null || mergePanel == null || rightContent == null)
            Debug.LogWarning("[TraitMerge] LeftPanel / TraitMergePanel / RightPanel Content 를 찾지 못함 — 인스펙터에 직접 연결 필요");
    }

    GameObject FindChild(string childName)
    {
        var t = transform.Find(childName);
        return t != null ? t.gameObject : null;
    }

    // 탭에 들어올 때마다 기본(장착) 화면으로 시작
    void OnEnable() => ResetToNormal();

    void OnDisable()
    {
        Unsubscribe();
        ResetToNormal();
    }

    void ResetToNormal()
    {
        KillTweens();
        _sliding = false;
        if (_layout != null) _layout.enabled = true;
        if (mergePanel != null) mergePanel.SetActive(false);
        if (leftPanel != null) leftPanel.SetActive(true);
        if (_mergeMode) ShowMergeContent(false);
        _mergeMode = false;
    }

    void KillTweens()
    {
        if (leftPanel != null) leftPanel.transform.DOKill();
        if (mergePanel != null) mergePanel.transform.DOKill();
        if (rightPanel != null) rightPanel.DOKill();
    }

    public void Toggle()
    {
        if (_sliding || leftPanel == null || mergePanel == null) return;
        _mergeMode = !_mergeMode;
        ShowMergeContent(_mergeMode);
        if (_mergeMode) Slide(leftPanel, mergePanel);
        else Slide(mergePanel, leftPanel);
    }

    // from 을 왼쪽 화면 밖으로 밀어내고 끈 뒤, to 를 켜서 왼쪽 밖에서 제자리로 들인다.
    // 부모 HorizontalLayoutGroup 이 살아 있으면 자식 anchoredPosition 을 매 리빌드마다 덮어쓰므로 연출 동안만 꺼둔다.
    void Slide(GameObject from, GameObject to)
    {
        _sliding = true;
        var fromRt = (RectTransform)from.transform;
        var toRt = (RectTransform)to.transform;
        if (_layout != null) _layout.enabled = false;

        fromRt.DOAnchorPosX(fromRt.anchoredPosition.x - (fromRt.rect.width + slideMargin), slideDuration)
            .SetEase(Ease.InQuad).SetUpdate(true)
            .OnComplete(() =>
            {
                from.SetActive(false);
                to.SetActive(true);

                // 레이아웃을 잠깐 켜서 to / RightPanel 의 최종 위치를 받아온 뒤 다시 끄고 그 위치로 트윈
                Vector2 rightFrom = rightPanel != null ? rightPanel.anchoredPosition : Vector2.zero;
                if (_layout != null)
                {
                    _layout.enabled = true;
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
                    _layout.enabled = false;
                }
                Vector2 target = toRt.anchoredPosition;
                toRt.anchoredPosition = new Vector2(target.x - (toRt.rect.width + slideMargin), target.y);
                if (rightPanel != null)
                {
                    float rightTo = rightPanel.anchoredPosition.x;
                    rightPanel.anchoredPosition = rightFrom;
                    rightPanel.DOAnchorPosX(rightTo, slideDuration).SetEase(Ease.OutQuad).SetUpdate(true);
                }
                toRt.DOAnchorPosX(target.x, slideDuration).SetEase(Ease.OutQuad).SetUpdate(true)
                    .OnComplete(() =>
                    {
                        if (_layout != null) _layout.enabled = true;
                        _sliding = false;
                    });
            });
    }

    // ----- RightPanel 내용 전환 -----
    void ShowMergeContent(bool merge)
    {
        if (rightContent == null) return;
        ClearPicked();

        if (merge)
        {
            Subscribe();
            HideSections();
            BuildGrid();
        }
        else
        {
            Unsubscribe();
            if (_grid != null) _grid.gameObject.SetActive(false);
            foreach (var go in _hiddenSections)
                if (go != null) go.SetActive(true);
            _hiddenSections.Clear();
            if (_traitPanel != null && _traitPanel.isActiveAndEnabled) _traitPanel.Refresh(); // 빈 등급 섹션은 다시 꺼짐
        }
        RefreshSlots();
    }

    void HideSections()
    {
        foreach (Transform child in rightContent)
        {
            if (child == _grid || !child.gameObject.activeSelf) continue;
            child.gameObject.SetActive(false);
            _hiddenSections.Add(child.gameObject);
        }
    }

    void BuildGrid()
    {
        if (_ownedList == null || _ownedList.itemPrefab == null || _ownedList.gridContainer == null) return;

        if (_grid == null)
        {
            // OwnedTraitListPanel 의 GridContainer 를 그대로 복제 (GridLayoutGroup 설정 유지) 후 내용만 비운다
            _grid = Instantiate(_ownedList.gridContainer, rightContent);
            _grid.name = "MergeGridContainer";
            for (int i = _grid.childCount - 1; i >= 0; i--) Destroy(_grid.GetChild(i).gameObject);
        }
        _grid.gameObject.SetActive(true);

        var rows = TraitOwnedListUI.BuildOwnedRows();
        for (int i = _pool.Count; i < rows.Count; i++)
        {
            var item = Instantiate(_ownedList.itemPrefab, _grid);
            item.OnClicked += Pick;
            _pool.Add(item);
        }
        for (int i = 0; i < _pool.Count; i++)
        {
            bool used = i < rows.Count;
            _pool[i].gameObject.SetActive(used);
            if (!used) continue;
            _pool[i].SetData(rows[i], true);
            if (_pool[i].equippedBadge != null) _pool[i].equippedBadge.SetActive(false);
        }
    }

    // 조합 모드 중 보유 특성이 바뀌면(뽑기 등) 목록을 다시 만들고 선택은 비운다.
    // TraitPanelUI 가 같은 이벤트에서 등급 섹션을 다시 켜므로 여기서 도로 숨긴다.
    void OnOwnedChanged()
    {
        if (!_mergeMode) return;
        ClearPicked();
        HideSections();
        BuildGrid();
        RefreshSlots();
    }

    void Subscribe()
    {
        if (_subscribed || OwnedTraitManager.Instance == null) return;
        OwnedTraitManager.Instance.OnChanged += OnOwnedChanged;
        _subscribed = true;
    }

    void Unsubscribe()
    {
        if (!_subscribed) return;
        if (OwnedTraitManager.Instance != null) OwnedTraitManager.Instance.OnChanged -= OnOwnedChanged;
        _subscribed = false;
    }

    // ----- 재료 선택 -----
    void Pick(TraitItemUI item)
    {
        if (!_mergeMode || item == null || item.Data == null) return;
        if (item.Data.grade == TraitGrade.S)
        {
            if (ConfirmUI.Instance != null)
                ConfirmUI.Instance.Show("S등급 특성은 조합 재료로 쓸 수 없습니다.", onConfirm: null, confirmText: "확인", cancelText: "닫기");
            return;
        }

        int slot = System.Array.IndexOf(_picked, null);
        if (slot < 0) return; // 3칸 다 참
        _picked[slot] = item;
        item.gameObject.SetActive(false); // 슬롯으로 옮겨간 카드는 목록에서 뺀다 (카드 1장 = 보유 1장)
        RefreshSlots();
    }

    void Unpick(int slot)
    {
        if (!_mergeMode || slot < 0 || slot >= _picked.Length || _picked[slot] == null) return;
        _picked[slot].gameObject.SetActive(true);
        _picked[slot] = null;
        RefreshSlots();
    }

    void ClearPicked()
    {
        for (int i = 0; i < _picked.Length; i++) _picked[i] = null;
    }

    void RefreshSlots()
    {
        bool full = true;
        for (int i = 0; i < _picked.Length; i++)
        {
            var row = _picked[i] != null ? _picked[i].Data : null;
            if (row == null) full = false;
            if (mergeSlots != null && i < mergeSlots.Length && mergeSlots[i] != null) mergeSlots[i].SetPreview(row);
        }

        // MergeRatePanel 은 VerticalLayoutGroup 슬롯이라 끄지 않고 투명하게만 한다 (구도 유지)
        int[] rates = null;
        bool show = full && TraitMerge.TryGetRates(_picked[0].Data.grade, _picked[1].Data.grade, _picked[2].Data.grade, out rates);
        if (_rateGroup != null) _rateGroup.alpha = show ? 1f : 0f;
        if (!show) return;
        SetRate(rateS, rates[0]);
        SetRate(rateA, rates[1]);
        SetRate(rateB, rates[2]);
        SetRate(rateC, rates[3]);
    }

    void SetRate(TMP_Text text, int rate)
    {
        if (text == null) return;
        _rateTemplates.TryGetValue(text, out var template);
        text.text = !string.IsNullOrEmpty(template) && Regex.IsMatch(template, @"\d+")
            ? Regex.Replace(template, @"\d+", rate.ToString())
            : $"{rate}%";
    }
}
