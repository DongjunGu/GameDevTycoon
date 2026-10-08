using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 아웃게임 "능력치" 패널 — CEO 책(Book) 강화 UI (구 조각 패널)
//
// 메인: 내 능력치 카드(초상화 + 기획/개발/아트 스탯) + 트랙 카드 3장
//   - 트랙 카드: 레벨, 책 바(보유 / 다음 레벨 필요량), 강화 가능 시 화살표
//   - 카드 클릭 → 상세 팝업
// 팝업: 레벨, 책 바, 특수효과(마일스톤 10/20/30/40), 레벨업 버튼(책 + 골드 소모량)
//
// 배열 인덱스 = CEOManager.Part (0 기획 / 1 개발 / 2 아트)
public class PiecePanelUI : MonoBehaviour
{
    [Serializable]
    public class TrackView
    {
        public Button cardButton;
        public TMP_Text levelText;
        [Tooltip("책 바 fill (Image Type = Filled). 보유 책 / 필요 책")]
        public Image bookFill;
        public TMP_Text bookText;
        [Tooltip("강화 가능할 때만 켜지는 화살표")]
        public GameObject upgradeArrow;
        [Tooltip("내 능력치 카드의 스탯 값")]
        public TMP_Text statText;
    }

    [Header("Tracks (0 기획 / 1 개발 / 2 아트)")]
    public TrackView[] tracks = new TrackView[3];
    public Sprite[] trackIcons = new Sprite[3];
    public Color[] trackColors = { Color.white, Color.white, Color.white };

    [Header("Currency")]
    [Tooltip("보유 책 수량")]
    public TMP_Text ownedBookText;
    [Tooltip("테스트용 — 누르면 책 +20 (뒤끝 저장)")]
    public Button addBookButton;

    [Header("Popup")]
    public GameObject popupRoot;
    [Tooltip("선택한 트랙 색으로 칠할 그래픽 (타이틀 탭, 좌측 카드 등)")]
    public Graphic[] popupTinted;
    public TMP_Text popupTitleText;
    public Image popupTitleIcon;
    public Image popupIcon;
    public TMP_Text popupLevelText;
    public Image popupBookFill;
    public TMP_Text popupBookText;
    [Tooltip("마일스톤 효과 텍스트 4개 (Lv10/20/30/40 순)")]
    public TMP_Text[] milestoneTexts = new TMP_Text[4];
    [Tooltip("미달성 마일스톤 텍스트 알파")]
    public float lockedAlpha = 0.4f;
    public Button closeButton;
    public Button levelUpButton;
    [Tooltip("레벨업 버튼 안 — 책 소모량")]
    public TMP_Text costBookText;
    [Tooltip("레벨업 버튼 안 — 골드 소모량")]
    public TMP_Text costGoldText;

    const int TestBookAmount = 20;
    const int DimOrder = 110; // ScreenDim: dim 110 / 팝업 111 (상단 재화 UI 100 위)

    static readonly string[] TrackNames = { "기획", "개발", "아트" };

    private CEOManager.Part _selected;
    private GameObject _dim;
    private bool _subscribed;

    void OnEnable()
    {
        for (int i = 0; i < tracks.Length; i++)
        {
            var part = (CEOManager.Part)i;
            Bind(tracks[i]?.cardButton, () => OpenPopup(part));
        }
        Bind(closeButton, ClosePopup);
        Bind(addBookButton, () => OutGameCurrencyManager.Instance?.AddBook(TestBookAmount)); // AddBook 이 즉시 저장
        Bind(levelUpButton, () => CEOManager.Instance?.TryUpgrade(_selected)); // OnChanged → Refresh 자동 호출

        if (popupRoot != null) popupRoot.SetActive(false);
        Subscribe();
        Refresh();
    }

    void OnDisable()
    {
        Unsubscribe();
        ScreenDim.Hide(_dim); // dim 은 패널 자식이 아니라 직접 꺼야 함
    }

