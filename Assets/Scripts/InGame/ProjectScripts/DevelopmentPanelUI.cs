using UnityEngine;
using TMPro;

public class DevelopmentPanelUI : MonoBehaviour
{
    public static DevelopmentPanelUI Instance { get; private set; }

    [Header("UI")]
    public TextMeshProUGUI planningText;
    public TextMeshProUGUI developText;
    public TextMeshProUGUI artText;
    public TextMeshProUGUI bugText;
    public TextMeshProUGUI creativityText;

    [Tooltip("프로젝트 개발 중이 아닐 때만 표시되는 기본 텍스트 (개발 중이면 자동 비활성)")]
    public GameObject defaultText;

    [Header("애니메이션")]
    [Tooltip("표시값이 1 변할 때마다 걸리는 시간 (초). 0 이하면 즉시 반영")]
    public float tickInterval = 0.15f;

    public float GetPlanning() => _planning;
    public float GetDevelop() => _develop;
    public float GetArt() => _art;
    public float GetBug() => _bug;
    public float GetCreativity() => _creativity;

    // 실제 누적값 (저장/로직이 사용)
    private float _planning;
    private float _develop;
    private float _art;
    private float _bug;
    private float _creativity;

    // 화면 표시용 (실제값을 향해 1씩 따라잡음)
    private float _planningDisplay;
    private float _developDisplay;
    private float _artDisplay;
    private float _bugDisplay;
    private float _creativityDisplay;

    private float _tickAccumulator;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start() => UpdateDefaultText();

    void Update()
    {
        UpdateDefaultText(); // 시간 정지 중에도 갱신해야 하므로 IsRunning 게이트보다 위
        if (GameTimeManager.Instance != null && !GameTimeManager.Instance.IsRunning) return;
        if (tickInterval <= 0f)
        {
            SyncDisplayInstantly();
            return;
        }

        _tickAccumulator += Time.deltaTime;
        if (_tickAccumulator < tickInterval) return;

        int steps = Mathf.FloorToInt(_tickAccumulator / tickInterval);
        _tickAccumulator -= steps * tickInterval;

        bool changed = false;
        changed |= MoveDisplay(ref _planningDisplay,   _planning,   steps);
        changed |= MoveDisplay(ref _developDisplay,    _develop,    steps);
        changed |= MoveDisplay(ref _artDisplay,        _art,        steps);
        changed |= MoveDisplay(ref _bugDisplay,        _bug,        steps);
        changed |= MoveDisplay(ref _creativityDisplay, _creativity, steps);

        if (changed) UpdateUI();
    }

    private static bool MoveDisplay(ref float display, float target, int steps)
    {
        if (Mathf.Approximately(display, target)) return false;
        float prev = display;
        if (display < target)
            display = Mathf.Min(target, display + steps);
        else
            display = Mathf.Max(target, display - steps);
        return !Mathf.Approximately(prev, display);
    }

    private void SyncDisplayInstantly()
    {
        _planningDisplay   = _planning;
        _developDisplay    = _develop;
        _artDisplay        = _art;
        _bugDisplay        = _bug;
        _creativityDisplay = _creativity;
        UpdateUI();
    }

    public void SetValues(float planning, float develop, float art, float bug, float creativity)
    {
        _planning   = planning;
        _develop    = develop;
        _art        = art;
        _bug        = bug;
        _creativity = creativity;
        // 저장 로드 등 — 애니메이션 없이 즉시 일치
        SyncDisplayInstantly();
    }

    public void ResetValues()
    {
        _planning = 0f;
        _develop = 0f;
        _art = 0f;
        _bug = 0f;
        _creativity = 0f;

        _planningDisplay = 0f;
        _developDisplay = 0f;
        _artDisplay = 0f;
        _bugDisplay = 0f;
        _creativityDisplay = 0f;
        _tickAccumulator = 0f;

        planningText.text = "";
        developText.text = "";
        artText.text = "";
        bugText.text = "";
        creativityText.text = "";
    }

    public void AddValues(float planning, float develop, float art, float bug, float creativity = 0f)
    {
        _planning   += planning;
        _develop    += develop;
        _art        += art;
        _bug        += bug;
        _creativity += creativity;
        // Update()가 표시값을 1씩 따라잡음
    }

    // 호출자가 직접 1씩 틱하는 경우(LeaderScoreUI 등) — 표시값도 즉시 동기화
    public void AddValuesInstant(float planning, float develop, float art, float bug, float creativity = 0f)
    {
        _planning   += planning;
        _develop    += develop;
        _art        += art;
        _bug        += bug;
        _creativity += creativity;
        SyncDisplayInstantly();
    }

    public void UpdateUI()
    {
        planningText.text   = $"기획: {Mathf.RoundToInt(_planningDisplay)}";
        developText.text    = $"개발: {Mathf.RoundToInt(_developDisplay)}";
        artText.text        = $"아트: {Mathf.RoundToInt(_artDisplay)}";
        bugText.text        = $"버그: {Mathf.RoundToInt(_bugDisplay)}";
        creativityText.text = $"창의성: {Mathf.RoundToInt(_creativityDisplay)}";
    }

    // 실제 개발 단계(Developing/BugFixing)에서만 defaultText 비활성.
    // 마케팅·판매·완료·대기 단계에서는 표시한다.
    // defaultText 표시 중에는 스탯/타이머 텍스트를 모두 숨긴다(반대로 동기화).
    void UpdateDefaultText()
    {
        if (defaultText == null) return;
        var dm = DevelopmentManager.Instance;
        bool developing = dm != null
            && (dm.CurrentStage == ProjectStage.Developing || dm.CurrentStage == ProjectStage.BugFixing);
        if (defaultText.activeSelf == developing) defaultText.SetActive(!developing);
        SetStatTextsVisible(developing); // 매 프레임 동기화 (초기 상태 어긋남 방지, enabled set 은 무비용)
    }

    // statKey(개발틱 종류) → 해당 스탯 텍스트 RectTransform. StatTickPopup 흡입 타겟용.
    // blank(꽝) 등 매핑 없는 종류는 null → 흡입 없이 제자리 소멸.
    public RectTransform GetStatTextRect(string statKey) => statKey switch
    {
        "planning"   => planningText   != null ? planningText.rectTransform   : null,
        "develop"    => developText    != null ? developText.rectTransform    : null,
        "art"        => artText        != null ? artText.rectTransform        : null,
        "bug"        => bugText        != null ? bugText.rectTransform         : null,
        "creativity" => creativityText != null ? creativityText.rectTransform : null,
        _ => null
    };

    // 개발 중에만 스탯/타이머 텍스트 표시. enabled 토글로 layout 자리 유지.
    void SetStatTextsVisible(bool visible)
    {
        if (planningText   != null) planningText.enabled   = visible;
        if (developText    != null) developText.enabled    = visible;
        if (artText        != null) artText.enabled        = visible;
        if (creativityText != null) creativityText.enabled = visible;
        if (bugText        != null) bugText.enabled        = visible;
        DevelopmentTimerUI.Instance?.SetTextVisible(visible);
    }

    public void SetBug(float value)
    {
        _bug = value;
        // Update()가 _bugDisplay를 따라잡음
    }

    public void MultiplyValues(float multiplier)
    {
        _planning *= multiplier;
        _develop  *= multiplier;
        _art      *= multiplier;
        // Update()가 따라잡음
    }
}
