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
        "김게임 (게임월드)",
        "이리뷰 (게임인사이드)",
        "박평점 (겜스코어)"
    };

    // 점수별 멘트 풀
    private static readonly string[][] Comments =
    {
        // 1점 (20 미만)
        new[] {
            "이게 게임인가요?",
            "환불 요청합니다.",
            "개발을 다시 배우세요.",
            "역대급 실망작입니다."
        },
        // 2점 (20~25)
        new[] {
            "출시를 서두른 것 같습니다.",
            "기초가 너무 부족합니다.",
            "아이디어만 있고 창의성이 없네요.",
            "더 많은 준비가 필요했습니다."
        },
        // 3점 (25~30)
        new[] {
            "가능성은 보이나 창의성이 아쉽습니다.",
            "방향성은 있지만 디테일이 부족합니다.",
            "기본기가 흔들립니다.",
            "좀 더 다듬었어야 했습니다."
        },
        // 4점 (30~35)
        new[] {
            "평균 이하의 작품입니다.",
            "몇 가지 좋은 요소가 있지만 전반적으로 부족합니다.",
            "할인 때 구매를 고려해보세요.",
            "아직 갈 길이 멉니다."
        },
        // 5점 (35~40)
        new[] {
            "그저 그런 게임입니다.",
            "특별함이 없는 평범한 작품입니다.",
            "기대에 못 미치는 결과물입니다.",
            "무난하지만 기억에 남지 않을 것 같습니다."
        },
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
        if (totalScoreText != null) totalScoreText.text = "유저 평점: ";

        // 도장 숨김 (평점 출력 후 찍힘)
        if (stampImage != null) stampImage.SetActive(false);

        // 슬롯은 전부 비활성화 → 순차 활성화 준비
        for (int i = 0; i < criticSlots.Length; i++)
            if (criticSlots[i] != null) criticSlots[i].SetActive(false);

        GameTimeManager.Instance?.StopTime();
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

        // 유저 평점 출력
        if (totalScoreText != null) totalScoreText.text = $"유저 평점: {score}점";

        // 평점 출력 후 도장 "쾅"
        yield return new WaitForSeconds(stampDelay);
        yield return StartCoroutine(StampPunch());

        // 슬롯 순서대로 활성화 (각 슬롯에 이름/점수/코멘트 분배)
        for (int i = 0; i < criticSlots.Length; i++)
        {
            if (i < criticNameTexts.Length && criticNameTexts[i] != null)
                criticNameTexts[i].text = CriticNames[i % CriticNames.Length];
            if (i < criticScoreTexts.Length && criticScoreTexts[i] != null)
                criticScoreTexts[i].text = $"{score}점";
            if (i < criticCommentTexts.Length && criticCommentTexts[i] != null)
                criticCommentTexts[i].text = GetComment(score, i);

            if (criticSlots[i] != null) criticSlots[i].SetActive(true);
            yield return new WaitForSeconds(0.5f);
        }
    }

    IEnumerator StampPunch()
    {
        if (stampImage == null) yield break;

        Transform t = stampImage.transform;
        Vector3 from = Vector3.one * stampStartScale;
        Vector3 to   = Vector3.one;

        t.localScale = from;
        stampImage.SetActive(true);

        float dur = Mathf.Max(0.01f, stampPunchDuration);
        float el = 0f;
        while (el < dur)
        {
            el += Time.deltaTime;
            float k = Mathf.Clamp01(el / dur);
            // ease-out — 빠르게 줄어들며 "쾅" 찍히는 느낌
            float eased = 1f - (1f - k) * (1f - k);
            t.localScale = Vector3.LerpUnclamped(from, to, eased);
            yield return null;
        }
        t.localScale = to;
    }

int CalcCriticScore(float x)
{
    // Y = 53.46·ln(원천 + 248) − 302.9  (변동분 Random(-5~+5)은 호출부에서 가산, 클램프도 호출부에서)
    float score = 53.46f * Mathf.Log(x + 248f) - 302.9f;
    return Mathf.RoundToInt(score);
}

    string GetComment(int score, int criticIndex)
    {
        int idx = Mathf.Clamp(score / 10, 0, Comments.Length - 1);
        var pool = Comments[idx];
        return pool[criticIndex % pool.Length];
    }

    public void OnClickConfirm()
    {
        reviewPanel.SetActive(false);
        GameTimeManager.Instance?.StartTime();
        _onComplete?.Invoke();
    }
}