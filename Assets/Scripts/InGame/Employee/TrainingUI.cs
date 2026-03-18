using System.Collections;
using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class TrainingUI : MonoBehaviour
{
    public static TrainingUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject listPanel;
    public GameObject trainingPanel;
    public GameObject resultPanel;

    [Header("List")]
    public Transform slotParent;
    public GameObject trainingSlotPrefab;

    [Header("Training Panel")]
    public TextMeshProUGUI selectedNameText;
    public TextMeshProUGUI selectedRoleText;
    public TextMeshProUGUI selectedGradeText;
    public TextMeshProUGUI selectedPotentialText;
    public TextMeshProUGUI selectedDevelopText;
    public TextMeshProUGUI selectedPlanningText;
    public TextMeshProUGUI selectedArtText;
    public TextMeshProUGUI selectedPerfectionText;
    public TextMeshProUGUI selectedEnhancementText; // 현재 강화 수치

    [Header("Result Panel")]
    public TextMeshProUGUI resultNameText;
    public TextMeshProUGUI resultRoleText;
    public TextMeshProUGUI resultPotentialText;
    public TextMeshProUGUI resultDevelopText;
    public TextMeshProUGUI resultPlanningText;
    public TextMeshProUGUI resultArtText;
    public TextMeshProUGUI resultPerfectionText;
    public TextMeshProUGUI resultEnhancementText; // 강화 결과 텍스트
    public TextMeshProUGUI resultOutcomeText;      // 성공/유지/하락 텍스트

    private EmployeeData _selectedEmployee;
    private struct EnhanceResult
    {
        public string statName;
        public int gain;
    }

    // 강화 확률 테이블 [level] = (성공%, 유지%, 하락%)
    private static readonly (int success, int maintain, int downgrade)[] EnhanceTable =
    {
    (99, 0,  0),  // 0  - 하락 없음, 나머지 5%는 그냥 성공 안함(유지)
    (95, 0,  5), // 1
    (89, 0,  11), // 2
    (89, 0,  11), // 3
    (84, 0,  16), // 4
    (78, 0,  22), // 5
    (73, 0,  27), // 6
    (68, 0,  32), // 7
    (63, 0,  37), // 8
    (57, 0,  43), // 9
    (52, 0,  48), // 10
    (47, 0,  53), // 11
    (42, 0,  58), // 12
    (37, 0,  63), // 13
    (31, 0,  69), // 14
    (30, 70, 0),  // 15 - 이후 하락 없음
    (28, 72, 0),  // 16
    (25, 75, 0),  // 17
    (22, 78, 0),  // 18
    (20, 80, 0),  // 19
    (18, 82, 0),  // 20
    (15, 85, 0),  // 21
    (10, 90, 0),  // 22
    (5,  95, 0),  // 23
    (3,  97, 0),  // 24
};
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void OpenTraining()
    {
        GameTimeManager.Instance?.StopTime();
        gameObject.SetActive(true);
        trainingPanel.SetActive(false);
        resultPanel.SetActive(false);
        ShowList();
    }

    void ShowList()
    {
        foreach (Transform child in slotParent)
            Destroy(child.gameObject);

        foreach (var employee in EmployeeManager.Instance.ownedEmployees)
        {
            var slot = Instantiate(trainingSlotPrefab, slotParent);
            slot.GetComponent<TrainingSlotUI>().Setup(employee, this);
        }

        listPanel.SetActive(true);
    }

    public void OnSelectEmployee(EmployeeData employee)
    {
        _selectedEmployee = employee;

        selectedNameText.text = employee.employeeName;
        selectedRoleText.text = employee.RoleToString();
        selectedGradeText.text = employee.GradeToString();
        selectedPotentialText.text = employee.PotentialToString();
        selectedDevelopText.text = employee.DevelopText();
        selectedPlanningText.text = employee.PlanningText();
        selectedArtText.text = employee.ArtText();
        selectedPerfectionText.text = employee.PerfectionText();
        selectedEnhancementText.text = $"강화 수치: +{employee.enhancementLevel}";

        listPanel.SetActive(false);
        trainingPanel.SetActive(true);
    }

    public void OnClickEnhance()
    {
        int level = _selectedEmployee.enhancementLevel;

        if (level >= 25)
        {
            resultOutcomeText.text = "이미 최대 강화 수치입니다!";
            return;
        }

        // 강화 전 수치 저장
        int beforeDev = _selectedEmployee.developSkill;
        int beforePlanning = _selectedEmployee.planningSkill;
        int beforeArt = _selectedEmployee.artSkill;
        int beforePerfection = _selectedEmployee.perfectionSkill;
        int beforeLevel = _selectedEmployee.enhancementLevel;

        var (success, maintain, downgrade) = EnhanceTable[level];
        int roll = UnityEngine.Random.Range(0, 100);

        string outcome;
        List<EnhanceResult> results = null;

        if (roll < success)
        {
            _selectedEmployee.enhancementLevel++;
            EmployeeManager.Instance.ApplyEnhancement(_selectedEmployee);
            outcome = "O 강화 성공!";
        }
        else if (downgrade > 0 && roll >= success + maintain)
        {
            _selectedEmployee.enhancementLevel = Mathf.Max(0, level - 1);
            outcome = "X 강화 하락";
        }
        else
        {
            outcome = "- 강화 유지";
        }

        EmployeeManager.Instance.UpdateEmployee(_selectedEmployee);
        ShowResult(outcome, results, beforeDev, beforePlanning, beforeArt, beforePerfection, beforeLevel);
    }
    string StatDisplayName(string key) => key switch
    {
        "develop" => "개발",
        "planning" => "기획",
        "art" => "아트",
        "perfection" => "완성도",
        _ => key
    };
    void ShowResult(string outcome, List<EnhanceResult> results,
                int beforeDev, int beforePlanning, int beforeArt, int beforePerfection, int beforeLevel)
    {
        resultNameText.text = _selectedEmployee.employeeName;
        resultRoleText.text = _selectedEmployee.RoleToString();
        resultPotentialText.text = _selectedEmployee.PotentialToString();
        resultEnhancementText.text = $"강화 수치: +{beforeLevel} → +{_selectedEmployee.enhancementLevel}";
        resultOutcomeText.text = outcome;

        // 기본: 변화 없으면 before == after
        resultDevelopText.text = beforeDev != _selectedEmployee.developSkill
            ? $"개발: {beforeDev} → {_selectedEmployee.developSkill}"
            : $"개발: {_selectedEmployee.developSkill}";

        resultPlanningText.text = beforePlanning != _selectedEmployee.planningSkill
            ? $"기획: {beforePlanning} → {_selectedEmployee.planningSkill}"
            : $"기획: {_selectedEmployee.planningSkill}";

        resultArtText.text = beforeArt != _selectedEmployee.artSkill
            ? $"아트: {beforeArt} → {_selectedEmployee.artSkill}"
            : $"아트: {_selectedEmployee.artSkill}";

        resultPerfectionText.text = beforePerfection != _selectedEmployee.perfectionSkill
            ? $"완성도: {beforePerfection} → {_selectedEmployee.perfectionSkill}"
            : $"완성도: {_selectedEmployee.perfectionSkill}";

        trainingPanel.SetActive(false);
        resultPanel.SetActive(true);
    }

    public void OnClickResultConfirm()
    {
        // 결과 확인 후 다시 강화 패널로
        resultPanel.SetActive(false);
        OnSelectEmployee(_selectedEmployee);
    }

    public void OnClickBack()
    {
        trainingPanel.SetActive(false);
        ShowList();
    }

    public void OnClickClose()
    {
        GameTimeManager.Instance?.StartTime();
        listPanel.SetActive(false);
        trainingPanel.SetActive(false);
    }
}