    static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    void Subscribe()
    {
        if (_subscribed || CEOManager.Instance == null) return;
        CEOManager.Instance.OnChanged += Refresh;
        if (OutGameCurrencyManager.Instance != null) OutGameCurrencyManager.Instance.OnChanged += Refresh;
        _subscribed = true;
    }

    void Unsubscribe()
    {
        if (!_subscribed) return;
        if (CEOManager.Instance != null) CEOManager.Instance.OnChanged -= Refresh;
        if (OutGameCurrencyManager.Instance != null) OutGameCurrencyManager.Instance.OnChanged -= Refresh;
        _subscribed = false;
    }

    void OpenPopup(CEOManager.Part part)
    {
        if (popupRoot == null) return;
        _selected = part;
        popupRoot.SetActive(true);
        ScreenDim.Show(ref _dim, popupRoot, DimOrder); // 반드시 패널을 켠 뒤 호출
        Refresh();
    }

    void ClosePopup()
    {
        if (popupRoot != null) popupRoot.SetActive(false);
        ScreenDim.Hide(_dim);
    }

    public void Refresh()
    {
        var mgr = CEOManager.Instance;
        if (mgr == null) return;

        int owned = OutGameCurrencyManager.Instance != null ? OutGameCurrencyManager.Instance.Book : 0;
        if (ownedBookText != null) ownedBookText.text = owned.ToString("N0");

        for (int i = 0; i < tracks.Length; i++)
        {
            var view = tracks[i];
            if (view == null) continue;
            var part = (CEOManager.Part)i;

            if (view.levelText != null) view.levelText.text = $"Lv {mgr.GetLevel(part)}";
            if (view.statText != null)  view.statText.text  = mgr.GetStat(part).ToString();
            if (view.upgradeArrow != null) view.upgradeArrow.SetActive(mgr.CanUpgrade(part));
            SetBookBar(view.bookFill, view.bookText, mgr.GetLevel(part), owned);
        }

        if (popupRoot != null && popupRoot.activeSelf) RefreshPopup(mgr, owned);
    }

    void RefreshPopup(CEOManager mgr, int owned)
    {
        int idx = (int)_selected;
        int level = mgr.GetLevel(_selected);

        if (popupTinted != null && idx < trackColors.Length)
            foreach (var g in popupTinted) if (g != null) g.color = trackColors[idx];

        if (popupTitleText != null) popupTitleText.text = TrackNames[idx];
        if (popupLevelText != null) popupLevelText.text = $"Lv {level}";
        if (idx < trackIcons.Length)
        {
            if (popupTitleIcon != null) popupTitleIcon.sprite = trackIcons[idx];
            if (popupIcon != null)      popupIcon.sprite      = trackIcons[idx];
        }
        SetBookBar(popupBookFill, popupBookText, level, owned);

        for (int i = 0; i < milestoneTexts.Length; i++)
        {
            var text = milestoneTexts[i];
            if (text == null) continue;
            text.text  = BookBonusListUI.BonusTexts[idx][i];
            text.alpha = CEOManager.HasMilestone(_selected, BookBonusListUI.Milestones[i]) ? 1f : lockedAlpha;
        }

        bool hasNext = BookChartLoader.TryGetCost(level, out int book, out int gold);
        if (costBookText != null) costBookText.text = hasNext ? $"책 {book:N0}" : "MAX";
        if (costGoldText != null) costGoldText.text = hasNext ? $"골드 {gold:N0}" : "";
        if (levelUpButton != null) levelUpButton.interactable = mgr.CanUpgrade(_selected);
    }

    // 책 바: 보유 책 / 다음 레벨 필요 책. MAX 레벨이면 가득 채우고 "MAX".
    static void SetBookBar(Image fill, TMP_Text text, int level, int owned)
    {
        bool hasNext = BookChartLoader.TryGetCost(level, out int need, out _);
        if (fill != null) fill.fillAmount = hasNext ? Mathf.Clamp01((float)owned / need) : 1f;
        if (text != null) text.text = hasNext ? $"{owned:N0}/{need:N0}" : "MAX";
    }
}
