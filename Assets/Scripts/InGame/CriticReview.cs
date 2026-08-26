using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class CriticReviewUI : MonoBehaviour
{
    public static CriticReviewUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject reviewPanel;

    [Header("Critic Slots (3개)")]
    public GameObject[] criticSlots;          // 슬롯 오브젝트 — 개수는 인스펙터 배열 크기를 따름
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
    private bool _pendingGoodScreen;
    private int _pendingTeamStressScore;
    private float _pendingCasePenaltyPct;
    private string _pendingTopPart;
    private string _pendingBottomPart;
    private string[] _pendingComments;
    private string[] _pendingNames;

    // 리뷰마다 이 풀에서 슬롯 개수만큼 중복 없이 랜덤으로 뽑아 배정한다(BuildComments 참고).
    private static readonly string[] CriticNames =
    {
        "망겜감별사", "무무", "승리요정", "오늘은다름", "데스티니", "월요병", "전국노예자랑",
        "빛의전사", "마스터", "버그헌터99", "매일접속중", "코코", "미묘한맛", "카페인중독자",
        "김이병", "윈도우메이커", "재화충", "뜻밖의행운", "고민중33", "밥먹으면서함",
        "폰터짐", "운빨망겜러", "겜잘알", "언끝", "익명07", "반반유저2",
        "10년째초보", "지금2순간", "ㅇㅇ", "동동동춘", "마침표", "만렙찍고옴",
    };

    // 평론가 점수(0~100, 랜덤 변동 포함 표시값) → 단계(1~5)
    static int GetCriticStage(int score)
    {
        if (score <= 25) return 1;
        if (score <= 40) return 2;
        if (score <= 55) return 3;
        if (score <= 70) return 4;
        return 5;
    }

    // [단계-1][0=안좋은 화면,1=좋은 화면] → 5줄. 화면유형은 점수가 아니라 팀 스트레스 점수(90/95/99 임계선
    // 달성도, GetTeamStressScore)로 갈린다 — 점수는 높은데 스트레스 성적이 나쁘면 "좋은 화면"이어도 나쁜 어투가 나올 수 있음.
    static readonly string[][][] StagePool =
    {
        new[] // 1단계
        {
            new[] { "10분 하고 바로 환불함. 내 돈 내놔", "억빠들 전멸ㅋㅋㅋ 퀄리티 실화냐", "겉만 번지르르한 10년 전 게임", "내 시간 돌려내라 진짜 돈 줘도 안 함", "돈 받고 팔 생각을 했다는 게 레전드" },
            new[] { "아이디어는 신선한데 완성도가 처참", "개발자 한숨 소리가 여기까지 들린다…", "내 취향은 맞는데 똥겜이긴 하다", "노력한 것 같긴한데 대학교 과제 느낌", "한 10년 더 있으면 재밌어질듯" },
        },
        new[] // 2단계
        {
            new[] { "초반은 재밌는데 점점 퀄리티 이상해짐", "트레일러만 재밌고 게임은 노잼", "이 체급으로 이 정도밖에 못 뽑아내나?", "기대 겁나 했는데 뚜껑 까보니 아쉽네", "전작보다 퇴보한듯..." },
            new[] { "패치 하면 할만해질 듯... 지금은 글쎄", "가격 보면 납득 가는데 남한테 추천은..", "작은 회사라 그런가 볼륨이 아쉽네", "감성은 좋은데 조작감이 다 깎아먹음", "컨텐츠 더 추가되면 할만해질듯" },
        },
        new[] // 3단계
        {
            new[] { "재미없는 건 아닌데 뭔가 2% 빠진 느낌", "흔한 양산형 스타일.", "쫌 더 재밌게 어떻게 못하나?", "초반만 꿀잼이고 뒤로 갈수록…", "하면 할 수록 재미 없어진다" },
            new[] { "세일할 때 사서 가볍게 할 만함", "기대치 낮추고 하면 은근히 시간 잘 감", "정가엔 아깝고 할인 때 찍먹 추천", "멍때리면서 하기엔 나쁘지 않음", "은근히 계속 빠져드는 매력이 있음" },
        },
        new[] // 4단계
        {
            new[] { "게임은 갓겜인데 편의성이 아쉽네", "다 좋은데 엔딩 마무리가 허무하다", "퀄리티는 탄탄한데 후반 밸런스가..", "명작 될 뻔했는데 디테일이 아쉽다", "재미는 있는데 노가다가 너무 심하다" },
            new[] { "이걸 이렇게 해내네? 정성이 눈에 보임", "돈값은 충분히 함. 몰입해서 달렸다", "디테일에 신경 쓴 게 확실히 느껴짐", "다음 신작이 기대되는 개발사", "연출이랑 분위기 미쳤다" },
        },
        new[] // 5단계
        {
            new[] { "버그 보이긴 하는데 용서되는 완성도", "분량이 적은 것 빼고는 모든 게 완벽함", "버그만 잡으면 인생 최고의 게임 될 듯", "진짜 재밌긴한데 난이도가 무슨..", "초반 진입장벽 높긴한데 적응되면 갓겜" },
            new[] { "개발자님 빨리 후속작 내시죠", "이 퀄리티에 이 가격이면 무료 수준", "대기업이 된게 눈에 보이는 게임", "진짜 가슴이 웅장해진다.", "안하면 인생 손해봅니다" },
        },
    };

    // 조건부 멘트 풀 — 조건이 여럿 겹치면 전부 후보에 합쳐서 그중 1개만 랜덤으로 뽑는다.
    private static readonly string[] PopularityLowComments =
    {
        "인기 없는 장르의 한계…",
        "인기 많은 장르면 1등했을듯 아쉽다",
    };
    private static readonly string[] FatigueHighComments =
    {
        "이 장르만 몇 번째냐... 이제 좀 질린다",
        "또 이 장르야? 다음엔 제발 다른 것 좀",
    };
    private static readonly string[] MasteryMasterComments =
    {
        "이 장르의 신이 만든 게임. 뭔가 다르다",
    };

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        reviewPanel.SetActive(false);
    }

    // teamStressScore: DevelopmentManager.GetTeamStressScore() — 파트 3곳 팀장점수 90/95/99 임계선 브라켓 합(>=5면 좋은 화면).
    // casePenaltyPct/topPart/bottomPart: 쏠린 케이스 감점(0~0.4)과 1등/꼴등 파트명 — 20% 이상일 때만 조건 멘트 후보에 들어간다.
    public void Show(float rawScore, int teamStressScore, float casePenaltyPct, string topPart, string bottomPart, System.Action onComplete)
    {
        int variation = UnityEngine.Random.Range(-5, 6); // Random(-5 ~ +5)
        int score = Mathf.Clamp(CalcCriticScore(rawScore) + variation, 0, 100);
        ShowInternal(score, teamStressScore, casePenaltyPct, topPart, bottomPart, onComplete);
    }

    // 디버그/테스트 전용 — CalcCriticScore 공식+랜덤 변동을 거치지 않고 점수를 직접 지정.
    // (예: 단계/화면유형 분기를 정확한 경계값으로 확인하고 싶을 때. 조건부 멘트 인자는 생략하면 빈 상태로 테스트)
    public void ShowWithScore(int score, System.Action onComplete = null,
        int teamStressScore = 0, float casePenaltyPct = 0f, string topPart = "", string bottomPart = "")
    {
        ShowInternal(Mathf.Clamp(score, 0, 100), teamStressScore, casePenaltyPct, topPart, bottomPart, onComplete);
    }

    void ShowInternal(int score, int teamStressScore, float casePenaltyPct, string topPart, string bottomPart, System.Action onComplete)
    {
        _onComplete = onComplete;
        _revealDone = false;
        _pendingTeamStressScore = teamStressScore;
        _pendingGoodScreen = teamStressScore >= 5;
        _pendingCasePenaltyPct = casePenaltyPct;
        _pendingTopPart = topPart;
        _pendingBottomPart = bottomPart;

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
        BuildComments();

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
            SetSlotText(i);
            if (criticSlots[i] != null) criticSlots[i].SetActive(true);
            yield return new WaitForSeconds(0.5f);
        }

        // 슬롯이 모두 등장한 뒤 — 총점 + 도장 + 점수 반응 이미지가 동시에 "꽝"
        yield return new WaitForSeconds(stampDelay);
        if (totalScoreText != null) totalScoreText.text = $"{_pendingScore}";
        PlayReactionReveal(_pendingGoodScreen);
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
    void SetReactionImageInstant(bool goodScreen)
    {
        EnsureReactionRestPosCached();
        bool low = !goodScreen;
        SnapReactionImage(lowImage,  _lowRestPos,  low);
        SnapReactionImage(highImage, _highRestPos, !low);
    }

    // 화면유형(팀 스트레스 점수 >=5 면 좋은 화면)=lowImage/highImage — 반대쪽은 확실히 끄고, 대상만 등장 연출과 함께 활성화.
    // 슬픔(low): 위쪽에서 서서히 가라앉듯 페이드인. 기쁨(high): 작게 시작해 OutBack으로 확 튀어나옴.
    void PlayReactionReveal(bool goodScreen)
    {
        EnsureReactionRestPosCached();
        bool low = !goodScreen;
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

    void SetSlotText(int i)
    {
        if (i < criticNameTexts.Length && criticNameTexts[i] != null)
            criticNameTexts[i].text = (_pendingNames != null && i < _pendingNames.Length) ? _pendingNames[i] : "";
        if (i < criticScoreTexts.Length && criticScoreTexts[i] != null)
            criticScoreTexts[i].text = "";
        if (i < criticCommentTexts.Length && criticCommentTexts[i] != null)
            criticCommentTexts[i].text = (_pendingComments != null && i < _pendingComments.Length) ? _pendingComments[i] : "";
    }

    // 순차 등장 도중 클릭 시 — 코루틴(등장 대기 + 도장 펀치 애니메이션 전부) 중단하고 최종 상태로 즉시 스냅.
    void SkipReveal()
    {
        StopAllCoroutines();

        if (nameText != null) nameText.text = $"게임명: {_pendingGameName}";
        for (int i = 0; i < criticSlots.Length; i++)
        {
            SetSlotText(i);
            if (criticSlots[i] != null) criticSlots[i].SetActive(true);
        }
        SetReactionImageInstant(_pendingGoodScreen);
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

    // 이번 리뷰에 쓸 슬롯별 대사를 미리 확정 — 조건이 우선순위: 충족된 조건 개수만큼(슬롯 수 한도 내에서)
    // 슬롯을 조건 멘트로 채우고(조건이 슬롯보다 많으면 그중 랜덤으로 슬롯 수만큼만 채택), 남는 슬롯만
    // 같은 단계-화면유형 표(5줄)에서 중복 없이 랜덤으로 채운다.
    void BuildComments()
    {
        int stage = GetCriticStage(_pendingScore);
        var tablePool = new List<string>(StagePool[stage - 1][_pendingGoodScreen ? 1 : 0]);
        Shuffle(tablePool);

        var conditionPools = GetSatisfiedConditionPools();
        Shuffle(conditionPools);

        int slotCount = criticSlots.Length;
        _pendingComments = new string[slotCount];

        int conditionSlots = Mathf.Min(conditionPools.Count, slotCount);
        int idx = 0;
        for (; idx < conditionSlots; idx++)
        {
            var pool = conditionPools[idx];
            _pendingComments[idx] = pool[UnityEngine.Random.Range(0, pool.Length)];
        }

        for (int p = 0; idx < slotCount; idx++, p++)
            _pendingComments[idx] = tablePool[p % tablePool.Count];

        BuildNames(slotCount);
    }

    // 슬롯 개수만큼 CriticNames 풀에서 중복 없이 랜덤 배정.
    void BuildNames(int slotCount)
    {
        var namePool = new List<string>(CriticNames);
        Shuffle(namePool);

        _pendingNames = new string[slotCount];
        for (int i = 0; i < slotCount; i++)
            _pendingNames[i] = namePool[i % namePool.Count];
    }

    // 마케팅비 조건은 이 시점(평론가 리뷰 시점)엔 마케팅비가 아직 정해지지 않아 제외한다(유저 확정 사양).
    // 인기도/피로도/숙련도/쏠린 케이스 — 충족된 조건마다 "카테고리 단위"로 풀 하나씩 모은다(카테고리당
    // 슬롯 1개씩 배정되고, 그 카테고리가 뽑힌 뒤에야 카테고리 내 여러 줄 중 랜덤으로 1줄을 쓴다).
    List<string[]> GetSatisfiedConditionPools()
    {
        var pools = new List<string[]>();

        if (ProjectSetupUI.SelectedGenrePopularity == 1)
            pools.Add(PopularityLowComments);

        int fatigue = ProjectSetupUI.SelectedGenreFatigue;
        if (fatigue == 2 || fatigue == 3)
            pools.Add(FatigueHighComments);

        if (MasteryManager.Instance != null
            && MasteryManager.Instance.GetTier(ProjectSetupUI.SelectedGenre) == MasteryTier.Master)
            pools.Add(MasteryMasterComments);

        if (_pendingCasePenaltyPct >= 0.20f
            && !string.IsNullOrEmpty(_pendingTopPart) && !string.IsNullOrEmpty(_pendingBottomPart))
        {
            pools.Add(new[]
            {
                $"{_pendingTopPart} 혼자 일하고 나머지는 딴짓한듯",
                $"{_pendingTopPart}는 예술인데 {_pendingBottomPart} 때문에 망함",
            });
        }

        return pools;
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
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