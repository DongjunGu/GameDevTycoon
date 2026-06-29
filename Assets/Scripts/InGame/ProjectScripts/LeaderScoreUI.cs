using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LeaderScoreUI : MonoBehaviour
{
    public static LeaderScoreUI Instance { get; private set; }

    [Header("직원 정보")]
    public GameObject leaderscorePanel;
    public TextMeshProUGUI leaderNameText;
    public TextMeshProUGUI leaderRoleText;
    public TextMeshProUGUI leaderGradeText;
    public TextMeshProUGUI leaderPotentialText;
    public TextMeshProUGUI categoryText;      // 현재 진행 중인 카테고리 (기획/개발/아트)

    [Header("회차 결과 (1~4회차 순서대로, 길이 4)")]
    public TextMeshProUGUI[] roundScoreTexts; // 각 회차 점수
    public TextMeshProUGUI dsText;            // 누적 ds (스트레스)
    public Slider dsSlider;                   // 누적 ds (0~100)
    public TextMeshProUGUI totalText;         // 팀장 점수 총합
    public Button confirmButton;

    [Header("연출")]
    public float rollDuration = 1f;   // 회차 점수/ds 상승 시간
    public float roundGap = 1f;       // 회차 간 간격

    private System.Action _onComplete;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // fullRoundScores: 차감 전 회차 점수 / roundScores: 차감 반영 최종 회차 점수
    // cumDsAfter: 회차 종료 시점 누적 ds / overflowRound: 누적 ds 100 초과 회차(없으면 -1)
    public void Show(EmployeeData employee, LeaderType type,
                     float[] fullRoundScores, float[] roundScores, float[] cumDsAfter,
                     float total, int overflowRound, float cutFactor,
                     int hunsuBonus, LeaderType hunsuBonusTarget, System.Action onComplete)
    {
        _onComplete = onComplete;

        if (leaderNameText)      leaderNameText.text      = employee.employeeName;
        if (leaderRoleText)      leaderRoleText.text      = employee.RoleToString();
        if (leaderGradeText)     leaderGradeText.text     = employee.GradeToString();
        if (leaderPotentialText) leaderPotentialText.text = employee.PotentialToString();
        if (categoryText) categoryText.text = type switch
        {
            LeaderType.Planner    => "기획",
            LeaderType.Programmer => "개발",
            LeaderType.Artist     => "아트",
            _ => ""
        };

        // 초기화
        if (roundScoreTexts != null)
            foreach (var t in roundScoreTexts) if (t) t.text = "0";
        if (dsText) dsText.text = "0";
        if (dsSlider) { dsSlider.minValue = 0f; dsSlider.maxValue = 100f; dsSlider.value = 0f; }
        if (totalText) totalText.text = "0";

        if (confirmButton) confirmButton.interactable = false;
        leaderscorePanel.SetActive(true);
        ModalGate.I.Register(this); // 점수 표시 중 다른 모달(상인 Alert 등) 차단

        StartCoroutine(PlayCoroutine(type, fullRoundScores, roundScores, cumDsAfter,
                                     total, overflowRound, cutFactor, hunsuBonus, hunsuBonusTarget));
    }

    IEnumerator PlayCoroutine(LeaderType type,
                              float[] fullRoundScores, float[] roundScores, float[] cumDsAfter,
                              float total, int overflowRound, float cutFactor,
                              int hunsuBonus, LeaderType hunsuBonusTarget)
    {
        yield return new WaitForSeconds(0.5f);

        float dur = Mathf.Max(0.01f, rollDuration);
        float displayedTotal = 0f;
        float prevCumDs = 0f;

        for (int r = 0; r < 4; r++)
        {
            bool isOverflow = (overflowRound == r);
            float targetDs    = cumDsAfter[r];
            float targetRound = isOverflow ? 0f : fullRoundScores[r]; // 오버플로 회차는 0점

            float startTotal = displayedTotal;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);

                float rs = Mathf.Lerp(0f, targetRound, t);
                SetRoundText(r, rs);

                float ds = Mathf.Lerp(prevCumDs, targetDs, t);
                if (dsText) dsText.text = Mathf.RoundToInt(ds).ToString();
                if (dsSlider) dsSlider.value = Mathf.Clamp(ds, 0f, 100f);

                if (totalText) totalText.text = Mathf.RoundToInt(startTotal + rs).ToString();

                yield return null;
            }

            // 회차 확정
            SetRoundText(r, targetRound);
            if (dsText) dsText.text = Mathf.RoundToInt(targetDs).ToString();
            if (dsSlider) dsSlider.value = Mathf.Clamp(targetDs, 0f, 100f);
            displayedTotal = startTotal + targetRound;
            if (totalText) totalText.text = Mathf.RoundToInt(displayedTotal).ToString();
            prevCumDs = targetDs;

            if (isOverflow)
            {
                // 누적 ds 100 초과: 전 회차 점수 일괄 차감 연출 후 종료
                yield return StartCoroutine(ApplyCutCoroutine(fullRoundScores, roundScores, r));
                break;
            }

            if (r < 3)
                yield return new WaitForSeconds(roundGap);
        }

        // 총합 최종 확정 + 역할 누적스탯 반영 (+ 훈수쟁이 보너스는 기획/아트로)
        if (totalText) totalText.text = Mathf.RoundToInt(total).ToString();

        float pl = type == LeaderType.Planner   ? total : 0f;
        float dv = type == LeaderType.Programmer ? total : 0f;
        float ar = type == LeaderType.Artist     ? total : 0f;
        if (hunsuBonus > 0)
        {
            if (hunsuBonusTarget == LeaderType.Planner) pl += hunsuBonus;
            else                                        ar += hunsuBonus;
        }
        DevelopmentPanelUI.Instance.AddValues(pl, dv, ar, 0f, 0f);

        yield return new WaitForSeconds(0.5f);
        if (confirmButton) confirmButton.interactable = true;
    }

    // 누적 ds 100 초과 시: 전 회차(0..overflowRound-1) 점수를 full → cut 값으로 내림
    IEnumerator ApplyCutCoroutine(float[] fullRoundScores, float[] roundScores, int overflowRound)
    {
        if (overflowRound <= 0) yield break;

        float dur = 0.5f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float sum = 0f;
            for (int k = 0; k < overflowRound; k++)
            {
                float v = Mathf.Lerp(fullRoundScores[k], roundScores[k], t);
                SetRoundText(k, v);
                sum += v;
            }
            if (totalText) totalText.text = Mathf.RoundToInt(sum).ToString();
            yield return null;
        }

        for (int k = 0; k < overflowRound; k++)
            SetRoundText(k, roundScores[k]);
    }

    void SetRoundText(int index, float value)
    {
        if (roundScoreTexts != null && index >= 0 && index < roundScoreTexts.Length && roundScoreTexts[index])
            roundScoreTexts[index].text = Mathf.RoundToInt(value).ToString();
    }

    public void OnClickConfirm()
    {
        leaderscorePanel.SetActive(false);
        ModalGate.I.Unregister(this);
        _onComplete?.Invoke();
    }
}
