using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// TraitPanel 의 "특성 조합" 화면 (TraitPanel 루트에 부착 — 조합 패널이 꺼졌다 켜지므로 항상 켜진 루트에 둔다)
// - MergeTraitBtn: 전체 화면 TraitMergePanel 을 켠다 / TraitMergePanel/TopPanel/BackBtn: 끈다
// - TraitMergePanel 자체 RightPanel/Content/MergeGridContainer 에 보유 특성(중복 포함) 카드를 표시
// - 카드 클릭 → 조합 슬롯(Top1Panel → Bottom2Panel 순)에 채움 + 카드에 veil/체크 표시 / 체크된 카드나 채워진 슬롯 클릭 → 되돌림
// - 3칸이 다 차면 MergeRatePanel 에 해당 조합의 결과 등급 확률 표시 + 조합하기 버튼 활성
// - 조합하기 버튼 → MergeConfirmPanel2 확인창 → 확인 시 TraitMerge.TryMerge → 결과 카드를 MergeConfirmPanel 에 표시 (실패 사유는 ConfirmUI)
//
// 참조는 비워두면 하이어라키 이름으로 자동 탐색한다. 이름을 바꿨다면 인스펙터에 직접 연결할 것.
public class TraitMergePanelUI : MonoBehaviour
{
    [Header("Panels (비우면 이름으로 탐색)")]
    public GameObject mergePanel;       // TraitPanel/TraitMergePanel (전체 화면)
    public Button toggleButton;         // MergeTraitBtn — 조합 화면 열기
    public Button backButton;           // TraitMergePanel/TopPanel/BackBtn — 조합 화면 닫기
    public Transform mergeGrid;         // TraitMergePanel/RightPanel/ScrollView/Viewport/Content/MergeGridContainer — 카드가 들어갈 그리드

    [Header("Merge Slots — Top1Panel 1칸, Bottom2Panel 2칸 순 (비우면 TraitMergePanel 아래 TraitSlotUI 탐색)")]
    public TraitSlotUI[] mergeSlots;

    [Header("Rate (비우면 MergeRatePanel / Rank?Rate 탐색)")]
    public GameObject ratePanel;
    public TMP_Text rateS, rateA, rateB, rateC;

    [Header("Merge (비우면 MergeExecuteBtn 탐색)")]
    public Button mergeButton;          // 조합하기 — 조합 화면에서만 보이고 3칸이 차야 눌린다

    [Header("Ask (비우면 ConfirmUI 아래 MergeConfirmPanel2 / 그 안의 ConfirmButton·CancelButton 탐색)")]
    public GameObject mergeAskPanel;       // 조합 실행 전 확인창
    public Button mergeAskConfirmButton;   // 확인 — 여기서 실제 조합
    public Button mergeAskCancelButton;    // 취소 — 창만 닫음

    [Header("Result (비우면 ConfirmUI 아래 MergeConfirmPanel / 그 안의 TraitItemUI 탐색)")]
    public GameObject mergeConfirmPanel;   // 조합 결과 창 — 안의 버튼은 전부 닫기
    public TraitItemUI mergeConfirmItem;   // 결과 특성 카드

    // ConfirmUI dim 과 같은 층 (다른 아웃게임 dim 110 보다 위)
    const int ResultDimSortingOrder = 120;
    private GameObject _resultDim;
    private bool _resultHooked;
    private GameObject _askDim;
    private bool _askHooked;

    private TraitOwnedListUI _ownedList;
    private CanvasGroup _rateGroup;
    private readonly List<TraitItemUI> _pool = new();
    private readonly TraitItemUI[] _picked = new TraitItemUI[TraitMerge.MaterialCount];
    private readonly Dictionary<TMP_Text, string> _rateTemplates = new();
    private bool _mergeMode;
    private bool _subscribed;

    public bool IsMergeMode => _mergeMode;

