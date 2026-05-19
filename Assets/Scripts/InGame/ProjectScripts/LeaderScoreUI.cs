using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LeaderScoreUI : MonoBehaviour
{
    public static LeaderScoreUI Instance { get; private set; }

    [Header("UI")]
    public GameObject leaderscorePanel;
    public TextMeshProUGUI leaderNameText;
    public TextMeshProUGUI leaderRoleText;
    public TextMeshProUGUI leaderGradeText;
    public TextMeshProUGUI leaderPotentialText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI tickCountText;
    public Button confirmButton;

    private float _planningTotal;
    private float _developTotal;
    private float _artTotal;

    private System.Action _onComplete;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show(EmployeeData employee, LeaderType type, float[] scores,
                     float tickDelay, System.Action onComplete)
    {
        _onComplete = onComplete;

        _planningTotal = 0f;
        _developTotal = 0f;
        _artTotal = 0f;

        leaderNameText.text = employee.employeeName;
        leaderRoleText.text = employee.RoleToString();
        leaderGradeText.text = employee.GradeToString();
        leaderPotentialText.text = employee.PotentialToString();
        tickCountText.text = "0회째";

        UpdateScoreText();

        confirmButton.interactable = false;
        leaderscorePanel.SetActive(true);
        ModalGate.I.Register(this); // 점수 표시 중 다른 모달(상인 Alert 등) 차단

        StartCoroutine(ApplyScoreCoroutine(type, scores, tickDelay));
    }

    IEnumerator ApplyScoreCoroutine(LeaderType type, float[] scores, float tickDelay)
    {
        yield return new WaitForSeconds(1.5f);
        for (int i = 0; i < scores.Length; i++)
        {
            tickCountText.text = $"{i + 1}회째";

            int target = Mathf.RoundToInt(scores[i]);
            int current = 0;

            // 0.1초마다 1씩 증가하면서 target까지
            while (current < target)
            {
                yield return new WaitForSeconds(0.1f);
                current++;

                switch (type)
                {
                    case LeaderType.Planner: _planningTotal += 1f; break;
                    case LeaderType.Programmer: _developTotal += 1f; break;
                    case LeaderType.Artist: _artTotal += 1f; break;
                }

                UpdateScoreText();
                DevelopmentPanelUI.Instance.AddValuesInstant(
                    type == LeaderType.Planner ? 1f : 0f,
                    type == LeaderType.Programmer ? 1f : 0f,
                    type == LeaderType.Artist ? 1f : 0f,
                    0f, // bug
                    0f  // creativity
                );
            }

            // 마지막 tick이 아니면 1.5초 대기
            if (i < scores.Length - 1)
                yield return new WaitForSeconds(1.5f);
        }

        yield return new WaitForSeconds(1.5f);
        confirmButton.interactable = true;
    }

    void UpdateScoreText()
    {
        scoreText.text = $"기획: {Mathf.RoundToInt(_planningTotal)}  " +
                         $"개발: {Mathf.RoundToInt(_developTotal)}  " +
                         $"아트: {Mathf.RoundToInt(_artTotal)}";
    }

    public void OnClickConfirm()
    {
        leaderscorePanel.SetActive(false);
        ModalGate.I.Unregister(this);
        _onComplete?.Invoke();
    }
}