using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;

// 프로젝트 설정 — 허브식 단일 패널(d1.png).
// mainPanel 하나에서: 규모(소/중/대 토글 — 클릭해서 선택 표시), 플랫폼·장르(누르면 별도 패널), 개발금(규모 따라 자동), 개발 시작.
// 플랫폼/장르 상세 패널(genrePanel/platformPanel)은 추후 개편 — 여기서는 OnClickGenre/OnClickPlatform 으로 선택 결과만 받아 mainPanel 로 복귀.
public class ProjectSetupUI : MonoBehaviour
{
    public static ProjectSetupUI Instance { get; private set; }

    // ── 패널 ──────────────────────────────────
    [Header("Panels")]
    public GameObject mainPanel;       // 허브: 규모 토글 + 플랫폼/장르 버튼 + 개발금 + 개발시작
    [FormerlySerializedAs("genrePlatformPanel")]
    public GameObject genrePanel;      // 장르 선택 패널 (장르 선택하기 → 이 패널)
    public GameObject platformPanel;   // 플랫폼 선택 패널 (플랫폼 선택하기 → 이 패널)

    // ── 규모 토글 버튼 ────────────────────────
    [Header("Scale Toggle (Small / Medium / Large)")]
    public Button scaleButtonSmall;
    public Button scaleButtonMedium;
    public Button scaleButtonLarge;
    public Color scaleTextNormalColor   = new Color32(0xB3, 0xA1, 0x8B, 0xFF);
    public Color scaleTextSelectedColor = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
    public Color scaleTextDisabledColor = new Color32(0x87, 0x78, 0x66, 0xFF);

    // ── 플랫폼 / 장르 바 (누르면 별도 패널) ────
    [Header("Platform / Genre Bar")]
    public Button platformButton;             // 누르면 platformPanel
    public Button genreButton;                // 누르면 genrePanel
    public TextMeshProUGUI platformValueText; // 선택된 플랫폼명 (미선택 시 unselectedText)
    public TextMeshProUGUI genreValueText;    // 선택된 장르명
    public string unselectedText = "선택하기";

    [Header("Platform Select Buttons (순서: Mobile/PC/Nintendo/Console)")]
    public Button[] platformButtons;          // PlatformPanel 안 플랫폼 선택 버튼 (인덱스 = ProjectPlatform enum)

    // ── 개발금 ────────────────────────────────
    [Header("Dev Cost")]
    public TextMeshProUGUI devCostText;       // "개발금: {N}G" — 규모 따라 자동 갱신

    // ── 개발시작 / 닫기 ───────────────────────
    [Header("Action Buttons")]
    public Button startButton;                // 개발 시작
    public Button closeButton;                // 닫기

    [Header("Back Buttons (장르/플랫폼 패널 → 메인 복귀)")]
    public Button genreBackButton;            // GenrePanel 뒤로가기
    public Button platformBackButton;         // PlatformPanel 뒤로가기

    // ── 선택 결과 (다른 시스템이 읽음) ────────
    public static ProjectScale SelectedScale { get; set; }
    public static ProjectGenre SelectedGenre { get; set; }
    public static ProjectPlatform SelectedPlatform { get; set; }
    public static int SelectedGenrePopularity { get; set; } = 1;
    public static int SelectedGenreFatigue { get; set; } = 0;

    // ── 내부 상태 ─────────────────────────────
    private ProjectData _projectData = new();
    private bool _genreChosen;
    private bool _platformChosen;
    private readonly Dictionary<Button, Sprite> _scaleNormalSprite = new();
    private readonly Dictionary<Button, Sprite> _platformNormalSprite = new();
    private readonly Dictionary<Button, Sprite> _genreNormalSprite = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // 버튼 리스너 코드 배선(인스펙터 배선 불필요). 규모 버튼은 mainPanel 인라인 토글.
        if (scaleButtonSmall  != null) scaleButtonSmall.onClick.AddListener(()  => OnClickScale(0));
        if (scaleButtonMedium != null) scaleButtonMedium.onClick.AddListener(() => OnClickScale(1));
        if (scaleButtonLarge  != null) scaleButtonLarge.onClick.AddListener(()  => OnClickScale(2));
        InitScaleSpriteState(scaleButtonSmall);
        InitScaleSpriteState(scaleButtonMedium);
        InitScaleSpriteState(scaleButtonLarge);
        if (platformButtons != null)
            foreach (var pb in platformButtons) InitPlatformSpriteState(pb);