    void Awake()
    {
        _ownedList = GetComponent<TraitOwnedListUI>();

        if (mergePanel == null)   { var mp = transform.Find("TraitMergePanel"); if (mp != null) mergePanel = mp.gameObject; }
        if (toggleButton == null) { var tb = transform.Find("MergeTraitBtn"); if (tb != null) toggleButton = tb.GetComponent<Button>(); }

        // 이름 끝에 공백이 붙은 오브젝트("RightPanel ")가 있어 Trim 해서 비교한다
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            bool inMerge = mergePanel != null && t.IsChildOf(mergePanel.transform);
            switch (t.name.Trim())
            {
                case "MergeExecuteBtn": if (mergeButton == null) mergeButton = t.GetComponent<Button>(); break;
                case "BackBtn":        if (inMerge && backButton == null) backButton = t.GetComponent<Button>(); break;
                case "MergeGridContainer": if (inMerge && mergeGrid == null) mergeGrid = t; break;
                case "MergeRatePanel": if (inMerge && ratePanel == null) ratePanel = t.gameObject; break;
                case "RankSRate": if (inMerge && rateS == null) rateS = t.GetComponent<TMP_Text>(); break;
                case "RankARate": if (inMerge && rateA == null) rateA = t.GetComponent<TMP_Text>(); break;
                case "RankBRate": if (inMerge && rateB == null) rateB = t.GetComponent<TMP_Text>(); break;
                case "RankCRate": if (inMerge && rateC == null) rateC = t.GetComponent<TMP_Text>(); break;
            }
        }
        if (mergePanel != null && (mergeSlots == null || mergeSlots.Length == 0))
            mergeSlots = mergePanel.GetComponentsInChildren<TraitSlotUI>(true);

        // 씬에 써둔 문구("S  100% " 등)의 숫자만 갈아끼우도록 원문을 보관
        foreach (var t in new[] { rateS, rateA, rateB, rateC })
            if (t != null) _rateTemplates[t] = t.text;

        if (ratePanel != null && !ratePanel.TryGetComponent(out _rateGroup)) _rateGroup = ratePanel.AddComponent<CanvasGroup>();

        if (toggleButton != null) toggleButton.onClick.AddListener(Open);
        if (backButton != null)
        {
            // 탭용 BackBtn 을 복제해 만든 버튼이라 인스펙터 OnClick(GoToMain)이 남아 있으면 메인까지 나가버린다 — 조합 화면만 닫도록 끈다
            for (int i = 0; i < backButton.onClick.GetPersistentEventCount(); i++)
                backButton.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
            backButton.onClick.AddListener(Close);
        }
        if (mergeButton != null) mergeButton.onClick.AddListener(DoMerge);
        if (mergeSlots != null)
            for (int i = 0; i < mergeSlots.Length && i < _picked.Length; i++)
            {
                int index = i;
                if (mergeSlots[i] != null && mergeSlots[i].button != null)
                    mergeSlots[i].button.onClick.AddListener(() => Unpick(index));
            }

