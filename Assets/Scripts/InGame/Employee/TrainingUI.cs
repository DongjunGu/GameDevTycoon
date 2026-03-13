using System.Collections;
using UnityEngine;
using TMPro;

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

    // 강화 확률 테이블 [level] = (성공%, 유지%, 하락%)
    private static readonly (int success, int maintain, int downgrade)[] EnhanceTable =
    {
    (95, 0,  0),  // 0  - 하락 없음, 나머지 5%는 그냥 성공 안함(유지)
    (90, 0,  10), // 1
    (85, 0,  15), // 2
    (80, 0,  20), // 3
    (75, 0,  25), // 4
    (70, 0,  30), // 5
    (65, 0,  35), // 6
    (60, 0,  40), // 7
    (55, 0,  45), // 8
    (50, 0,  50), // 9
    (45, 0,  55), // 10
    (40, 0,  60), // 11
    (35, 0,  65), // 12
    (30, 0,  70), // 13
    (25, 0,  75), // 14
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

        var (success, maintain, downgrade) = EnhanceTable[level];
        int roll = UnityEngine.Random.Range(0, 100);

        string outcome;
        int gain = 0;

        if (roll < success)
        {
            // 성공
            _selectedEmployee.enhancementLevel++;
            _selectedEmployee.developSkill += 1;
            _selectedEmployee.planningSkill += 1;
            _selectedEmployee.artSkill += 1;
            _selectedEmployee.perfectionSkill += 1;
            gain = 1;
            outcome = "✨ 강화 성공!";
        }
        else if (roll < success + maintain)
        {
            // 유지
            outcome = "🔒 강화 유지";
        }
        else
        {
            // 하락
            _selectedEmployee.enhancementLevel = Mathf.Max(0, level - 1);
            outcome = "💔 강화 하락";
        }

        EmployeeManager.Instance.UpdateEmployee(_selectedEmployee);
        ShowResult(outcome, gain);
    }

    void ShowResult(string outcome, int gain)
    {
        resultNameText.text = _selectedEmployee.employeeName;
        resultRoleText.text = _selectedEmployee.RoleToString();
        resultPotentialText.text = _selectedEmployee.PotentialToString();
        resultEnhancementText.text = $"강화 수치: +{_selectedEmployee.enhancementLevel}";
        resultOutcomeText.text = outcome;

        resultDevelopText.text = gain > 0 ? $"개발: {_selectedEmployee.developSkill - gain} → {_selectedEmployee.developSkill}" : $"개발: {_selectedEmployee.developSkill}";
        resultPlanningText.text = gain > 0 ? $"기획: {_selectedEmployee.planningSkill - gain} → {_selectedEmployee.planningSkill}" : $"기획: {_selectedEmployee.planningSkill}";
        resultArtText.text = gain > 0 ? $"아트: {_selectedEmployee.artSkill - gain} → {_selectedEmployee.artSkill}" : $"아트: {_selectedEmployee.artSkill}";
        resultPerfectionText.text = gain > 0 ? $"완성도: {_selectedEmployee.perfectionSkill - gain} → {_selectedEmployee.perfectionSkill}" : $"완성도: {_selectedEmployee.perfectionSkill}";

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
        listPanel.SetActive(false);
        gameObject.SetActive(false);
    }
}