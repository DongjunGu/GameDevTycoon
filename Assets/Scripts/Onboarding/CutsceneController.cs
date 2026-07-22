using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

// 로딩 완료 후 첫 게스트에게 보여주는 컷씬 (LoadingScene 에 배치).
//
// steps 를 클릭 순서대로 진행한다. 각 step:
//   - image       : 이 단계에서 보여줄 이미지 GameObject
//   - clearBefore : true 면 기존 이미지를 크로스페이드로 교체. false 면 기존 위에 누적(좌→우 등).
//   - autoPan     : true 면 표시 후 이미지를 좌상단→우하단으로 panDuration 초 동안 자동 이동(큰 이미지용).
// 전환은 크로스페이드(fadeDuration). 마지막 step 표시 후 한 번 더 클릭하면 onComplete (게임씬 이동).
// skipButton 을 누르면 진행 중인 단계 무관하게 즉시 마지막까지 본 것처럼 종료(Skip()).
//
// ⚠️ 큰 이미지(autoPan)는 RectTransform 이 화면(부모)보다 커야 하고, 앵커/피벗 중앙 + 고정 크기여야 한다.
//    팬은 anchoredPosition 을 좌상↔우하 코너로 이동시켜 구현(부모보다 넘치는 만큼).
// ⚠️ 이미지는 각 step.image 의 Sprite 만 교체하면 됨. steps 가 비어있으면 컷씬 스킵.
//
// 클릭 입력은 New Input System(Mouse/Touchscreen) 사용. 팬 중에도 클릭하면 다음 단계로 진행.
public class CutsceneController : MonoBehaviour
{
    [Serializable]
    public class Step
    {
        [Tooltip("이 단계에서 보여줄 이미지")]
        public GameObject image;

        [Tooltip("표시 전에 기존 컷씬 이미지를 크로스페이드로 교체. 누적 표시면 false.")]
        public bool clearBefore = true;

        [Tooltip("이 단계에서 하단에 타이핑될 대사 (비우면 대사창 숨김)")]
        [TextArea]
        public string line;

        [Header("자동 팬 (큰 이미지용)")]
        [Tooltip("true 면 표시 후 이미지를 좌상단→우하단으로 자동 이동")]
        public bool autoPan;

        [Tooltip("팬 지속 시간(초)")]
        public float panDuration = 3f;

        [Header("자동 줌 (확대 연출)")]
        [Tooltip("true 면 표시 후 이미지를 zoomFrom→zoomTo 배율로 서서히 확대")]
        public bool autoZoom;

        [Tooltip("줌 지속 시간(초)")]
        public float zoomDuration = 3f;

        [Tooltip("시작 배율")]
        public float zoomFrom = 1f;

        [Tooltip("끝 배율")]
        public float zoomTo = 3f;

        [Tooltip("줌 동안 alpha 0→1 로 페이드인할 자식 이미지(선택). 크로스페이드 중엔 숨겨짐.")]
        public Image zoomFadeChild;

        [Header("애니메이션 (Animator)")]
        [Tooltip("true 면 이미지의 Animator 클립이 끝난 뒤 대사 시작")]
        public bool waitForAnim;

        [Tooltip("대기할 Animator (비우면 image 에서 자동 탐색). 1회 재생 클립(Loop off) 권장.")]
        public Animator animator;

        [Header("자동 넘김")]
        [Tooltip("true 면 클릭 없이 일정 시간 뒤 자동으로 다음 단계로 넘어감")]
        public bool autoAdvance;

        [Tooltip("자동 넘김까지 대기 시간(초). 대사 타이핑이 끝난 뒤부터 카운트. (대사 없으면 표시 직후부터)")]
        public float autoAdvanceDelay = 2f;
    }

    [Tooltip("컷씬 전체를 감싸는 풀스크린 루트(시작 시 비활성). 비우면 이 GameObject 사용.")]
    public GameObject root;

    [Tooltip("클릭 순서대로 진행할 단계들. 비어있으면 컷씬 스킵.")]
    public List<Step> steps = new();

    [Tooltip("각 단계 표시 후 이 시간(초) 동안 클릭 무시 (오입력 방지).")]
    public float clickLockSeconds = 0.5f;

    [Tooltip("크로스페이드 전환 시간(초).")]
    public float fadeDuration = 0.4f;

    [Header("대사 (하단 타이핑)")]
    [Tooltip("대사 배경 박스(선택). 대사 있는 단계에서만 표시.")]
    public GameObject dialogueBox;

    [Tooltip("대사가 타이핑될 텍스트(TMP).")]
    public TextMeshProUGUI dialogueText;

