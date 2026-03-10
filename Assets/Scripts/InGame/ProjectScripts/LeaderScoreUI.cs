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

    public void Show(EmployeeData employee, LeaderType type, int n, float r,
                     float tickDelay, System.Action onComplete)
    {
        _onComplete = onComplete;

        _planningTotal = 0f;
        _developTotal  = 0f;
        _artTotal      = 0f;

        leaderNameText.text  = employee.employeeName;
        leaderRoleText.text  = employee.RoleToString();
        leaderGradeText.text = employee.GradeToString();
        tickCountText.text   = "0회째";

        UpdateScoreText();

        confirmButton.interactable = false;
        gameObject.SetActive(true);
        
        StartCoroutine(ApplyScoreCoroutine(type, n, r, tickDelay));
    }

    IEnumerator ApplyScoreCoroutine(LeaderType type, int n, float r, float tickDelay)
    {
        yield return new WaitForSeconds(1.5f);
        for (int i = 0; i < n; i++)
        {
            int target = Mathf.RoundToInt(r);
            int current = 0;

            // 0.1초마다 1씩 증가하면서 target까지
            while (current < target)
            {
                yield return new WaitForSeconds(0.1f);
                current++;

                switch (type)
                {
                    case LeaderType.Planner:    _planningTotal += 1f; break;
                    case LeaderType.Programmer: _developTotal  += 1f; break;
                    case LeaderType.Artist:     _artTotal      += 1f; break;
                }

                UpdateScoreText();
                DevelopmentPanelUI.Instance.AddValues(
                    type == LeaderType.Planner    ? 1f : 0f,
                    type == LeaderType.Programmer ? 1f : 0f,
                    type == LeaderType.Artist     ? 1f : 0f
                );
            }

            tickCountText.text = $"{i + 1}회째";

            // 마지막 tick이 아니면 1.5초 대기
            if (i < n - 1)
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
        _onComplete?.Invoke();
    }
}