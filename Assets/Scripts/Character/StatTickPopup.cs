using System.Collections;
using TMPro;
using UnityEngine;
using DG.Tweening;

// 상시 개발틱 팝업: [statIcon] +N
// - 위로 안 떠오르고 제자리에서 카운트업 (총 totalCountDuration 초)
// - 잭팟이면 jackpotParticle 재생
// - 게임 시간 정지 중이면 일시정지
// - 카운트 종료 시 onFinish 콜백 (큐잉 매니저가 다음 팝업 띄움)
// - ActiveCount는 외부(OfficeCharacter)에서 관리 — 큐 대기까지 포함
public class StatTickPopup : MonoBehaviour
{
    public static int ActiveCount;   // queued + showing 합계 (OfficeCharacter에서 ++/--)

    [Header("자식 컴포넌트")]
    public SpriteRenderer  statIcon;
    public TextMeshPro     numberLabel;
    public ParticleSystem  jackpotParticle;

    [Header("타이밍")]
    public float totalCountDuration = 2f;   // (구버전 카운트업용 — 현재 미사용)
    public float holdAfter          = 0.3f; // (구버전)
    public float blankDuration      = 1f;   // 꽝 제자리 표시 시간

    [Header("등장 연출 (정한 위치보다 위로 튀어올랐다가 스프링으로 복귀)")]
    public float enterOvershootHeight = 0.6f; // 정한 위치보다 위로 튀어오르는 높이(월드 단위)
    public float enterDuration        = 0.45f; // 튀어올랐다가 정한 위치로 안착하기까지 걸리는 시간
    public int   enterVibrato         = 6;    // 안착까지 되튕기는 진동 횟수
    [Range(0f, 1f)] public float enterElasticity = 0.6f; // 되튕김 탄성(0=감쇠만, 1=강하게 튕김)

    [Header("카운트업")]
    public float countUpDuration = 1f;   // 0 → target 까지 고르게 올라가는 시간

    [Header("흡입 연출")]
    public float holdBeforeSuck = 0.3f;  // 카운트업 종료 후 흡입 시작까지 유지 시간
    public float suckDuration   = 0.4f;  // 타겟으로 빨려드는 시간
    public float nudgeDistance  = 0.5f;  // 흡입 직전 우측으로 살짝 빼는 거리(월드 단위)
    public float nudgeDuration  = 0.15f; // 우측 이동 시간

    private System.Action _onFinish;
    private string _statKey;
    private float _revealAmount;   // 흡입 완료 시 패널에 공개할 개발값 (>0이면 Finish/OnDisable에서 1회 공개)
    private Vector3 _baseScale = Vector3.one;

    void Awake() => _baseScale = transform.localScale;

    public void Show(Sprite stat, string statKey, int target, Color color, bool playParticle, System.Action onFinish)
    {
        _statKey = statKey;
        _revealAmount = Mathf.Max(0, target); // 흡입 끝나야 패널 표시값 공개 (즉시 X)
        transform.localScale = _baseScale; // 이전 흡입으로 줄어든 스케일 복원

        if (statIcon != null)
        {
            statIcon.sprite  = stat;
            statIcon.enabled = stat != null;
        }
        if (numberLabel != null)
        {
            numberLabel.color   = Color.white;   // 숫자는 항상 흰색
            numberLabel.text    = "";
            numberLabel.enabled = target > 0;
        }
        if (jackpotParticle != null)
        {
            jackpotParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (playParticle) jackpotParticle.Play();
        }

        _onFinish = onFinish;

        // 등장 순간 정한 위치(=현재 transform.position, 풀에서 스폰 시 이미 세팅됨) 위로 튀어올랐다가
        // 스프링으로 되튕기며 그 위치로 안착 — 카운트업(NumberLabel)과 동시에 진행되어 "밀리는" 느낌.
        transform.DOKill();
        transform.DOPunchPosition(Vector3.up * enterOvershootHeight, enterDuration, enterVibrato, enterElasticity).SetUpdate(true);

        StopAllCoroutines();
        StartCoroutine(Animate(target));
    }

    IEnumerator Animate(int target)
    {
        // ── 신규 연출: 0→target 고르게 카운트업 → holdBeforeSuck 유지 → 타겟 스탯 텍스트로 가속 흡입 → 소멸 ──
        if (target <= 0)
        {
            // 꽝(blank) — 숫자 없이 제자리 소멸 (빨려들어가지 않음)
            yield return WaitPaused(blankDuration);
            Finish();
            yield break;
        }

        if (numberLabel != null) numberLabel.text = "+0";
        yield return CountUp(target);              // 0 → target 고르게 카운트업
        yield return WaitPaused(holdBeforeSuck);    // 카운트업 종료 후 유지
        yield return NudgeRight();                  // 흡입 직전 살짝 우측으로(준비 동작)
        yield return SuckIntoTarget();              // 타겟으로 가속 흡입
        Finish();

        /* ── 구버전: 제자리 카운트업 (보존용 주석) ──
        if (target <= 0) { yield return WaitPaused(blankDuration); Finish(); yield break; }
        if (numberLabel != null) numberLabel.text = "+1";
        if (target == 1) { yield return WaitPaused(totalCountDuration); }
        else
        {
            float interval = totalCountDuration / (target - 1);
            for (int i = 2; i <= target; i++)
            {
                yield return WaitPaused(interval);
                if (numberLabel != null) numberLabel.text = $"+{i}";
            }
            yield return WaitPaused(holdAfter);
        }
        Finish();
        */
    }