    [Tooltip("글자당 간격(초). 작을수록 빠름.")]
    public float charInterval = 0.04f;

    [Header("스킵")]
    [Tooltip("누르면 컷씬을 마지막 단계까지 본 것처럼 즉시 종료(onComplete 호출). 비워두면 코드에서 Skip()을 직접 호출해야 함.")]
    public Button skipButton;

    Action _onComplete;
    int    _index;
    bool   _playing;
    bool   _busy;       // 전환(페이드) 중 입력 무시
    float  _lockUntil;
    readonly List<GameObject> _visible = new();
    Coroutine _panCo;
    Coroutine _zoomCo;
    Coroutine _typeCo;
    Coroutine _autoCo;  // 자동 넘김 대기
    bool      _typing;
    string    _currentLine;
    bool      _panning;  // 팬 진행 중(대사 시작 전)
    bool      _skipPan;  // 팬 중 클릭 → 팬 즉시 완료 요청

    void Awake()
    {
        if (root == null) root = gameObject;
        root.SetActive(false);
        HideAllImagesImmediate();
        if (dialogueBox != null) dialogueBox.SetActive(false);
        if (skipButton != null)
        {
            skipButton.onClick.AddListener(Skip);
            skipButton.gameObject.SetActive(false); // 컷씬 재생 중에만 노출(Play/Finish 에서 토글)
        }
    }

    // 스킵 버튼 OnClick — 진행 중인 단계와 무관하게 "마지막 단계까지 다 본 것"처럼 즉시 종료.
    // 진행 중인 코루틴(페이드/팬/줌/타이핑/자동넘김)을 전부 끊고 Finish()와 동일한 정리+onComplete 호출.
    public void Skip()
    {
        if (!_playing) return;
        StopAllCoroutines();
        Finish();
    }

    // 컷씬 재생. 마지막 단계 후 클릭하면(또는 steps 가 없으면 즉시) onComplete 호출.
    public void Play(Action onComplete)
    {
        _onComplete = onComplete;

        if (steps == null || steps.Count == 0)
        {
            Debug.Log("[Cutscene] steps 없음 — 컷씬 스킵");
            Finish();
            return;
        }

        root.SetActive(true);
        if (skipButton != null) skipButton.gameObject.SetActive(true);
        HideAllImagesImmediate();
        _playing = true;
        _index = -1;
        Advance(); // 첫 단계 표시
    }

    void Update()
    {
        if (!_playing || _busy) return;
        if (Time.unscaledTime < _lockUntil) return;
        if (!PressedThisFrame()) return;

        // 팬 중이면 클릭 시 팬 즉시 완료(그 뒤 대사), 타이핑 중이면 즉시 완성, 둘 다 끝났으면 다음 단계
        if (_panning) { _skipPan = true; return; }
        if (_typing)  { CompleteTyping(); return; }
        Advance();
    }

    void Advance()
    {
        StopEffects();
        _panning = false;
        _skipPan = false;
        _index++;

        // 마지막 단계까지 본 뒤 클릭 → 종료
        if (_index >= steps.Count)
        {
            Finish();
            return;
        }

        StartCoroutine(ShowStep(steps[_index]));
    }

