using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [Header("Total Score")]
    public GameObject totalScoreObject;       // 점수 패널 오브젝트 (게임명 + 평점)
    public TextMeshProUGUI nameText;          // "게임명: {게임명}"
    public TextMeshProUGUI totalScoreText;    // "유저 평점: {점수}"

    [Header("Stamp")]
    public GameObject stampImage;             // 평점 뒤 "쾅" 찍히는 도장
    public float stampDelay = 0.3f;           // 평점 출력 후 도장까지 대기
    public float stampPunchDuration = 0.12f;  // 도장 축소 연출 시간
    public float stampStartScale = 2.5f;      // 도장 시작 배율 (크게 → 1배)

    [Header("Settings")]
    public float criticRevealDelay = 1.5f; // 평론가 등장 간격
    public Button confirmButton;           // 확인 버튼

    public int LastCriticTotal { get; private set; }

    private System.Action _onComplete;

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
            "괜찮은 부분도 있지만 아쉬운 부분도 많습니다.",
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
            "올해 주목할 만한 작품 중 하나입니다.",
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
        _onComplete = onComplete;

        // 점수 패널은 계속 활성 — 텍스트만 라벨 상태로 비워둔다
        if (totalScoreObject != null) totalScoreObject.SetActive(true);
        if (nameText != null)       nameText.text       = "게임명: ";
        if (totalScoreText != null) totalScoreText.text = "";

        // 도장 숨김 (평점 출력 후 찍힘)
        if (stampImage != null) stampImage.SetActive(false);

        // 슬롯은 전부 비활성화 → 순차 활성화 준비
        for (int i = 0; i < criticSlots.Length; i++)
            if (criticSlots[i] != null) criticSlots[i].SetActive(false);

        GameTimeManager.Instance?.StopTime();
        ModalGate.I.Register(this);
        reviewPanel.SetActive(true);

        StartCoroutine(RevealCritics(rawScore));
    }

    IEnumerator RevealCritics(float rawScore)
    {
        int variation = UnityEngine.Random.Range(-5, 6); // Random(-5 ~ +5)
        int score = Mathf.Clamp(CalcCriticScore(rawScore) + variation, 0, 100);
        LastCriticTotal = score;

        string gameName = DevelopmentResultUI.Instance != null
            ? DevelopmentResultUI.Instance.LastProjectName : "";

        yield return new WaitForSeconds(criticRevealDelay);

        // 게임명 출력
        if (nameText != null) nameText.text = $"게임명: {gameName}";
        yield return new WaitForSeconds(0.5f);

        // 슬롯 순서대로 활성화 (이름/코멘트만 — 개별 점수는 표시 안 함, 총점만 마지막에 한 번)
        for (int i = 0; i < criticSlots.Length; i++)
        {
            if (i < criticNameTexts.Length && criticNameTexts[i] != null)
                criticNameTexts[i].text = CriticNames[i % CriticNames.Length];
            if (i < criticScoreTexts.Length && criticScoreTexts[i] != null)
                criticScoreTexts[i].text = "";
            if (i < criticCommentTexts.Length && criticCommentTexts[i] != null)
                criticCommentTexts[i].text = GetComment(score, i);

            if (criticSlots[i] != null) criticSlots[i].SetActive(true);
            yield return new WaitForSeconds(0.5f);
        }

        // 슬롯이 모두 등장한 뒤 — 총점 + 도장이 동시에 "꽝"
        yield return new WaitForSeconds(stampDelay);
        if (totalScoreText != null) totalScoreText.text = $"{score}";
        yield return StartCoroutine(ScoreAndStampPunch());
    }

    // 총점 텍스트 + 도장 이미지가 동시에 확대 상태에서 축소되며 "쾅" 박히는 연출.
    IEnumerator ScoreAndStampPunch()
    {
        Transform stampT = stampImage    != null ? stampImage.transform    : null;
        Transform scoreT = totalScoreText != null ? totalScoreText.transform : null;
        if (stampT == null && scoreT == null) yield break;

        Vector3 from = Vector3.one * stampStartScale;
        Vector3 to   = Vector3.one;

        if (stampT != null) { stampT.localScale = from; stampImage.SetActive(true); }
        if (scoreT != null) scoreT.localScale = from;

        float dur = Mathf.Max(0.01f, stampPunchDuration);
        float el = 0f;
        while (el < dur)
        {
            el += Time.deltaTime;
            float k = Mathf.Clamp01(el / dur);
            // ease-out — 빠르게 줄어들며 "쾅" 찍히는 느낌
            float eased = 1f - (1f - k) * (1f - k);
            if (stampT != null) stampT.localScale = Vector3.LerpUnclamped(from, to, eased);
            if (scoreT != null) scoreT.localScale = Vector3.LerpUnclamped(from, to, eased);
            yield return null;
        }
        if (stampT != null) stampT.localScale = to;
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
        reviewPanel.SetActive(false);
        GameTimeManager.Instance?.StartTime();
        ModalGate.I.Unregister(this);
        _onComplete?.Invoke();
    }
}