    // 0 → target 까지 countUpDuration 동안 고르게(선형) 카운트업.
    IEnumerator CountUp(int target)
    {
        float elapsed = 0f;
        float dur = Mathf.Max(0.01f, countUpDuration);
        int shown = 0;
        while (elapsed < dur)
        {
            if (GameTimeManager.Instance == null || GameTimeManager.Instance.IsRunning)
                elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            int value = Mathf.RoundToInt(Mathf.Lerp(0, target, t));
            if (value != shown)
            {
                shown = value;
                if (numberLabel != null) numberLabel.text = $"+{shown}";
            }
            yield return null;
        }
        if (numberLabel != null) numberLabel.text = $"+{target}";
    }

    // 흡입 직전 우측으로 살짝 빠지는 준비 동작(anticipation). ease-out 으로 부드럽게 멈춤.
    IEnumerator NudgeRight()
    {
        Vector3 from = transform.position;
        Vector3 to   = from + Vector3.right * nudgeDistance;
        float elapsed = 0f;
        float dur = Mathf.Max(0.01f, nudgeDuration);
        while (elapsed < dur)
        {
            if (GameTimeManager.Instance == null || GameTimeManager.Instance.IsRunning)
                elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float ease = 1f - (1f - t) * (1f - t); // ease-out
            transform.position = Vector3.Lerp(from, to, ease);
            yield return null;
        }
    }

    // 역할 스탯 텍스트(DevelopmentPanelUI) 위치로 가속(ease-in) 이동 + 축소 → 빨려드는 연출.
    // 타겟을 못 찾으면(블록/창의성 등 매핑 없음) 그냥 종료해 제자리 소멸.
    IEnumerator SuckIntoTarget()
    {
        RectTransform targetRect = DevelopmentPanelUI.Instance != null
            ? DevelopmentPanelUI.Instance.GetStatTextRect(_statKey) : null;
        Camera cam = Camera.main;
        if (targetRect == null || cam == null) yield break;

        Canvas canvas = targetRect.GetComponentInParent<Canvas>();
        Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera : null;

        Vector3 start = transform.position;
        float zDist = Mathf.Abs(cam.transform.position.z - start.z);

        float elapsed = 0f;
        float dur = Mathf.Max(0.01f, suckDuration);
        while (elapsed < dur)
        {
            if (GameTimeManager.Instance == null || GameTimeManager.Instance.IsRunning)
                elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float ease = t * t * t; // 가속(3차 ease-in) — 처음 천천히, 끝에서 급격히 빨라짐

            // 타겟 UI 위치를 매 프레임 월드 좌표로 변환 (카메라 줌/팬 추종)
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCam, targetRect.position);
            Vector3 worldTarget = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, zDist));
            worldTarget.z = start.z;

            // 가속(ease-in)만 적용한 직선 이동 — 포물선 호 없이 타겟으로 곧장 빨려듦
            Vector3 pos = Vector3.LerpUnclamped(start, worldTarget, ease);
            transform.position = pos;
            yield return null;
        }
    }

    void Finish()
    {
        if (jackpotParticle != null)
            jackpotParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        RevealToPanel();   // 흡입 완료 — 이제 패널 표시값이 올라간다

        var cb = _onFinish;
        _onFinish = null;
        StatTickPopupPool.Instance?.Return(this);
        cb?.Invoke();
    }

    // 패널 표시값 공개 — 1회만(중복 방지로 _revealAmount 소진).
    void RevealToPanel()
    {
        if (_revealAmount > 0f)
        {
            DevelopmentPanelUI.Instance?.RevealValues(_statKey, _revealAmount);
            _revealAmount = 0f;
        }
    }

    void OnDisable()
    {
        // 강제 비활성화(개발 시작 시 ClearAllPopups 등) 시에도 패널값 공개 누락 방지
        RevealToPanel();

        // 강제 비활성화 시 콜백 누락 방지
        if (_onFinish != null)
        {
            var cb = _onFinish;
            _onFinish = null;
            cb?.Invoke();
        }
    }

    IEnumerator WaitPaused(float seconds)
    {
        float remain = seconds;
        while (remain > 0f)
        {
            if (GameTimeManager.Instance == null || GameTimeManager.Instance.IsRunning)
                remain -= Time.deltaTime;
            yield return null;
        }
    }
}
