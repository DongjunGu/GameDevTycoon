using System.Collections;
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
        // 상시 개발틱 카운트업 진행 중이면 끝날 때까지 대기 후 표시
        if (StatTickPopup.ActiveCount > 0)
        {
            StartCoroutine(OpenAfterPopups(type, onComplete));
            return;
        }
        OpenInternal(type, onComplete);
    }

    IEnumerator OpenAfterPopups(LeaderType type, System.Action onComplete)
    {
        // 호출자가 미리 StopTime 했어도 카운트업 끝까지 시간을 풀어줌 (deadlock 방지)
        bool wasStopped = GameTimeManager.Instance != null && !GameTimeManager.Instance.IsRunning;
        if (wasStopped) GameTimeManager.Instance.StartTime();

        while (StatTickPopup.ActiveCount > 0)
            yield return null;

        if (wasStopped) GameTimeManager.Instance?.StopTime();
        OpenInternal(type, onComplete);
    }

    void OpenInternal(LeaderType type, System.Action onComplete)
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
            .FindAll(e => e.role == filterRole
                          && (DispatchManager.Instance == null || !DispatchManager.Instance.IsDispatched(e.id))); // 파견중 직원은 팀장 후보 제외

        // CEO 는 ownedEmployees 에 없지만 모든 role 의 팀장 후보로 항상 노출
        var ceo = EmployeeManager.Instance.CEO;
        if (ceo != null) filtered.Insert(0, ceo);

        foreach (var employee in filtered)
        {
            var slot = Instantiate(leaderSlotPrefab, slotParent);
            slot.GetComponent<LeaderSlotUI>().Setup(employee, this);
        }

        leaderPanel.gameObject.SetActive(true);
        ModalGate.I.Register(this);
    }

    public void OnSelectLeader(EmployeeData employee)
    {
        DevelopmentManager.Instance.SetLeader(_currentType, employee);
        leaderPanel.gameObject.SetActive(false);
        ModalGate.I.Unregister(this);
        leaderscorePanel.SetActive(true);
    }
}