        WireGenreButton(genreButtonRPG,               "RPG");
        WireGenreButton(genreButtonFPS,                "FPS");
        WireGenreButton(genreButtonArcade,             "Arcade");
        WireGenreButton(genreButtonHealingSimulation,  "HealingSimulation");
        WireGenreButton(genreButtonHorror,             "Horror");
        WireGenreButton(genreButtonIdle,               "Idle");
        WireGenreButton(genreButtonRTS,                "RTS");
        WireGenreButton(genreButtonVisualNovel,        "VisualNovel");
        WireGenreButton(genreButtonSports,             "Sports");
        WireGenreButton(genreButtonPuzzle,             "Puzzle");
        InitGenreSpriteState(genreButtonRPG);
        InitGenreSpriteState(genreButtonFPS);
        InitGenreSpriteState(genreButtonArcade);
        InitGenreSpriteState(genreButtonHealingSimulation);
        InitGenreSpriteState(genreButtonHorror);
        InitGenreSpriteState(genreButtonIdle);
        InitGenreSpriteState(genreButtonRTS);
        InitGenreSpriteState(genreButtonVisualNovel);
        InitGenreSpriteState(genreButtonSports);
        InitGenreSpriteState(genreButtonPuzzle);
        if (platformButton != null) platformButton.onClick.AddListener(OpenPlatformPanel);
        if (genreButton    != null) genreButton.onClick.AddListener(OpenGenrePanel);

        if (startButton != null) { startButton.onClick.RemoveAllListeners(); startButton.onClick.AddListener(OnClickStartDevelopment); }
        if (closeButton != null) { closeButton.onClick.RemoveAllListeners(); closeButton.onClick.AddListener(OnClickClose); }

        if (genreBackButton    != null) { genreBackButton.onClick.RemoveAllListeners();    genreBackButton.onClick.AddListener(BackToMain); }
        if (platformBackButton != null) { platformBackButton.onClick.RemoveAllListeners(); platformBackButton.onClick.AddListener(BackToMain); }

