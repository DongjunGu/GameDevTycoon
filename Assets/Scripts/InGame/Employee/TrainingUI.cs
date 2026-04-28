using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
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
    public TextMeshProUGUI selectedCreativityText;
    public TextMeshProUGUI selectedEnhancementText;
    public TextMeshProUGUI selectedCostText;
    public TextMeshProUGUI selectedSatisfactionText;
    public TextMeshProUGUI selectedSuccessRateText;
    public TextMeshProUGUI selectedMaintainRateText;
    public TextMeshProUGUI selectedDowngradeRateText;
    public Button enhanceButton;
    public TextMeshProUGUI enhanceButtonText;

    [Header("Result Panel")]
    public TextMeshProUGUI resultNameText;
    public TextMeshProUGUI resultRoleText;
    public TextMeshProUGUI resultPotentialText;
    public TextMeshProUGUI resultDevelopText;
    public TextMeshProUGUI resultPlanningText;
    public TextMeshProUGUI resultArtText;
    public TextMeshProUGUI resultCreativityText;
    public TextMeshProUGUI resultEnhancementText; // 강화 결과 텍스트
    public TextMeshProUGUI resultOutcomeText;
    public TextMeshProUGUI resultSatisfactionText;

    private static int GetMaxEnhancement(EmployeeGrade grade) => grade switch
    {
        EmployeeGrade.Normal => 15,
        EmployeeGrade.Rare   => 15,
        EmployeeGrade.Epic   => 20,
        EmployeeGrade.Unique => 25,
        _ => 15
    };

    private EmployeeData _selectedEmployee;
    private struct EnhanceResult
    {
        public string statName;
        public int gain;
    }

    // 강화 비용 테이블 [level]
    private static readonly int[] EnhanceCostTable =
    {
            50,  // 0→1
           100,  // 1→2
           100,  // 2→3
           100,  // 3→4
           150,  // 4→5
           150,  // 5→6
           150,  // 6→7
           200,  // 7→8
           200,  // 8→9
           200,  // 9→10
           250,  // 10→11
         2_000,  // 11→12
         4_000,  // 12→13
         6_000,  // 13→14
         8_000,  // 14→15
        10_000,  // 15→16
        20_000,  // 16→17
        30_000,  // 17→18
        40_000,  // 18→19
        50_000,  // 19→20
        70_000,  // 20→21
        90_000,  // 21→22
       110_000,  // 22→23
       150_000,  // 23→24
       200_000,  // 24→25
    };

    // 강화 확률 테이블 [level] = (성공%, 유지%, 하락%)
    // 0~10: 실패 시 하락 가능, 11~24: 하락 없음
    private static readonly (int success, int maintain, int downgrade)[] EnhanceTable =
    {
        (99,  0,  0),  // 0
        (95,  0,  5),  // 1
        (90,  0, 10),  // 2
        (85,  0, 15),  // 3
        (80,  0, 20),  // 4
        (75,  0, 25),  // 5
        (70,  0, 30),  // 6
        (65,  0, 35),  // 7
        (60,  0, 40),  // 8
        (55,  0, 45),  // 9
        (50,  0, 50),  // 10
        (30, 70,  0),  // 11
        (30, 70,  0),  // 12
        (30, 70,  0),  // 13
        (30, 70,  0),  // 14
        (20, 80,  0),  // 15
        (20, 80,  0),  // 16
        (20, 80,  0),  // 17
        (20, 80,  0),  // 18
        (15, 85,  0),  // 19
        (15, 85,  0),  // 20
        (15, 85,  0),  // 21
        (15, 85,  0),  // 22
        (10, 90,  0),  // 23
        ( 5, 95,  0),  // 24
    };
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // 카드 컨텍스트로 열렸을 때 닫힘 콜백 + 플래그 (true면 ListPanel 단계 스킵)
    private System.Action _onClosedCallback;
    private bool _isCardContext;

    public void OpenTraining()
    {
        GameTimeManager.Instance?.StopTime();
        gameObject.SetActive(true);
        trainingPanel.SetActive(false);
        resultPanel.SetActive(false);
        ShowList();
    }

    // EmployeeCardUI 등에서 특정 직원 강화 패널을 즉시 띄울 때 사용
    public void OpenTrainingForEmployee(EmployeeData employee, System.Action onClosed = null)
    {
        if (employee == null) return;
        _isCardContext = true;
        _onClosedCallback = onClosed;

        GameTimeManager.Instance?.StopTime();
        gameObject.SetActive(true);
        listPanel.SetActive(false);
        resultPanel.SetActive(false);
        OnSelectEmployee(employee); // 강화 패널 바로 표시
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
        selectedCreativityText.text = employee.CreativityText();
        selectedEnhancementText.text = $"강화 수치: +{employee.enhancementLevel}";

        int nextLevel = employee.enhancementLevel;
        if (nextLevel < EnhanceCostTable.Length)
            selectedCostText.text = $"강화 비용:\n{EnhanceCostTable[nextLevel]:N0}G";
        else
            selectedCostText.text = "강화 비용: -";
        selectedSatisfactionText.text = employee.SatisfactionText();
        RefreshEnhanceButton(employee);
        RefreshRateTexts(employee);
        listPanel.SetActive(false);
        trainingPanel.SetActive(true);
    }

    void RefreshRateTexts(EmployeeData employee)
    {
        int level = employee.enhancementLevel;
        int maxLevel = GetMaxEnhancement(employee.grade);

        if (level >= maxLevel)
        {
            selectedSuccessRateText.text   = "성공: -";
            selectedMaintainRateText.text  = "유지: -";
            selectedDowngradeRateText.text = "하락: -";
            return;
        }

        var (success, maintain, downgrade) = EnhanceTable[level];
        int implicitMaintain = 100 - success - downgrade;

        selectedSuccessRateText.text   = $"성공: {success}%";
        selectedMaintainRateText.text  = maintain > 0
            ? $"유지: {maintain}%"
            : $"유지: {implicitMaintain}%";
        selectedDowngradeRateText.text = $"하락: {downgrade}%";
    }

    void RefreshEnhanceButton(EmployeeData employee)
    {
        bool isMax = employee.enhancementLevel >= GetMaxEnhancement(employee.grade);
        enhanceButton.interactable = !isMax;
        enhanceButtonText.text = isMax ? "최대치입니다" : "강화하기";
    }

    public void OnClickEnhance()
    {
        int level = _selectedEmployee.enhancementLevel;

        if (level >= GetMaxEnhancement(_selectedEmployee.grade))
        {
            resultOutcomeText.text = "이미 최대 강화 수치입니다!";
            return;
        }

        // 강화 전 수치 저장
        int beforeDev = _selectedEmployee.developSkill;
        int beforePlanning = _selectedEmployee.planningSkill;
        int beforeArt = _selectedEmployee.artSkill;
        int beforeCreativity = _selectedEmployee.creativitySkill;
        int beforeLevel = _selectedEmployee.enhancementLevel;

        int cost = EnhanceCostTable[level];
        if (!MoneyManager.Instance.SpendGold(cost)) return;

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
            EmployeeManager.Instance.ReverseEnhancement(_selectedEmployee, level);
            _selectedEmployee.enhancementLevel = Mathf.Max(0, level - 1);
            outcome = "X 강화 하락";
        }
        else
        {
            outcome = "- 강화 유지";
        }

        EmployeeManager.Instance.UpdateEmployee(_selectedEmployee);
        GameTimeManager.Instance?.SaveGameTime();
        ProjectSaveManager.Instance?.SaveProject();
        ShowResult(outcome, results, beforeDev, beforePlanning, beforeArt, beforeCreativity, beforeLevel);
    }
    string StatDisplayName(string key) => key switch
    {
        "develop" => "개발",
        "planning" => "기획",
        "art" => "아트",
        "creativity" => "창의성",
        _ => key
    };
    void ShowResult(string outcome, List<EnhanceResult> results,
                int beforeDev, int beforePlanning, int beforeArt, int beforeCreativity, int beforeLevel)
    {
        resultNameText.text = _selectedEmployee.employeeName;
        resultRoleText.text = _selectedEmployee.RoleToString();
        resultPotentialText.text = _selectedEmployee.PotentialToString();
        resultSatisfactionText.text = _selectedEmployee.SatisfactionText();
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

        resultCreativityText.text = beforeCreativity != _selectedEmployee.creativitySkill
            ? $"창의성: {beforeCreativity} → {_selectedEmployee.creativitySkill}"
            : $"창의성: {_selectedEmployee.creativitySkill}";

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
        // 카드 컨텍스트면 List 단계가 없으므로 뒤로가기 = 전체 닫기
        if (_isCardContext) { OnClickClose(); return; }
        trainingPanel.SetActive(false);
        ShowList();
    }

    public void OnClickClose()
    {
        GameTimeManager.Instance?.StartTime();
        listPanel.SetActive(false);
        trainingPanel.SetActive(false);

        if (_isCardContext)
        {
            _isCardContext = false;
            var cb = _onClosedCallback;
            _onClosedCallback = null;
            cb?.Invoke();
        }
    }
}