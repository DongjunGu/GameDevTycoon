using System.Collections;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUDCanvas/TutorialPanel — 튜토리얼 전용 대사창 (DialogUI와 별개 시스템, TutorialDialog 차트 전용).
// stepGroup(예: "1-1") 단위로 줄 목록을 순차 재생 — 첫 줄은 PortraitImage → TutorialTextPanel →
// TutorialText → nextButton 순서로 하나씩 하단에서 위로 fade-in(등장), 이후 줄은 텍스트/버튼만
// 같은 방식으로 다시 fade-in. nextButton 클릭을 기다렸다가 다음 줄로 진행.
[DisallowMultipleComponent]
public class TutorialPanelUI : MonoBehaviour
{
    public static TutorialPanelUI Instance { get; private set; }

    [Header("References")]
    [Tooltip("비워두면 이 컴포넌트가 붙은 오브젝트(TutorialPanel 자신)를 켜고 끔")]
    public GameObject panelRoot;
    public Image portraitImage;                // PortraitImage
    public RectTransform tutorialTextPanel;    // TutorialTextPanel (텍스트 배경)
    public TextMeshProUGUI tutorialText;       // TutorialText
    public Button nextButton;                  // nextButton
    public TextMeshProUGUI nextButtonText;     // nextButton/nextText
    public Image nextButtonHandImage;          // nextButton/handImage

    [Header("등장 연출 (하단 → 상단 fade-in)")]
    [Tooltip("요소 하나가 등장하는 시간(초)")]
    public float revealDuration = 0.25f;
    [Tooltip("등장 시 제자리보다 얼마나 아래에서 시작해 올라올지(px)")]
    public float revealRiseDistance = 30f;

    [Header("강조 색상")]
    [Tooltip("대사의 [[강조할 텍스트]] 를 이 색으로 치환")]
    public string highlightColorHex = "#FFD966";

    static readonly Regex HighlightPattern = new(@"\[\[(.+?)\]\]", RegexOptions.Compiled);

    bool _clicked;
    RectTransform _panelRect;