        if (mergePanel == null || mergeGrid == null || backButton == null)
            Debug.LogWarning("[TraitMerge] TraitMergePanel / 그 안의 MergeGridContainer / BackBtn 을 찾지 못함 — 인스펙터에 직접 연결 필요");
    }

    // 탭에 들어오거나 떠날 때 항상 기본(장착) 화면으로
    void OnEnable() => Close();
    void OnDisable() => Close();

    public void Open()
    {
        if (mergePanel == null) return;
        _mergeMode = true;
        mergePanel.SetActive(true);
        if (mergeButton != null) mergeButton.gameObject.SetActive(true);
        Subscribe();
        ClearPicked();
        BuildGrid();
        RefreshSlots();
    }

    public void Close()
    {
        _mergeMode = false;
        Unsubscribe();
        ClearPicked();
        HideAsk();
        HideResult();
        if (mergePanel != null) mergePanel.SetActive(false);
        if (mergeButton != null) mergeButton.gameObject.SetActive(false);
    }

    void BuildGrid()
    {
        if (mergeGrid == null || _ownedList == null || _ownedList.itemPrefab == null) return;

        var rows = TraitOwnedListUI.BuildOwnedRows();
        for (int i = _pool.Count; i < rows.Count; i++)
        {
            var item = Instantiate(_ownedList.itemPrefab, mergeGrid);
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

    // 조합 화면 중 보유 특성이 바뀌면(조합 등) 목록을 다시 만들고 선택은 비운다.
    void OnOwnedChanged()
    {
        if (!_mergeMode) return;
        ClearPicked();
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

        // 이미 체크된 카드를 다시 누르면 선택 해제
        int already = System.Array.IndexOf(_picked, item);
        if (already >= 0) { Unpick(already); return; }

        int slot = System.Array.IndexOf(_picked, null);
        if (slot < 0) return; // 3칸 다 참
        _picked[slot] = item;
        item.SetSelected(true); // 카드는 목록에 그대로 두고 veil + 체크로 선택됨을 표시 (카드 1장 = 보유 1장)
        RefreshSlots();
    }

    void Unpick(int slot)
    {
        if (!_mergeMode || slot < 0 || slot >= _picked.Length || _picked[slot] == null) return;
        _picked[slot].SetSelected(false);
        _picked[slot] = null;
        RefreshSlots();
    }

    void ClearPicked()
    {
        for (int i = 0; i < _picked.Length; i++)
        {
            if (_picked[i] != null) _picked[i].SetSelected(false);
            _picked[i] = null;
        }
    }

    // ----- 조합 실행 -----
    bool AllPicked()
    {
        foreach (var p in _picked)
            if (p == null || p.Data == null) return false;
        return true;
    }

    // 조합하기 버튼 — 바로 조합하지 않고 확인창(MergeConfirmPanel2)을 띄운다. 창을 못 찾으면 ConfirmUI 로 대신 묻는다
    void DoMerge()
    {
        if (!_mergeMode || !AllPicked()) return;

        if (mergeAskPanel == null && ConfirmUI.Instance != null)
        {
            var t = ConfirmUI.Instance.transform.Find("MergeConfirmPanel2");
            if (t != null) mergeAskPanel = t.gameObject;
        }
        if (mergeAskPanel == null)
        {
            if (ConfirmUI.Instance != null) ConfirmUI.Instance.Show("선택한 특성 3개를 조합할까요?", onConfirm: ExecuteMerge);
            return;
        }

        if (!_askHooked)
        {
            _askHooked = true;
            foreach (var b in mergeAskPanel.GetComponentsInChildren<Button>(true))
            {
                if (mergeAskConfirmButton == null && b.name.Trim() == "ConfirmButton") mergeAskConfirmButton = b;
                if (mergeAskCancelButton == null && b.name.Trim() == "CancelButton")   mergeAskCancelButton = b;
            }
            if (mergeAskConfirmButton != null) mergeAskConfirmButton.onClick.AddListener(() => { HideAsk(); ExecuteMerge(); });
            if (mergeAskCancelButton != null)  mergeAskCancelButton.onClick.AddListener(HideAsk);
        }
        mergeAskPanel.SetActive(true);
        ScreenDim.Show(ref _askDim, mergeAskPanel, ResultDimSortingOrder); // 뒤 dim + 클릭 차단
    }

    void HideAsk()
    {
        if (mergeAskPanel != null) mergeAskPanel.SetActive(false);
        ScreenDim.Hide(_askDim);
    }

    void ExecuteMerge()
    {
        if (!_mergeMode || !AllPicked()) return;

        // TryMerge 성공 시 OnChanged → OnOwnedChanged 가 선택을 비우고 목록을 다시 만든다
        bool ok = TraitMerge.TryMerge(_picked[0].Data.traitId, _picked[1].Data.traitId, _picked[2].Data.traitId, out var result, out var reason);
        if (ok && ShowResult(result)) return;
        if (ConfirmUI.Instance == null) return;
        ConfirmUI.Instance.Show(ok ? $"조합 결과\n\n[{result.grade}] {result.name}\n{result.description}" : reason,
                                onConfirm: null, confirmText: "확인", cancelText: "닫기");
    }

    // 결과 창(MergeConfirmPanel)에 얻은 특성 카드를 띄운다. 창을 못 찾으면 false (호출부가 ConfirmUI 텍스트로 대체)
    bool ShowResult(TraitChartRow row)
    {
        if (mergeConfirmPanel == null && ConfirmUI.Instance != null)
        {
            var t = ConfirmUI.Instance.transform.Find("MergeConfirmPanel");
            if (t != null) mergeConfirmPanel = t.gameObject;
        }
        if (mergeConfirmPanel == null) return false;
        if (mergeConfirmItem == null) mergeConfirmItem = mergeConfirmPanel.GetComponentInChildren<TraitItemUI>(true);

        if (!_resultHooked)
        {
            _resultHooked = true;
            foreach (var b in mergeConfirmPanel.GetComponentsInChildren<Button>(true))
                if (mergeConfirmItem == null || b != mergeConfirmItem.button) b.onClick.AddListener(HideResult);
        }

        if (mergeConfirmItem != null)
        {
            mergeConfirmItem.SetData(row, true);
            if (mergeConfirmItem.equippedBadge != null) mergeConfirmItem.equippedBadge.SetActive(false);
        }
        mergeConfirmPanel.SetActive(true);
        ScreenDim.Show(ref _resultDim, mergeConfirmPanel, ResultDimSortingOrder); // 뒤 dim + 클릭 차단
        return true;
    }

    void HideResult()
    {
        if (mergeConfirmPanel != null) mergeConfirmPanel.SetActive(false);
        ScreenDim.Hide(_resultDim);
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
        if (mergeButton != null) mergeButton.interactable = show;
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