    IEnumerator ShowStep(Step step)
    {
        _busy = true;

        // 전환(크로스페이드) 시작과 동시에 이전 대사 즉시 제거
        ClearDialogue();

        // 새 이미지 알파 0 으로 켜기
        var newImg = step.image;
        CanvasGroup newCg = null;
        if (newImg != null)
        {
            newCg = EnsureCanvasGroup(newImg);
            newImg.transform.SetAsLastSibling(); // 새 이미지를 위로
            newImg.SetActive(true);
            newCg.alpha = 0f;
            // 줌 페이드인 자식은 크로스페이드 중 숨김 → 줌 동안 0→1
            if (step.autoZoom && step.zoomFadeChild != null) SetAlpha(step.zoomFadeChild, 0f);
        }

        // clearBefore 면 기존 이미지들과 크로스페이드
        var fadingOut = step.clearBefore ? new List<GameObject>(_visible) : null;

        float dur = Mathf.Max(0.01f, fadeDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            if (newCg != null) newCg.alpha = k;
            if (fadingOut != null)
                foreach (var go in fadingOut)
                {
                    if (go == null) continue;
                    var cg = go.GetComponent<CanvasGroup>();
                    if (cg != null) cg.alpha = 1f - k;
                }
            yield return null;
        }
        if (newCg != null) newCg.alpha = 1f;

        // 페이드아웃된 기존 이미지 정리
        if (fadingOut != null)
        {
            foreach (var go in fadingOut)
                if (go != null) go.SetActive(false);
            _visible.Clear();
        }
        if (newImg != null && !_visible.Contains(newImg)) _visible.Add(newImg);

        _lockUntil = Time.unscaledTime + clickLockSeconds;
        _busy = false;

        var rt = newImg != null ? newImg.GetComponent<RectTransform>() : null;

        // 자동 줌 (확대) + 자식 이미지 페이드인 — 즉시 시작 (대사를 막지 않음)
        if (step.autoZoom && rt != null)
            _zoomCo = StartCoroutine(ZoomImage(rt, step.zoomFadeChild, step.zoomFrom, step.zoomTo, step.zoomDuration));

        // 자동 팬: 팬이 끝난 뒤에 대사 시작 (팬 중 클릭하면 팬 즉시 완료 후 대사)
        if (step.autoPan && rt != null)
        {
            _panning = true;
            _skipPan = false;
            _panCo = StartCoroutine(PanImage(rt, step.panDuration));
            yield return _panCo;
            _panning = false;
        }

        // 애니메이션: Animator 클립이 끝난 뒤 대사 시작 (재생 중 클릭하면 대기 건너뜀)
        if (step.waitForAnim)
        {
            var anim = step.animator != null ? step.animator
                       : (newImg != null ? newImg.GetComponentInChildren<Animator>() : null);
            if (anim != null)
            {
                _panning = true;   // "대사 전 대기" 상태 재사용(클릭 시 _skipPan 으로 건너뜀)
                _skipPan = false;
                yield return WaitForAnimation(anim);
                _panning = false;
            }
        }

        // 하단 대사 타이핑
        StartTyping(step.line);

        // 자동 넘김: 대사 타이핑 완료 후 delay 초 뒤 클릭 없이 다음 단계로 (그 전에 클릭하면 수동 진행)
        if (step.autoAdvance)
            _autoCo = StartCoroutine(AutoAdvanceAfter(step.autoAdvanceDelay));
    }

    // 타이핑이 끝난 뒤 delay 초 대기 후 자동 진행. (StopEffects/Advance 가 호출되면 취소됨)
    IEnumerator AutoAdvanceAfter(float delay)
    {
        while (_typing) yield return null;            // 대사 다 나올 때까지 대기
        float t = 0f;
        float d = Mathf.Max(0f, delay);
        while (t < d) { t += Time.unscaledDeltaTime; yield return null; }
        _autoCo = null;
        Advance();
    }

    // Animator 의 현재(레이어0) 1회 재생 클립이 끝날 때까지 대기. 클릭(_skipPan)/루프/타임아웃 시 조기 종료.
    IEnumerator WaitForAnimation(Animator anim)
    {
        yield return null; // 상태 진입 대기
        float safety = 0f;
        while (!_skipPan)
        {
            safety += Time.unscaledDeltaTime;
            if (safety >= 30f) break; // 안전 타임아웃

            if (!anim.IsInTransition(0))
            {
                var st = anim.GetCurrentAnimatorStateInfo(0);
                if (st.loop) break;                // 루프 클립이면 무한 대기 방지
                if (st.normalizedTime >= 1f) break; // 1회 재생 완료
            }
            yield return null;
        }
    }