    // 각 요소의 "제자리" anchoredPosition — 최초 1회만 캐싱해서 등장 애니메이션의 도착 지점으로 재사용.
    Vector2? _portraitRestPos;
    Vector2? _textPanelRestPos;
    Vector2? _textRestPos;
    Vector2? _nextButtonRestPos;
    Vector2? _nextTextRestPos;
    Vector2? _handImageRestPos;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        if (panelRoot == null) panelRoot = gameObject;
        _panelRect = panelRoot.GetComponent<RectTransform>();
        panelRoot.SetActive(false);
        if (nextButton != null) nextButton.onClick.AddListener(() => _clicked = true);
    }

    // 어떤 패널(다른 루트 Canvas에 떠 있는 모달 포함) 보다도 항상 위에 뜨도록 강제 — AlertUI.EnsureTopMost와
    // 동일 패턴. 중첩 Canvas에 overrideSorting을 걸면 루트 Canvas 순서와 무관하게 전역에서 독립적으로 정렬된다.
    // ⚠️ TutorialPanel(MenuCanvas/TutorialPanel)에는 씬에 이미 ModalLayer가 붙어있는데, 이게 ModalBlocker
    // 스택에 등록된 채로 있으면 ModalBlocker.ApplyOrder()가 다른 모달이 열고 닫힐 때마다 이 Canvas의
    // sortingOrder를 스택 계산값(보통 수십대)으로 계속 되돌려버려서, 여기서 아무리 높은 값을 넣어도 소용없고
    // (프로젝트_alertui3_dispatch_invisible_bug와 동일 패턴으로) 공유 딤까지 끌어올려져 dim이 이중으로
    // 겹쳐 보인다. AlertUI처럼 "자체 dim(TutorialHighlighter) + EnsureTopMost"로 완결된 패널이라 ModalBlocker
    // 관리가 필요 없음 — 아예 꺼서 등록 해제(Unregister)시킨다.
    void EnsureTopMost()
    {
        var modalLayer = panelRoot.GetComponent<ModalLayer>();
        if (modalLayer != null) modalLayer.enabled = false;

        var canvas = panelRoot.GetComponent<Canvas>();
        if (canvas == null) canvas = panelRoot.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder    = 32500; // AlertUI(32000)보다도 위 — "어떤 것보다 항상 제일 위"
        if (panelRoot.GetComponent<GraphicRaycaster>() == null)
            panelRoot.AddComponent<GraphicRaycaster>();
        panelRoot.transform.SetAsLastSibling();
    }

    // 디버그 강제 정리 전용(TutorialController.ForceResetToNormal/ForceResetAndStart) — PlayStepGroup
    // 코루틴이 StopAllCoroutines() 등으로 도중에 죽어서 마지막 panelRoot.SetActive(false)를 못 타면
    // 패널이 화면에 고아로 남는다. 그걸 즉시 정리한다. 정상 흐름에서는 쓰지 말 것.
    public void ForceHideImmediate()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        _clicked = false;
    }

    // stepGroup 전체(여러 줄)를 순차 재생 — 마지막 줄 클릭까지 끝나면 반환.
    // anchoredPosition 지정 시 표시 전에 패널 위치를 그 값으로 옮김(스텝마다 다른 위치에 띄우고 싶을 때 사용).
    public IEnumerator PlayStepGroup(string stepGroup, Vector2? anchoredPosition = null)
    {
        if (string.IsNullOrEmpty(stepGroup)) yield break;

        if (!TutorialDialogChartLoader.Cache.TryGetValue(stepGroup, out var lines) || lines == null || lines.Count == 0)
        {
            Debug.LogWarning($"[TutorialPanelUI] stepGroup '{stepGroup}' 대사 없음 - 스킵");
            yield break;
        }

        if (anchoredPosition.HasValue && _panelRect != null)
            _panelRect.anchoredPosition = anchoredPosition.Value;

        EnsureTopMost();
        panelRoot.SetActive(true);

        for (int i = 0; i < lines.Count; i++)
            yield return PlayLine(lines[i], isFirst: i == 0);

        panelRoot.SetActive(false);
    }

    IEnumerator PlayLine(TutorialDialogLine line, bool isFirst)
    {
        bool hasPortrait = ApplyPortrait(line.portraitId);

        if (isFirst)
        {
            // 최초 등장 — 초상화 → 텍스트배경 → 텍스트 → 다음버튼 순서로 하나씩 하단에서 위로 fade-in.
            HideAllForReveal();

            if (hasPortrait && portraitImage != null)
                yield return RevealElement(portraitImage.rectTransform, GetPortraitRestPos());
            else if (portraitImage != null)
                portraitImage.gameObject.SetActive(false);

            if (tutorialTextPanel != null)
                yield return RevealElement(tutorialTextPanel, GetTextPanelRestPos());

            SetFullText(line.text ?? "");
            if (tutorialText != null)
                yield return RevealElement(tutorialText.rectTransform, GetTextRestPos());

            if (nextButton != null)
                yield return RevealNextButtonGroup();
        }
        else
        {
            // 이후 줄 — 초상화/텍스트배경은 이미 나와있으니 유지(초상화는 스프라이트만 즉시 갱신),
            // 텍스트와 다음버튼만 다시 하단에서 위로 fade-in.
            if (portraitImage != null) portraitImage.gameObject.SetActive(hasPortrait);

            if (nextButton != null) nextButton.gameObject.SetActive(false);

            SetFullText(line.text ?? "");
            if (tutorialText != null)
                yield return RevealElement(tutorialText.rectTransform, GetTextRestPos());

            if (nextButton != null)
                yield return RevealNextButtonGroup();
        }

        _clicked = false;
        yield return new WaitUntil(() => _clicked);
    }

    // stepGroup 재생 시작 전 4요소를 전부 숨김 상태로 되돌린다 — 이후 RevealElement가 순서대로 켠다.
    void HideAllForReveal()
    {
        if (portraitImage != null)     portraitImage.gameObject.SetActive(false);
        if (tutorialTextPanel != null) tutorialTextPanel.gameObject.SetActive(false);
        if (tutorialText != null)      tutorialText.gameObject.SetActive(false);
        if (nextButton != null)        nextButton.gameObject.SetActive(false);
    }

    Vector2 GetPortraitRestPos()
    {
        _portraitRestPos ??= portraitImage.rectTransform.anchoredPosition;
        return _portraitRestPos.Value;
    }

    Vector2 GetTextPanelRestPos()
    {
        _textPanelRestPos ??= tutorialTextPanel.anchoredPosition;
        return _textPanelRestPos.Value;
    }

    Vector2 GetTextRestPos()
    {
        _textRestPos ??= tutorialText.rectTransform.anchoredPosition;
        return _textRestPos.Value;
    }

    Vector2 GetNextButtonRestPos()
    {
        _nextButtonRestPos ??= ((RectTransform)nextButton.transform).anchoredPosition;
        return _nextButtonRestPos.Value;
    }

    Vector2 GetRestPos(RectTransform rt, ref Vector2? cache)
    {
        cache ??= rt.anchoredPosition;
        return cache.Value;
    }

    // nextButton 자신과 그 안의 nextText/handImage를 동시에(병렬로) 하단에서 위로 fade-in.
    IEnumerator RevealNextButtonGroup()
    {
        var textRect = nextButtonText != null ? nextButtonText.rectTransform : null;
        var handRect = nextButtonHandImage != null ? nextButtonHandImage.rectTransform : null;

        if (textRect != null) StartCoroutine(RevealElement(textRect, GetRestPos(textRect, ref _nextTextRestPos)));
        if (handRect != null) StartCoroutine(RevealElement(handRect, GetRestPos(handRect, ref _handImageRestPos)));

        yield return RevealElement((RectTransform)nextButton.transform, GetNextButtonRestPos());
    }

    // rt를 restPos보다 revealRiseDistance만큼 아래에서 시작해, alpha 0→1 + 위치를 restPos로 올리며 등장시킨다.
    IEnumerator RevealElement(RectTransform rt, Vector2 restPos)
    {
        if (rt == null) yield break;

        rt.gameObject.SetActive(true);

        // PortraitImage/TutorialTextPanel은 TutorialPanel의 HorizontalLayoutGroup 자식이라 SetActive
        // 직후 레이아웃이 지연 리빌드되며 밑에서 설정할 시작 위치를 되돌려버릴 수 있다 — 그 전에 즉시
        // 강제 리빌드해 레이아웃을 먼저 확정시킨 뒤 시작 위치를 덮어쓴다.
        if (rt.parent is RectTransform parentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);

        var cg = rt.GetComponent<CanvasGroup>();
        if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();

        Vector2 startPos = restPos + new Vector2(0f, -revealRiseDistance);
        rt.anchoredPosition = startPos;
        cg.alpha = 0f;

        float dur = Mathf.Max(0.0001f, revealDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float eased = 1f - (1f - k) * (1f - k); // ease-out
            cg.alpha = eased;
            rt.anchoredPosition = Vector2.Lerp(startPos, restPos, eased);
            yield return null;
        }
        cg.alpha = 1f;
        rt.anchoredPosition = restPos;
    }

    // 초상화 스프라이트만 갱신 — 활성/비활성 토글은 호출부(PlayLine)가 담당.
    bool ApplyPortrait(string portraitId)
    {
        if (portraitImage == null) return false;
        if (string.IsNullOrEmpty(portraitId)) return false;

        var sprite = Resources.Load<Sprite>($"Portraits/Tutorial/{portraitId}");
        if (sprite == null)
        {
            Debug.LogWarning($"[TutorialPanelUI] 초상화 없음: Portraits/Tutorial/{portraitId}");
            return false;
        }

        portraitImage.sprite = sprite;
        return true;
    }

    // 타이핑 대신 강조 태그만 치환해서 한 번에 전체 텍스트를 세팅 — 등장 연출(RevealElement)이 노출을 담당.
    void SetFullText(string rawText)
    {
        if (tutorialText == null) return;
        tutorialText.text = HighlightPattern.Replace(rawText, $"<color={highlightColorHex}>$1</color>");
    }
}
