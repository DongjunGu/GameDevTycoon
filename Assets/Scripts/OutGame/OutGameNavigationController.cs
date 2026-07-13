using UnityEngine;
using UnityEngine.SceneManagement;

// 아웃게임 화면 전환 오케스트레이터 (SRP)
// - 어떤 탭이 활성인지 / 메인인지 결정
// - 탭별 시각/상태는 OutGameTabController에 위임
// - 메인 패널 + BottomNavBar 묶음만 직접 토글
public class OutGameNavigationController : MonoBehaviour
{
    public static OutGameNavigationController Instance { get; private set; }

    [Header("Main / NavBar")]
    [Tooltip("홈 화면 패널 (탭 들어가면 비활성화)")]
    public GameObject mainPanel;
    [Tooltip("하단 네비게이션 바 (탭 들어가면 숨김)")]
    public GameObject bottomNavBar;

    [Header("Tabs")]
    [Tooltip("탭 controller들 (직원/특성/조각/상점/랭킹/업적 ...)")]
    public OutGameTabController[] tabs;

    [Header("Default")]
    [Tooltip("씬 진입 시 메인 화면으로 시작 (false면 fallbackTabIndex로)")]
    public bool startInMain = true;
    public int  fallbackTabIndex = 0;

    public bool IsInMain => _activeIndex < 0;
    public int  ActiveIndex => _activeIndex;
    public OutGameTabController ActiveTab =>
        (_activeIndex >= 0 && tabs != null && _activeIndex < tabs.Length) ? tabs[_activeIndex] : null;

    private int _activeIndex = -1;

    void Awake() { Instance = this; }

    void Start()
    {
        // 다른 경로(파산 등)로 인게임에서 넘어와도 시간이 계속 틱하지 않도록 안전망
        GameTimeManager.Instance?.ForceStartTime(); // _stopCount 잔여 초기화
        GameTimeManager.Instance?.StopTime();       // 그 위에 일관된 정지 상태
        ModalGate.I.ClearAll();                     // 인게임 패널이 Unregister 없이 넘어왔을 잔여 등록 정리

        WireTabButtons();
        HideAllTabs();

        if (startInMain) GoToMain();
        else             SwitchTo(Mathf.Clamp(fallbackTabIndex, 0, Mathf.Max(0, (tabs?.Length ?? 1) - 1)));
    }

    void WireTabButtons()
    {
        if (tabs == null) return;
        for (int i = 0; i < tabs.Length; i++)
        {
            int captured = i;
            var tab = tabs[i];
            if (tab != null && tab.TabButton != null)
                tab.TabButton.onClick.AddListener(() => SwitchTo(captured));
        }
    }

    void HideAllTabs()
    {
        if (tabs == null) return;
        for (int i = 0; i < tabs.Length; i++)
            tabs[i]?.Hide();
    }

    void ShowMain(bool show)
    {
        if (mainPanel != null)    mainPanel.SetActive(show);
        if (bottomNavBar != null) bottomNavBar.SetActive(show);
    }

    public void SwitchTo(int index)
    {
        if (tabs == null || index < 0 || index >= tabs.Length) return;
        if (_activeIndex == index) return;

        ShowMain(false);
        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i] == null) continue;
            if (i == index) tabs[i].Show();
            else            tabs[i].Hide();
        }
        _activeIndex = index;
    }

    public void SwitchTo(string tabId)
    {
        if (tabs == null || string.IsNullOrEmpty(tabId)) return;
        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i] != null && tabs[i].TabId == tabId)
            {
                SwitchTo(i);
                return;
            }
        }
    }

    // 각 탭 패널의 "뒤로/홈" 버튼 OnClick 에 연결
    public void GoToMain()
    {
        HideAllTabs();
        ShowMain(true);
        _activeIndex = -1;
    }

    // MainPanel 의 "게임시작" 버튼 OnClick 에 연결 → 새 런 초기화 후 GameScene 진입
    public void StartGame()
    {
        NewRunInitializer.StartNewRun();
    }
}