        // 플랫폼 선택 버튼 — 장르 버튼처럼 클릭 시 해당 플랫폼 선택(OnClickPlatform). 인덱스 = enum 순서.
        if (platformButtons != null)
            for (int i = 0; i < platformButtons.Length; i++)
            {
                if (platformButtons[i] == null) continue;
                int idx = i;
                platformButtons[i].onClick.RemoveAllListeners();
                platformButtons[i].onClick.AddListener(() => OnClickPlatform(idx));
            }
    }

    // ══════════════════════════════════════════
    // 진입 / 메인 패널
    // ══════════════════════════════════════════

    // 프로젝트 시작 버튼(메뉴) → 메인 패널 오픈
    public void OnClickProjectStart()
    {
        var stage = DevelopmentManager.Instance.CurrentStage;
        if (DevelopmentManager.Instance.IsStarted &&
            (stage == ProjectStage.Developing || stage == ProjectStage.BugFixing || stage == ProjectStage.Marketing))
        {
            AlertUI.Instance.Show("진행중인 프로젝트가 있습니다.");
            return;
        }
        GameTimeManager.Instance.StopTime();
        ModalGate.I.Register(this);

        // 새 설정 초기화 — 마지막 선택(로컬 저장)이 있으면 규모/플랫폼/장르 복원, 없으면 소규모+미선택.
        _projectData = new ProjectData { scale = ProjectScale.Small };
        _genreChosen = false;
        _platformChosen = false;
        LoadLastSelection();

        UpdateGenreButtonLabels();
        RefreshMain();
        ShowOnly(mainPanel);
    }

    // 메인 패널 표시값 일괄 갱신
    void RefreshMain()
    {
        UpdateScaleAvailability();
        RefreshScaleHighlight();
        UpdateDevCost();
        if (platformValueText != null) platformValueText.text = _platformChosen ? _projectData.PlatformToString() : unselectedText;
        if (genreValueText    != null) genreValueText.text    = _genreChosen    ? _projectData.GenreToString()    : unselectedText;
    }

    // StageManager 단계에 따라 규모 버튼 잠금(1단계=소, 2단계=중까지, 3단계 이상=대까지). 잠긴 규모가 선택돼 있으면 사용 가능한 최대 규모로 낮춤.
    void UpdateScaleAvailability()
    {
        int stage = StageManager.Instance != null ? StageManager.Instance.CurrentStage : 1;
        SetScaleLocked(scaleButtonSmall,  stage < 1);
        SetScaleLocked(scaleButtonMedium, stage < 2);
        SetScaleLocked(scaleButtonLarge,  stage < 3);

        if (stage < 3 && _projectData.scale == ProjectScale.Large)  _projectData.scale = ProjectScale.Medium;
        if (stage < 2 && _projectData.scale == ProjectScale.Medium) _projectData.scale = ProjectScale.Small;
    }

    // interactable 토글 + 버튼 자식의 "lockImage" 활성화/비활성화 (Small 은 항상 해금이라 자식 없음 — null 무시)
    void SetScaleLocked(Button b, bool locked)
    {
        if (b == null) return;
        b.interactable = !locked;
        var lockTf = b.transform.Find("lockImage");
        if (lockTf != null) lockTf.gameObject.SetActive(locked);
    }

    void RefreshScaleHighlight()
    {
        SetScaleBtnColor(scaleButtonSmall,  _projectData.scale == ProjectScale.Small);
        SetScaleBtnColor(scaleButtonMedium, _projectData.scale == ProjectScale.Medium);
        SetScaleBtnColor(scaleButtonLarge,  _projectData.scale == ProjectScale.Large);
    }

    // Sprite Swap 트랜지션은 hover/press 마다 overrideSprite 를 건드려 선택 표시가 지워지므로 Transition.None 으로 끄고
    // 원래(Normal) 스프라이트만 캐싱 — Selected/Disabled 스프라이트는 인스펙터에 이미 세팅된 spriteState 를 그대로 사용.
    void InitScaleSpriteState(Button b)
    {
        if (b == null) return;
        b.transition = Selectable.Transition.None;
        var img = b.GetComponent<Image>();
        if (img != null) _scaleNormalSprite[b] = img.sprite;
    }

    void SetScaleBtnColor(Button b, bool selected)
    {
        if (b == null) return;

        var img = b.GetComponent<Image>();
        if (img != null)
        {
            var normal = _scaleNormalSprite.TryGetValue(b, out var s) ? s : img.sprite;
            var state = b.spriteState;
            img.sprite = !b.interactable && state.disabledSprite != null ? state.disabledSprite
                       : selected && state.selectedSprite != null         ? state.selectedSprite
                       : normal;
        }

        var text = b.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
            text.color = !b.interactable ? scaleTextDisabledColor : (selected ? scaleTextSelectedColor : scaleTextNormalColor);
    }

    // PBottomPanel 플랫폼 버튼도 규모 버튼과 동일 패턴: Transition.None + spriteState.selectedSprite 캐싱.
    void InitPlatformSpriteState(Button b)
    {
        if (b == null) return;
        b.transition = Selectable.Transition.None;
        var img = b.GetComponent<Image>();
        if (img != null) _platformNormalSprite[b] = img.sprite;
    }

    // 선택된 플랫폼만 스프라이트를 selectedSprite 로 교체 + 알파 255, 그 외에는 원래 스프라이트 + 알파 0(숨김).
    void RefreshPlatformHighlight()
    {
        if (platformButtons == null) return;
        for (int i = 0; i < platformButtons.Length; i++)
        {
            var b = platformButtons[i];
            if (b == null) continue;
            bool selected = _platformChosen && _projectData.platform == (ProjectPlatform)i;

            var img = b.GetComponent<Image>();
            if (img == null) continue;

            var normal = _platformNormalSprite.TryGetValue(b, out var s) ? s : img.sprite;
            var state = b.spriteState;
            img.sprite = selected && state.selectedSprite != null ? state.selectedSprite : normal;

            var c = img.color;
            c.a = selected ? 1f : 0f;
            img.color = c;
        }
    }

    // 장착 특성 'c1'(devCostDiscount) 적용한 실제 개발금 — 표시·차감 공통 소스.
    int CurrentDevCost => TraitEffectApplier.ApplyDevCostDiscount(_projectData.Cost);

    void UpdateDevCost()
    {
        if (devCostText != null) devCostText.text = $"{CurrentDevCost:N0} G";
    }

    // ── 규모 선택 (인라인 토글, 패널 전환 없음) ──
    public void OnClickScale(int scale)
    {
        _projectData.scale = (ProjectScale)scale;
        RefreshScaleHighlight();
        UpdateDevCost();
    }

    // ── 뒤로가기 → 메인(SummaryPanel) 복귀, 장르/플랫폼 패널은 꺼짐 ──
    public void BackToMain() => ShowOnly(mainPanel);

    // ── 플랫폼/장르 패널 열기 (각각 별도 패널) ──
    public void OpenPlatformPanel() => ShowOnly(platformPanel);
    public void OpenGenrePanel()
    {
        UpdateGenreButtonLabels();
        ShowOnly(genrePanel);
    }

    // ── 플랫폼 선택 (platformPanel 의 버튼 OnClick) → 메인 복귀 ──
    public void OnClickPlatform(int platform)
    {
        _projectData.platform = (ProjectPlatform)platform;
        _platformChosen = true;
        RefreshPlatformHighlight();
        RefreshMain();
        ShowOnly(mainPanel);
    }

    // ── 장르 선택 (genrePanel 의 버튼 OnClick) → 메인 복귀 ──
    public void OnClickGenre(string genre)
    {
        _projectData.genre = System.Enum.Parse<ProjectGenre>(genre);
        SelectedGenrePopularity = GenrePopularityManager.Instance != null
            ? GenrePopularityManager.Instance.GetPopularity(_projectData.genre) : 1;
        SelectedGenreFatigue = GenreFatigueManager.Instance != null
            ? GenreFatigueManager.Instance.GetFatigue(_projectData.genre) : 0;

        _genreChosen = true;
        UpdateGenreButtonLabels();
        RefreshMain();
        ShowOnly(mainPanel);
    }

    // GenreBtn1~5 (Left/Right 10개) 코드 배선 — 인스펙터 persistent call 없이 Start() 에서 일괄 연결.
    void WireGenreButton(Button b, string genre)
    {
        if (b == null) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() => OnClickGenre(genre));
    }

    // ── 개발 시작 ─────────────────────────────
    public void OnClickStartDevelopment()
    {
        if (!_platformChosen || !_genreChosen)
        {
            // ProjectSetupUI 자신이 패널을 여는 시점에 ModalGate.Register(this)로 게이트를 쥐고 있어서,
            // bypassGate 없이 부르면 패널이 열려있는 동안 계속 대기(pending)만 되다 안 뜬다. 즉시 표시.
            AlertUI.Instance.Show("플랫폼과 장르를 선택해주세요.", null, bypassGate: true);
            return;
        }

        int cost = CurrentDevCost;
        if (!MoneyManager.Instance.CanAfford(cost))
        {
            AlertUI.Instance.ShowMoney($"개발금이 부족합니다.\n필요: {cost:N0}G / 보유: {MoneyManager.Instance.Gold:N0}G", null, null, bypassGate: true);
            return;
        }

        MoneyManager.Instance.SpendGold(cost, saveImmediately: false);

        SelectedScale = _projectData.scale;
        SelectedGenre = _projectData.genre;
        SelectedPlatform = _projectData.platform;

        SaveLastSelection(); // 다음 프로젝트 설정 시 기본값으로 복원되도록 로컬 저장

        if (DevelopmentManager.Instance.CurrentStage == ProjectStage.Sales)
            SalesUI.Instance.NotifyNewProjectStarted();

        Debug.Log($"개발 시작! {_projectData.ScaleToString()} / {_projectData.GenreToString()} / {_projectData.PlatformToString()} / 개발금: -{cost:N0}G");
        DevelopmentManager.Instance.StartDevelopment();
        InfoUI.Instance?.Show("개발 시작!");
        if (mainPanel != null) mainPanel.SetActive(false);
        // GameTimeManager.StartTime() 은 여기서 호출 안 함(StartDevelopment 내부가 자체 StopTime 을 또 쌓고,
        // 팀장 선택/투자 이벤트 체인 끝의 ForceStartTime 이 한 번에 정리하는 기존 설계) — 그러나 이 패널
        // 자체는 시각적으로 닫히므로 ModalGate 등록은 여기서 바로 풀어야 다른 모달이 안 막힌다.
        ModalGate.I.Unregister(this);
    }

    // ── 닫기 ──────────────────────────────────
    public void OnClickClose()
    {
        _projectData = new ProjectData();
        _genreChosen = false;
        _platformChosen = false;

        ShowOnly(null);
        GameTimeManager.Instance.StartTime();
        ModalGate.I.Unregister(this);
    }

    // ── 마지막 선택 기억 (한 회차 한정 — PlayerPrefs 로 영속화해 재접속에도 유지, 새 런 시작 시 ResetForNewRun 으로 클리어) ──
    const string PrefScale    = "ProjectSetup_LastScale";
    const string PrefGenre    = "ProjectSetup_LastGenre";
    const string PrefPlatform = "ProjectSetup_LastPlatform";

    void SaveLastSelection()
    {
        PlayerPrefs.SetInt(PrefScale,    (int)_projectData.scale);
        PlayerPrefs.SetInt(PrefGenre,    (int)_projectData.genre);
        PlayerPrefs.SetInt(PrefPlatform, (int)_projectData.platform);
        PlayerPrefs.Save();
    }

    // 이번 회차에 저장된 마지막 선택을 복원. 없으면(첫 프로젝트/새 런) 그대로 둠.
    void LoadLastSelection()
    {
        if (!PlayerPrefs.HasKey(PrefScale)) return;

        _projectData.scale = (ProjectScale)PlayerPrefs.GetInt(PrefScale);

        if (PlayerPrefs.HasKey(PrefPlatform))
        {
            _projectData.platform = (ProjectPlatform)PlayerPrefs.GetInt(PrefPlatform);
            _platformChosen = true;
        }

        if (PlayerPrefs.HasKey(PrefGenre))
        {
            _projectData.genre = (ProjectGenre)PlayerPrefs.GetInt(PrefGenre);
            _genreChosen = true;
            SelectedGenrePopularity = GenrePopularityManager.Instance != null
                ? GenrePopularityManager.Instance.GetPopularity(_projectData.genre) : 1;
            SelectedGenreFatigue = GenreFatigueManager.Instance != null
                ? GenreFatigueManager.Instance.GetFatigue(_projectData.genre) : 0;
        }
    }

    // 새 런 시작 시 호출 (NewRunInitializer) — 마지막 선택 기억 삭제 → 다음 프로젝트는 "선택하기" 로 시작.
    public static void ResetForNewRun()
    {
        PlayerPrefs.DeleteKey(PrefScale);
        PlayerPrefs.DeleteKey(PrefGenre);
        PlayerPrefs.DeleteKey(PrefPlatform);
        PlayerPrefs.Save();
    }

    // ── 패널 전환 ─────────────────────────────
    void ShowOnly(GameObject target)
    {
        if (mainPanel     != null) mainPanel.SetActive(target == mainPanel);
        if (genrePanel    != null) genrePanel.SetActive(target == genrePanel);
        if (platformPanel != null) platformPanel.SetActive(target == platformPanel);

        // 비활성 상태에서 미리 적용한 색은 Button.OnEnable 상태전환(targetGraphic 리셋)에 덮이고,
        // GetComponentInChildren 도 비활성 계층에서는 텍스트를 못 찾으므로 활성화 "이후" 재적용.
        if (target == mainPanel) RefreshMain();
        if (target == platformPanel) RefreshPlatformHighlight();
    }

    // ══════════════════════════════════════════
    // 장르 패널 인기도/피로도 — GenreBtnN 자식(PopPnael/FatPanel) 이미지 활성화 방식
    // ══════════════════════════════════════════
    [Header("Genre Buttons - Click")]
    public Button genreButtonRPG;
    public Button genreButtonFPS;
    public Button genreButtonArcade;
    public Button genreButtonHealingSimulation;
    public Button genreButtonHorror;
    public Button genreButtonIdle;
    public Button genreButtonRTS;
    public Button genreButtonVisualNovel;
    public Button genreButtonSports;
    public Button genreButtonPuzzle;

    [Header("Genre Buttons - Mastery (MasteryPanel/MasteryImage 공용 SO)")]
    public MasterySpriteSet masterySpriteSet;

    void UpdateGenreButtonLabels()
    {
        // Sales 진행 중인 경우 해당 장르를 포함해 피로도 재계산
        if (CompletedProjectManager.Instance != null && GenreFatigueManager.Instance != null)
            GenreFatigueManager.Instance.RebuildFromHistory(CompletedProjectManager.Instance.completedProjects);

        RefreshGenreIndicators(genreButtonRPG,               ProjectGenre.RPG);
        RefreshGenreIndicators(genreButtonFPS,               ProjectGenre.FPS);
        RefreshGenreIndicators(genreButtonArcade,            ProjectGenre.Arcade);
        RefreshGenreIndicators(genreButtonHealingSimulation, ProjectGenre.HealingSimulation);
        RefreshGenreIndicators(genreButtonHorror,            ProjectGenre.Horror);
        RefreshGenreIndicators(genreButtonIdle,              ProjectGenre.Idle);
        RefreshGenreIndicators(genreButtonRTS,               ProjectGenre.RTS);
        RefreshGenreIndicators(genreButtonVisualNovel,       ProjectGenre.VisualNovel);
        RefreshGenreIndicators(genreButtonSports,            ProjectGenre.Sports);
        RefreshGenreIndicators(genreButtonPuzzle,            ProjectGenre.Puzzle);
    }

    // FatPanel/FatImage1~3 는 피로도만큼, PopPnael/defaultImage(x3)의 자식 FireImage 는 인기도만큼 누적 활성화.
    // defaultImage 자체는 항상 표시되는 기본 이미지라 건드리지 않음.
    void RefreshGenreIndicators(Button genreButton, ProjectGenre genre)
    {
        if (genreButton == null) return;

        int fatigue = GenreFatigueManager.Instance    != null ? GenreFatigueManager.Instance.GetFatigue(genre)      : 0;
        int popular = GenrePopularityManager.Instance != null ? GenrePopularityManager.Instance.GetPopularity(genre) : 1;

        var fatPanel = genreButton.transform.Find("FatPanel");
        if (fatPanel != null)
            for (int i = 1; i <= 3; i++)
            {
                var img = fatPanel.Find($"FatImage{i}");
                if (img != null) img.gameObject.SetActive(fatigue >= i);
            }

        var popPanel = genreButton.transform.Find("PopPnael");
        if (popPanel != null)
            for (int i = 0; i < popPanel.childCount; i++)
            {
                var defaultImage = popPanel.GetChild(i);
                if (defaultImage.childCount == 0) continue;
                defaultImage.GetChild(0).gameObject.SetActive(popular >= i + 1);
            }

        // MasteryPanel/MasteryImage(+자식 Text) — 숙련도 등급에 따라 이미지 스왑 + 텍스트 갱신.
        var masteryPanel = genreButton.transform.Find("MasteryPanel");
        if (masteryPanel != null)
        {
            var masteryImageT = masteryPanel.Find("MasteryImage");
            if (masteryImageT != null)
            {
                var tier = MasteryManager.Instance != null ? MasteryManager.Instance.GetTier(genre) : MasteryTier.Novice;
                MasterySpriteSet.Apply(masteryImageT.GetComponent<Image>(), masterySpriteSet, tier);

                var masteryText = masteryImageT.GetComponentInChildren<TextMeshProUGUI>();
                if (masteryText != null) masteryText.text = MasteryManager.TierToString(tier);
            }
        }

        bool selected = _genreChosen && _projectData.genre == genre;
        var btnImg = genreButton.GetComponent<Image>();
        if (btnImg != null)
        {
            var normal = _genreNormalSprite.TryGetValue(genreButton, out var s) ? s : btnImg.sprite;
            var state = genreButton.spriteState;
            btnImg.sprite = selected && state.selectedSprite != null ? state.selectedSprite : normal;

            var c = btnImg.color;
            c.a = selected ? 1f : 0f;
            btnImg.color = c;
        }
    }

    // GenreBtn1~10 도 규모/플랫폼 버튼과 동일 패턴: Transition.None + 원래(Normal) 스프라이트 캐싱.
    void InitGenreSpriteState(Button b)
    {
        if (b == null) return;
        b.transition = Selectable.Transition.None;
        var img = b.GetComponent<Image>();
        if (img != null) _genreNormalSprite[b] = img.sprite;
    }
}