    // 이미지를 from→to 배율로 서서히 확대(피벗 중심). 확대분은 화면 밖으로 잘림.
    // fadeChild 가 있으면 같은 duration 동안 alpha 0→1 로 페이드인.
    IEnumerator ZoomImage(RectTransform rt, Image fadeChild, float from, float to, float duration)
    {
        if (rt == null) yield break;

        float dur = Mathf.Max(0.01f, duration);
        float t = 0f;
        rt.localScale = new Vector3(from, from, 1f);
        if (fadeChild != null) SetAlpha(fadeChild, 0f);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dur);
            float s = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, p));
            rt.localScale = new Vector3(s, s, 1f);
            if (fadeChild != null) SetAlpha(fadeChild, p); // alpha 0→1 (선형)
            yield return null;
        }
        rt.localScale = new Vector3(to, to, 1f);
        if (fadeChild != null) SetAlpha(fadeChild, 1f);
        _zoomCo = null;
    }

    static void SetAlpha(Image img, float a)
    {
        if (img == null) return;
        var c = img.color;
        c.a = a;
        img.color = c;
    }

    // 이미지를 "좌상단 정렬 → 우하단 정렬"로 이동(이미지가 부모보다 큰 만큼 보이는 영역이 대각 이동).
    // 앵커/피벗에 무관하게 동작 — 현재 RectTransform 설정 기준으로 시작/끝 anchoredPosition 을 계산한다.
    //   시작: 이미지 좌상단 코너 == 부모 좌상단 코너 / 끝: 이미지 우하단 코너 == 부모 우하단 코너.
    IEnumerator PanImage(RectTransform rt, float duration)
    {
        if (rt == null) yield break;
        var parent = rt.parent as RectTransform;
        if (parent == null) yield break;

        float PW = parent.rect.width,  PH = parent.rect.height;
        float IW = rt.rect.width,      IH = rt.rect.height;

        Vector2 a  = (rt.anchorMin + rt.anchorMax) * 0.5f; // 앵커(점) 위치
        Vector2 pv = rt.pivot;
        Vector2 anchorPos = new Vector2((a.x - 0.5f) * PW, (a.y - 0.5f) * PH); // 부모 중심 원점 기준 앵커 좌표

        // pivot 이 놓여야 할 위치(부모 로컬) → anchoredPosition = pivotPos - anchorPos
        Vector2 pivotStart = new Vector2(-PW * 0.5f + pv.x * IW,  PH * 0.5f - (1f - pv.y) * IH); // 좌상 정렬
        Vector2 pivotEnd   = new Vector2( PW * 0.5f - (1f - pv.x) * IW, -PH * 0.5f + pv.y * IH); // 우하 정렬

        Vector2 start = pivotStart - anchorPos;
        Vector2 end   = pivotEnd   - anchorPos;
        rt.anchoredPosition = start;

        float dur = Mathf.Max(0.01f, duration);
        float t = 0f;
        while (t < dur)
        {
            if (_skipPan) break; // 클릭 시 즉시 완료
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            rt.anchoredPosition = Vector2.Lerp(start, end, k);
            yield return null;
        }
        rt.anchoredPosition = end;
        _panCo = null;
    }

    void StopEffects()
    {
        if (_panCo != null)  { StopCoroutine(_panCo);  _panCo = null; }
        if (_zoomCo != null) { StopCoroutine(_zoomCo); _zoomCo = null; }
        if (_autoCo != null) { StopCoroutine(_autoCo); _autoCo = null; } // 수동 진행/종료 시 자동 넘김 취소
    }

    // ── 대사 타이핑 ───────────────────────────
    void StartTyping(string line)
    {
        StopTyping();
        _currentLine = line;

        bool has = !string.IsNullOrEmpty(line);
        if (dialogueBox != null) dialogueBox.SetActive(has);
        if (dialogueText == null) return;

        if (!has) { dialogueText.text = ""; _typing = false; return; }
        _typeCo = StartCoroutine(TypeLine(line));
    }

    IEnumerator TypeLine(string line)
    {
        _typing = true;
        dialogueText.text = "";
        var wait = new WaitForSecondsRealtime(Mathf.Max(0.001f, charInterval));
        for (int i = 0; i < line.Length; i++)
        {
            dialogueText.text = line.Substring(0, i + 1);
            yield return wait;
        }
        dialogueText.text = line;
        _typing = false;
        _typeCo = null;
    }

    // 타이핑 중 클릭 시 즉시 전체 표시
    void CompleteTyping()
    {
        StopTyping();
        if (dialogueText != null) dialogueText.text = _currentLine ?? "";
        _typing = false;
    }

    void StopTyping()
    {
        if (_typeCo != null) { StopCoroutine(_typeCo); _typeCo = null; }
        _typing = false;
    }

    // 대사 즉시 제거(타이핑 중단 + 텍스트 비움 + 대사창 숨김)
    void ClearDialogue()
    {
        StopTyping();
        _currentLine = null;
        if (dialogueText != null) dialogueText.text = "";
        if (dialogueBox != null) dialogueBox.SetActive(false);
    }

    static CanvasGroup EnsureCanvasGroup(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }

    void HideAllImagesImmediate()
    {
        if (steps != null)
            foreach (var s in steps)
                if (s != null && s.image != null) s.image.SetActive(false);
        _visible.Clear();
    }

    void Finish()
    {
        StopEffects();
        ClearDialogue();
        _panning = false;
        _skipPan = false;
        _playing = false;
        _busy = false;
        HideAllImagesImmediate();
        if (skipButton != null) skipButton.gameObject.SetActive(false);
        //if (root != null) root.SetActive(false);
        var cb = _onComplete;
        _onComplete = null;
        cb?.Invoke();
    }

    static bool PressedThisFrame()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;
        return false;
    }
}
