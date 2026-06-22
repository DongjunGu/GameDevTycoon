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

    // 화면 표시용 (공개값을 향해 1씩 따라잡음)
    private float _planningDisplay;
    private float _developDisplay;
    private float _artDisplay;
    private float _bugDisplay;
    private float _creativityDisplay;

    // 공개값 — 표시값(Display)이 따라잡는 목표. 실제값과 분리해 개발틱 팝업이 패널로 빨려든 뒤
    // RevealValues 로만 공개된다(흡입 후 카운트업 연출). 그 외 경로(로드/즉시가산/배수)는 실제값과 동시에 공개.
    private float _planningReveal;
    private float _developReveal;
    private float _artReveal;
    private float _bugReveal;
    private float _creativityReveal;

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
        changed |= MoveDisplay(ref _planningDisplay,   _planningReveal,   steps);
        changed |= MoveDisplay(ref _developDisplay,    _developReveal,    steps);
        changed |= MoveDisplay(ref _artDisplay,        _artReveal,        steps);
        changed |= MoveDisplay(ref _bugDisplay,        _bugReveal,        steps);
        changed |= MoveDisplay(ref _creativityDisplay, _creativityReveal, steps);

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
        _planningDisplay   = _planningReveal;
        _developDisplay    = _developReveal;
        _artDisplay        = _artReveal;
        _bugDisplay        = _bugReveal;
        _creativityDisplay = _creativityReveal;
        UpdateUI();
    }

    public void SetValues(float planning, float develop, float art, float bug, float creativity)
    {
        _planning   = planning;
        _develop    = develop;
        _art        = art;
        _bug        = bug;
        _creativity = creativity;
        // 저장 로드 등 — 흡입 연출 없이 실제값을 즉시 공개 + 표시
        _planningReveal   = planning;
        _developReveal    = develop;
        _artReveal        = art;
        _bugReveal        = bug;
        _creativityReveal = creativity;
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

        _planningReveal = 0f;
        _developReveal = 0f;
        _artReveal = 0f;
        _bugReveal = 0f;
        _creativityReveal = 0f;
        _tickAccumulator = 0f;

        planningText.text = "";
        developText.text = "";
        artText.text = "";
        bugText.text = "";
        creativityText.text = "";
    }

    // 개발틱 산출 — 실제값(저장/로직)만 즉시 증가. 화면 표시값은 개발틱 팝업이 패널로 빨려든 뒤
    // RevealValues 로 공개될 때까지 오르지 않는다(흡입 후 카운트업 연출).
    public void AddValues(float planning, float develop, float art, float bug, float creativity = 0f)
    {
        _planning   += planning;
        _develop    += develop;
        _art        += art;
        _bug        += bug;
        _creativity += creativity;
    }

    // 개발틱 팝업이 패널로 빨려든 뒤(StatTickPopup.Finish) 호출 — 해당 스탯 표시값을 amount 만큼 공개.
    // Update()가 표시값을 공개값까지 1씩 따라잡으며 카운트업.
    public void RevealValues(string statKey, float amount)
    {
        if (amount == 0f) return;
        switch (statKey)
        {
            case "planning":   _planningReveal   += amount; break;
            case "develop":    _developReveal    += amount; break;
            case "art":        _artReveal        += amount; break;
            case "bug":        _bugReveal        += amount; break;
            case "creativity": _creativityReveal += amount; break;
        }
    }

    // 호출자가 직접 1씩 틱하는 경우(LeaderScoreUI 등) — 실제값/공개값/표시값 즉시 동기화
    public void AddValuesInstant(float planning, float develop, float art, float bug, float creativity = 0f)
    {
        _planning   += planning;
        _develop    += develop;
        _art        += art;
        _bug        += bug;
        _creativity += creativity;
        _planningReveal   += planning;
        _developReveal    += develop;
        _artReveal        += art;
        _bugReveal        += bug;
        _creativityReveal += creativity;
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
        _bugReveal = value;   // 버그 작업은 흡입 연출 없이 즉시 반영
        // Update()가 _bugDisplay를 따라잡음
    }

    public void MultiplyValues(float multiplier)
    {
        _planning *= multiplier;
        _develop  *= multiplier;
        _art      *= multiplier;
        _planningReveal *= multiplier;
        _developReveal  *= multiplier;
        _artReveal      *= multiplier;
        // Update()가 따라잡음
    }
}
