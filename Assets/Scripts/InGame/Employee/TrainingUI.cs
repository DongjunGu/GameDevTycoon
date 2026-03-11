using System.Collections;
using UnityEngine;
using TMPro;

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
    public TextMeshProUGUI selectedPerfectionText;

    [Header("Result Panel")]
    public TextMeshProUGUI resultNameText;
    public TextMeshProUGUI resultRoleText;
    public TextMeshProUGUI resultGradeText;
    public TextMeshProUGUI resultDevelopText;
    public TextMeshProUGUI resultPlanningText;
    public TextMeshProUGUI resultArtText;
    public TextMeshProUGUI resultPerfectionText;

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

        selectedNameText.text       = employee.employeeName;
        selectedRoleText.text       = employee.RoleToString();
        selectedGradeText.text      = employee.GradeToString();
        selectedDevelopText.text    = employee.DevelopText();
        selectedPlanningText.text   = employee.PlanningText();
        selectedArtText.text        = employee.ArtText();
        selectedPerfectionText.text = employee.PerfectionText();

        listPanel.SetActive(false);
        trainingPanel.SetActive(true);
    }

    public void OnClickDevelopmentTraining()
    {
        int devGain = UnityEngine.Random.Range(10, 16);
        _selectedEmployee.developSkill += devGain;
        ShowResult(devGain, 0, 0, 0);
    }

    public void OnClickAllTraining()
    {
        int devGain        = UnityEngine.Random.Range(3, 6);
        int planningGain   = UnityEngine.Random.Range(3, 6);
        int artGain        = UnityEngine.Random.Range(3, 6);
        int perfectionGain = UnityEngine.Random.Range(3, 6);

        _selectedEmployee.developSkill    += devGain;
        _selectedEmployee.planningSkill   += planningGain;
        _selectedEmployee.artSkill        += artGain;
        _selectedEmployee.perfectionSkill += perfectionGain;

        ShowResult(devGain, planningGain, artGain, perfectionGain);
    }

    void ShowResult(int devGain, int planningGain, int artGain, int perfectionGain)
    {
        EmployeeManager.Instance.UpdateEmployee(_selectedEmployee);

        resultNameText.text  = _selectedEmployee.employeeName;
        resultRoleText.text  = _selectedEmployee.RoleToString();
        resultGradeText.text = _selectedEmployee.GradeToString();

        int beforeDev        = _selectedEmployee.developSkill    - devGain;
        int beforePlanning   = _selectedEmployee.planningSkill   - planningGain;
        int beforeArt        = _selectedEmployee.artSkill        - artGain;
        int beforePerfection = _selectedEmployee.perfectionSkill - perfectionGain;

        resultDevelopText.text = devGain > 0
            ? $"개발: {beforeDev} +{devGain}"
            : $"개발: {beforeDev}";

        resultPlanningText.text = planningGain > 0
            ? $"기획: {beforePlanning} +{planningGain}"
            : $"기획: {beforePlanning}";

        resultArtText.text = artGain > 0
            ? $"아트: {beforeArt} +{artGain}"
            : $"아트: {beforeArt}";

        resultPerfectionText.text = perfectionGain > 0
            ? $"완성도: {beforePerfection} +{perfectionGain}"
            : $"완성도: {beforePerfection}";

        trainingPanel.SetActive(false);
        resultPanel.SetActive(true);

        StartCoroutine(UpdateResultAfterDelay(devGain, planningGain, artGain, perfectionGain, 2f));
    }

    IEnumerator UpdateResultAfterDelay(int devGain, int planningGain, int artGain, int perfectionGain, float delay)
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

        resultPerfectionText.text = perfectionGain > 0
            ? $"완성도: {_selectedEmployee.perfectionSkill} +{perfectionGain}"
            : $"완성도: {_selectedEmployee.perfectionSkill}";
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