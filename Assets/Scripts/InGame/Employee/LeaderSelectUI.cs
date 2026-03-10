using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LeaderSelectUI : MonoBehaviour
{
    public static LeaderSelectUI Instance { get; private set; }

    [Header("UI")]
    public Transform leaderPanel;
    public GameObject leaderscorePanel;
    public TextMeshProUGUI titleText;
    public Transform slotParent;
    
    public GameObject leaderSlotPrefab;

    private LeaderType _currentType;
    private System.Action _onComplete;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Open(LeaderType type, System.Action onComplete)
    {
        _currentType = type;
        _onComplete  = onComplete;

        titleText.text = type switch
        {
            LeaderType.Planner    => "기획팀장 선택",
            LeaderType.Programmer => "개발팀장 선택",
            LeaderType.Artist     => "아트팀장 선택",
            _ => ""
        };

        // role 필터링
        EmployeeRole filterRole = type switch
        {
            LeaderType.Planner    => EmployeeRole.Planner,
            LeaderType.Programmer => EmployeeRole.Programmer,
            LeaderType.Artist     => EmployeeRole.Artist,
            _ => EmployeeRole.Planner
        };

        foreach (Transform child in slotParent)
            Destroy(child.gameObject);

        var filtered = EmployeeManager.Instance.ownedEmployees
            .FindAll(e => e.role == filterRole);

        foreach (var employee in filtered)
        {
            var slot = Instantiate(leaderSlotPrefab, slotParent);
            slot.GetComponent<LeaderSlotUI>().Setup(employee, this);
        }

        leaderPanel.gameObject.SetActive(true);
    }

    public void OnSelectLeader(EmployeeData employee)
    {
        DevelopmentManager.Instance.SetLeader(_currentType, employee);
        leaderPanel.gameObject.SetActive(false);
        leaderscorePanel.SetActive(true);
    }
}