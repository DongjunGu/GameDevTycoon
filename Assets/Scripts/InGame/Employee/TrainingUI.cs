using UnityEngine;
using TMPro;
using System.Collections;

public enum TrainingType { Development, All }

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
    public TextMeshProUGUI selectedDevelopText;
    public TextMeshProUGUI selectedPlanningText;
    public TextMeshProUGUI selectedArtText;

    [Header("Result Panel")]
    public TextMeshProUGUI resultNameText;
    public TextMeshProUGUI resultRoleText;
    public TextMeshProUGUI resultGradeText;
    public TextMeshProUGUI resultDevelopText;
    public TextMeshProUGUI resultPlanningText;
    public TextMeshProUGUI resultArtText;

    private EmployeeData _selectedEmployee;

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
        selectedDevelopText.text = $"개발: {employee.developSkill}";
        selectedPlanningText.text = $"기획: {employee.planningSkill}";
        selectedArtText.text = $"아트: {employee.artSkill}";

        listPanel.SetActive(false);
        trainingPanel.SetActive(true);
    }

    // 개발 훈련
    public void OnClickDevelopmentTraining()
    {
        int devGain = UnityEngine.Random.Range(10, 16);
        _selectedEmployee.developSkill += devGain;

        ShowResult(devGain, 0, 0);
    }

    // 종합 훈련
    public void OnClickAllTraining()
    {
        int devGain = UnityEngine.Random.Range(3, 6);
        int planningGain = UnityEngine.Random.Range(3, 6);
        int artGain = UnityEngine.Random.Range(3, 6);

        _selectedEmployee.developSkill += devGain;
        _selectedEmployee.planningSkill += planningGain;
        _selectedEmployee.artSkill += artGain;

        ShowResult(devGain, planningGain, artGain);
    }

    void ShowResult(int devGain, int planningGain, int artGain)
    {
        EmployeeManager.Instance.UpdateEmployee(_selectedEmployee);

        resultNameText.text = _selectedEmployee.employeeName;
        resultRoleText.text = _selectedEmployee.RoleToString();
        resultGradeText.text = _selectedEmployee.GradeToString();

        // 훈련 전 수치 (증가량 빼서 계산)
        int beforeDev = _selectedEmployee.developSkill - devGain;
        int beforePlanning = _selectedEmployee.planningSkill - planningGain;
        int beforeArt = _selectedEmployee.artSkill - artGain;

        // 처음엔 훈련 전 수치 표시
        resultDevelopText.text = devGain > 0
            ? $"개발: {beforeDev} +{devGain}"
            : $"개발: {beforeDev}";

        resultPlanningText.text = planningGain > 0
            ? $"기획: {beforePlanning} +{planningGain}"
            : $"기획: {beforePlanning}";

        resultArtText.text = artGain > 0
            ? $"아트: {beforeArt} +{artGain}"
            : $"아트: {beforeArt}";

        trainingPanel.SetActive(false);
        resultPanel.SetActive(true);

        // 2초 후 훈련 후 수치로 변경
        StartCoroutine(UpdateResultAfterDelay(devGain, planningGain, artGain, 2f));
    }

    IEnumerator UpdateResultAfterDelay(int devGain, int planningGain, int artGain, float delay)
    {
        yield return new WaitForSeconds(delay);

        resultDevelopText.text = devGain > 0
            ? $"개발: {_selectedEmployee.developSkill} +{devGain}"
            : $"개발: {_selectedEmployee.developSkill}";

        resultPlanningText.text = planningGain > 0
            ? $"기획: {_selectedEmployee.planningSkill} +{planningGain}"
            : $"기획: {_selectedEmployee.planningSkill}";

        resultArtText.text = artGain > 0
            ? $"아트: {_selectedEmployee.artSkill} +{artGain}"
            : $"아트: {_selectedEmployee.artSkill}";
    }

    public void OnClickResultConfirm()
    {
        resultPanel.SetActive(false);
        ShowList();
    }

    public void OnClickBack()
    {
        trainingPanel.SetActive(false);
        ShowList();
    }

    public void OnClickClose()
    {
        listPanel.SetActive(false);
    }
}