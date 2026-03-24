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

    [Header("Settings")]
    public float criticRevealDelay = 1.5f; // 평론가 등장 간격
    public Button confirmButton;           // 확인 버튼

    private System.Action _onComplete;

    private static readonly string[] CriticNames =
    {
        "김게임 (게임월드)",
        "이리뷰 (픽셀매거진)",
        "박크리틱 (게임타임즈)",
        "최평론 (인디씬)"
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
            "아이디어만 있고 완성도가 없네요.",
            "더 많은 준비가 필요했습니다."
        },
        // 3점 (25~30)
        new[] {
            "가능성은 보이나 완성도가 아쉽습니다.",
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
            "완성도 높은 수작입니다.",
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

        // 슬롯 초기화
        foreach (var slot in criticSlots)
            slot.SetActive(false);

        confirmButton.gameObject.SetActive(false);
        reviewPanel.SetActive(true);

        StartCoroutine(RevealCritics(rawScore));
    }

    IEnumerator RevealCritics(float rawScore)
    {
        int score = CalcCriticScore(rawScore);

        for (int i = 0; i < criticSlots.Length; i++)
        {
            yield return new WaitForSeconds(criticRevealDelay);

            criticSlots[i].SetActive(true);
            criticNameTexts[i].text    = CriticNames[i];
            criticScoreTexts[i].text   = $"{score}점";
            criticCommentTexts[i].text = GetComment(score, i);
        }

        yield return new WaitForSeconds(0.5f);
        confirmButton.gameObject.SetActive(true);
    }

    int CalcCriticScore(float rawScore)
    {
        if (rawScore >= 90) return 10;
        if (rawScore >= 85) return 9;
        if (rawScore >= 80) return 8;
        if (rawScore >= 70) return 7;
        if (rawScore >= 60) return 6;
        if (rawScore >= 50) return 7;
        if (rawScore >= 40) return 6;
        if (rawScore >= 35) return 5;
        if (rawScore >= 30) return 4;
        if (rawScore >= 25) return 3;
        if (rawScore >= 20) return 2;
        return 1;
    }

    string GetComment(int score, int criticIndex)
    {
        int idx = Mathf.Clamp(score - 1, 0, Comments.Length - 1);
        var pool = Comments[idx];
        return pool[criticIndex % pool.Length];
    }

    public void OnClickConfirm()
    {
        reviewPanel.SetActive(false);
        _onComplete?.Invoke();
    }
}