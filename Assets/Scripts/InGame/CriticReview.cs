using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class CriticReviewUI : MonoBehaviour
{
    public static CriticReviewUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject reviewPanel;

    [Header("Critic Slots (4개)")]
    public GameObject[] criticSlots;          // 4개 슬롯 오브젝트
    public TextMeshProUGUI[] criticNameTexts; // 평론가 이름
    public TextMeshProUGUI[] criticScoreTexts;// 점수
    public TextMeshProUGUI[] criticCommentTexts; // 한줄평

    [Header("Score Reaction (총점+도장 \"쾅\" 찍히는 순간 점수에 따라 택1 등장, ScoreAndStampPunch와 동시)")]
    public GameObject lowImage;  // 50점 미만 — 위에서 가라앉듯 페이드인
    public GameObject highImage; // 50점 이상 — 확 튀어나오듯 스케일 펀치
    [Tooltip("lowImage 등장 시 페이드인 시간(초) — 위쪽 오프셋에서 제자리로 서서히 가라앉음")]
    public float lowRevealDuration = 0.45f;
    [Tooltip("lowImage가 시작하는 위쪽 오프셋(px) — 이 값에서 제자리(anchoredPosition)로 하강")]
    public float lowRevealStartOffsetY = 15f;
    [Tooltip("highImage 등장 스케일 펀치 시간(초)")]
    public float highRevealDuration = 0.35f;
    [Tooltip("highImage 펀치 오버슈트(OutBack 진폭) — 클수록 더 크게 튕기고 줄어듦")]
    public float highRevealOvershoot = 1.3f;
    [Tooltip("lowImage/highImage 가 등장을 마친 뒤 유지하는 알파값 (0~255 표기)")]
    public float reactionRestAlpha255 = 65f;
    float ReactionRestAlpha => Mathf.Clamp01(reactionRestAlpha255 / 255f);

    Vector2 _lowRestPos, _highRestPos;
    bool _reactionRestPosCached;

    [Header("Total Score")]
    public GameObject totalScoreObject;       // 점수 패널 오브젝트 (게임명 + 평점)
    public TextMeshProUGUI nameText;          // "게임명: {게임명}"
    public TextMeshProUGUI totalScoreText;    // "유저 평점: {점수}"

    [Header("Stamp — HiringUI.PlayHireStamp와 동일 연출(스쿼시+흔들림+탄성 안착)")]
    public GameObject stampImage;             // 평점 뒤 "쾅" 찍히는 도장
    public float stampDelay = 0.3f;           // 평점 출력 후 도장까지 대기
    public float stampPunchDuration = 0.12f;  // "쾅" 임팩트(스쿼시+알파 등장) 시간 — 아주 짧고 빠르게
    public float stampStartScale = 2.5f;      // 도장 시작 배율 (크게 → 1배)
    [Tooltip("찍히는 순간 옆으로 퍼지는 스쿼시 스케일(X)")]
    public float stampSquashScaleX = 1.25f;
    [Tooltip("찍히는 순간 위아래로 눌리는 스쿼시 스케일(Y)")]
    public float stampSquashScaleY = 0.35f;
    [Tooltip("임팩트 후 튕겨나오며 최종 크기(1배)로 안착하는 시간(초)")]
    public float stampSettleDuration = 0.35f;
    [Tooltip("임팩트 순간 흔들릴 패널 — 비우면 totalScoreObject 사용")]
    public RectTransform stampShakeTarget;
    public float stampShakeStrength = 14f;    // 임팩트 순간 패널 흔들림 세기(px)
    public float stampShakeDuration = 0.3f;
    public int   stampShakeVibrato  = 14;

    [Header("Settings")]
    public float criticRevealDelay = 1.5f; // 평론가 등장 간격
    public Button confirmButton;           // 확인 버튼

    public int LastCriticTotal { get; private set; }

    private System.Action _onComplete;

    // 순차 등장 도중 클릭 시 스킵용 — 다이얼로그(DialogUI)와 동일 패턴: 등장 중 클릭 → 즉시 전부 표시,
    // 다 나온 뒤 클릭 → 다음(OnClickConfirm)으로 진행.
    private bool _revealDone;
    private int _pendingScore;
    private string _pendingGameName;

    private static readonly string[] CriticNames =
    {
        "망겜감별사",
        "빛의전사",
        "김이병"
    };

    // 50점 미만 전용 멘트풀 (3개만)
    private static readonly string[] LowScoreComments =
    {
        "억빠들 전멸ㅋㅋㅋ 퀄리티 실화냐",
        "10분 하고 환불함. 내 돈 내놔",
        "겉만 번지르르한 10년 전 게임",
    };

    // 점수별 멘트 풀 (50점 이상만 — 50점 미만은 LowScoreComments 사용)
    private static readonly string[][] Comments =
    {
        // 6점 (40~50, 60~70)
        new[] {
            "어느 정도 즐길 수 있는 게임입니다.",
            "팬이라면 즐길 수 있을 것입니다.",
            "보통 수준의 게임입니다."
        },
        // 7점 (50~60, 70~80)
        new[] {
            "꽤 잘 만든 게임입니다.",
            "추천할 만한 작품입니다.",
            "재미있게 즐겼습니다.",
            "장르 팬이라면 필수입니다."
        },
        // 8점 (80~85)
        new[] {
            "창의성 높은 수작입니다.",
            "올해 주목할 만한 작품 중 하나입니다",
            "강력 추천합니다!",
            "개발팀의 역량이 돋보입니다."
        },
        // 9점 (85~90)
        new[] {
            "거의 완벽에 가까운 게임입니다.",
            "올해 최고의 작품 중 하나입니다.",
            "모든 면에서 탁월합니다.",
            "기억에 남을 명작입니다."
        },
        // 10점 (90~99)
        new[] {
            "완벽한 게임입니다. 만점을 드립니다!",
            "역대급 명작 탄생!",
            "이런 게임을 기다려왔습니다!",
            "게임 역사에 남을 작품입니다!"
        },
    };

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        reviewPanel.SetActive(false);
    }

    public void Show(float rawScore, System.Action onComplete)
    {
        int variation = UnityEngine.Random.Range(-5, 6); // Random(-5 ~ +5)
        int score = Mathf.Clamp(CalcCriticScore(rawScore) + variation, 0, 100);
        ShowInternal(score, onComplete);
    }

    // 디버그/테스트 전용 — CalcCriticScore 공식+랜덤 변동을 거치지 않고 점수를 직접 지정.
    // (예: 50점 미만/이상 반응 이미지·멘트풀 분기를 정확한 경계값으로 확인하고 싶을 때)
    public void ShowWithScore(int score, System.Action onComplete = null)
    {
        ShowInternal(Mathf.Clamp(score, 0, 100), onComplete);
    }

    void ShowInternal(int score, System.Action onComplete)
    {
        _onComplete = onComplete;
        _revealDone = false;

        // 점수 패널은 계속 활성 — 텍스트만 라벨 상태로 비워둔다
        if (totalScoreObject != null) totalScoreObject.SetActive(true);
        if (nameText != null)       nameText.text       = "게임명: ";
        if (totalScoreText != null) totalScoreText.text = "";

        // 도장 숨김 (평점 출력 후 찍힘)
        if (stampImage != null) stampImage.SetActive(false);

        // 슬롯은 전부 비활성화 → 순차 활성화 준비
        for (int i = 0; i < criticSlots.Length; i++)
            if (criticSlots[i] != null) criticSlots[i].SetActive(false);

        ResetReactionImages();

        LastCriticTotal = score;
        _pendingScore = score;
        _pendingGameName = DevelopmentResultUI.Instance != null
            ? DevelopmentResultUI.Instance.LastProjectName : "";

        GameTimeManager.Instance?.StopTime();
        ModalGate.I.Register(this);
        reviewPanel.SetActive(true);

        StartCoroutine(RevealCritics());
    }

    IEnumerator RevealCritics()
    {
        yield return new WaitForSeconds(criticRevealDelay);

        // 게임명 출력
        if (nameText != null) nameText.text = $"게임명: {_pendingGameName}";
        yield return new WaitForSeconds(0.5f);

        // 슬롯 순서대로 활성화 (이름/코멘트만 — 개별 점수는 표시 안 함, 총점만 마지막에 한 번)
        for (int i = 0; i < criticSlots.Length; i++)
        {
            SetSlotText(i, _pendingScore);
            if (criticSlots[i] != null) criticSlots[i].SetActive(true);
            yield return new WaitForSeconds(0.5f);
        }

        // 슬롯이 모두 등장한 뒤 — 총점 + 도장 + 점수 반응 이미지가 동시에 "꽝"
        yield return new WaitForSeconds(stampDelay);
        if (totalScoreText != null) totalScoreText.text = $"{_pendingScore}";
        PlayReactionReveal(_pendingScore);
        yield return StartCoroutine(ScoreAndStampPunch());

        _revealDone = true;
    }

    // lowImage/highImage 의 원래 anchoredPosition(디자인타임 위치)을 1회만 캐싱 — 애니메이션 시작/원복 기준값.
    void EnsureReactionRestPosCached()
    {
        if (_reactionRestPosCached) return;
        if (lowImage  != null) _lowRestPos  = ((RectTransform)lowImage.transform).anchoredPosition;
        if (highImage != null) _highRestPos = ((RectTransform)highImage.transform).anchoredPosition;
        _reactionRestPosCached = true;
    }

    // 진행 중이던 트윈을 멎고 스케일/위치/알파를 rest 상태로 되돌린 뒤 active만 지정.
    void SnapReactionImage(GameObject go, Vector2 restPos, bool active)
    {
        if (go == null) return;
        var rt = (RectTransform)go.transform;
        rt.DOKill();
        rt.localScale = Vector3.one;
        rt.anchoredPosition = restPos;
        var img = go.GetComponent<Image>();
        if (img != null)
        {
            img.DOKill();
            var c = img.color; c.a = ReactionRestAlpha; img.color = c;
        }
        go.SetActive(active);
    }

    // Show() 시작 시 — 둘 다 rest 상태로 스냅 + 비활성.
    void ResetReactionImages()
    {
        EnsureReactionRestPosCached();
        SnapReactionImage(lowImage,  _lowRestPos,  false);
        SnapReactionImage(highImage, _highRestPos, false);
    }

    // 스킵(즉시 전체 표시) 경로 — 애니메이션 없이 최종 상태로 바로 스냅.
    void SetReactionImageInstant(int score)
    {
        EnsureReactionRestPosCached();
        bool low = score < 50;
        SnapReactionImage(lowImage,  _lowRestPos,  low);
        SnapReactionImage(highImage, _highRestPos, !low);
    }

    // 50점 미만=lowImage, 이상=highImage — 반대쪽은 확실히 끄고, 대상만 등장 연출과 함께 활성화.
    // 슬픔(low): 위쪽에서 서서히 가라앉듯 페이드인. 기쁨(high): 작게 시작해 OutBack으로 확 튀어나옴.
    void PlayReactionReveal(int score)
    {
        EnsureReactionRestPosCached();
        bool low = score < 50;
        SnapReactionImage(low ? highImage : lowImage, low ? _highRestPos : _lowRestPos, false);

        GameObject target = low ? lowImage : highImage;
        if (target == null) return;
        Vector2 restPos = low ? _lowRestPos : _highRestPos;

        var rt = (RectTransform)target.transform;
        rt.DOKill();
        var img = target.GetComponent<Image>();
        img?.DOKill();

        if (low)
        {
            rt.localScale = Vector3.one;
            rt.anchoredPosition = restPos + new Vector2(0f, lowRevealStartOffsetY);
            if (img != null) { var c = img.color; c.a = 0f; img.color = c; }
            target.SetActive(true);
            rt.DOAnchorPos(restPos, lowRevealDuration).SetEase(Ease.OutSine).SetUpdate(true);
            if (img != null) img.DOFade(ReactionRestAlpha, lowRevealDuration).SetUpdate(true);
        }
        else
        {
            rt.anchoredPosition = restPos;
            rt.localScale = Vector3.one * 0.7f;
            if (img != null) { var c = img.color; c.a = 0f; img.color = c; }
            target.SetActive(true);
            rt.DOScale(1f, highRevealDuration).SetEase(Ease.OutBack, highRevealOvershoot).SetUpdate(true);
            if (img != null) img.DOFade(ReactionRestAlpha, highRevealDuration * 0.7f).SetUpdate(true);
        }
    }

    void SetSlotText(int i, int score)
    {
        if (i < criticNameTexts.Length && criticNameTexts[i] != null)
            criticNameTexts[i].text = CriticNames[i % CriticNames.Length];
        if (i < criticScoreTexts.Length && criticScoreTexts[i] != null)
            criticScoreTexts[i].text = "";
        if (i < criticCommentTexts.Length && criticCommentTexts[i] != null)
            criticCommentTexts[i].text = GetComment(score, i);
    }

    // 순차 등장 도중 클릭 시 — 코루틴(등장 대기 + 도장 펀치 애니메이션 전부) 중단하고 최종 상태로 즉시 스냅.
    void SkipReveal()
    {
        StopAllCoroutines();

        if (nameText != null) nameText.text = $"게임명: {_pendingGameName}";
        for (int i = 0; i < criticSlots.Length; i++)
        {
            SetSlotText(i, _pendingScore);
            if (criticSlots[i] != null) criticSlots[i].SetActive(true);
        }
        SetReactionImageInstant(_pendingScore);
        if (totalScoreText != null)
        {
            totalScoreText.text = $"{_pendingScore}";
            totalScoreText.transform.localScale = Vector3.one;
        }
        if (stampImage != null)
        {
            stampImage.transform.DOKill();
            stampImage.SetActive(true);
            stampImage.transform.localScale = Vector3.one;
            // 애니메이션 도중 스킵되면 알파가 0으로 남아있을 수 있어 명시적으로 복원.
            var stampImg = stampImage.GetComponent<Image>();
            if (stampImg != null)
            {
                stampImg.DOKill();
                var c = stampImg.color; c.a = 1f; stampImg.color = c;
            }
        }
        var shakeTarget = stampShakeTarget != null
            ? stampShakeTarget
            : (totalScoreObject != null ? totalScoreObject.transform as RectTransform : null);
        shakeTarget?.DOKill();

        _revealDone = true;
    }

    // 총점 텍스트는 기존처럼 단순 축소, 도장 이미지는 HiringUI.PlayHireStamp와 동일하게 "쾅" 찍히는
    // 스쿼시(가로로 퍼지고 세로로 눌림) + 알파 확 등장 + 패널 흔들림 → 튕겨나오듯 최종 스케일 안착.
    IEnumerator ScoreAndStampPunch()
    {
        Transform scoreT = totalScoreText != null ? totalScoreText.transform : null;
        RectTransform stampRt = stampImage != null ? stampImage.transform as RectTransform : null;
        if (stampRt == null && scoreT == null) yield break;

        Vector3 from = Vector3.one * stampStartScale;
        Vector3 to   = Vector3.one;

        if (scoreT != null) scoreT.localScale = from;

        Image stampImg = null;
        if (stampRt != null)
        {
            stampImage.SetActive(true);
            stampImg = stampImage.GetComponent<Image>();
            stampRt.DOKill();
            if (stampImg != null) stampImg.DOKill();
            stampRt.localScale = from;
            if (stampImg != null) { var c = stampImg.color; c.a = 0f; stampImg.color = c; }

            var shakeTarget = stampShakeTarget != null
                ? stampShakeTarget
                : (totalScoreObject != null ? totalScoreObject.transform as RectTransform : null);
            shakeTarget?.DOKill();

            float impactDur = Mathf.Max(0.01f, stampPunchDuration);
            var seq = DOTween.Sequence().SetUpdate(true).SetTarget(stampImage);
            if (stampImg != null) seq.Join(stampImg.DOFade(1f, impactDur));                                     // 찍히는 순간 알파 확 등장
            seq.Join(stampRt.DOScale(new Vector3(stampSquashScaleX, stampSquashScaleY, 1f), impactDur).SetEase(Ease.InQuad)); // 눌려 퍼지는 스쿼시
            seq.AppendCallback(() =>                                                                             // 임팩트 순간 패널 흔들림
            {
                if (shakeTarget != null)
                    shakeTarget.DOShakeAnchorPos(stampShakeDuration, stampShakeStrength, stampShakeVibrato, 90f, false, true).SetUpdate(true);
            });
            seq.Append(stampRt.DOScale(Vector3.one, stampSettleDuration).SetEase(Ease.OutElastic, 1.1f, 0.6f)); // 임팩트 후 튕겨나오며 안착
        }

        // 총점 텍스트는 기존과 동일하게 단순 ease-out 축소 (도장과 같은 임팩트 시간 동안 동시 진행)
        float dur = Mathf.Max(0.01f, stampPunchDuration);
        float el = 0f;
        while (el < dur)
        {
            el += Time.deltaTime;
            float k = Mathf.Clamp01(el / dur);
            float eased = 1f - (1f - k) * (1f - k);
            if (scoreT != null) scoreT.localScale = Vector3.LerpUnclamped(from, to, eased);
            yield return null;
        }
        if (scoreT != null) scoreT.localScale = to;
    }

// public static — MasteryManager 승급 판정용 "판정점수"(랜덤 변동 -5~+5 제외) 계산에도 재사용.
public static int CalcCriticScore(float x)
{
    // Y = 53.46·ln(원천 + 248) − 302.9  (변동분 Random(-5~+5)은 호출부에서 가산, 클램프도 호출부에서)
    float score = 53.46f * Mathf.Log(x + 248f) - 302.9f;
    return Mathf.RoundToInt(score);
}

    string GetComment(int score, int criticIndex)
    {
        if (score < 50)
            return LowScoreComments[criticIndex % LowScoreComments.Length];

        int idx = Mathf.Clamp(score / 10 - 5, 0, Comments.Length - 1);
        var pool = Comments[idx];
        return pool[criticIndex % pool.Length];
    }

    public void OnClickConfirm()
    {
        if (!_revealDone) { SkipReveal(); return; }

        reviewPanel.SetActive(false);
        GameTimeManager.Instance?.StartTime();
        ModalGate.I.Unregister(this);
        _onComplete?.Invoke();
    }
}