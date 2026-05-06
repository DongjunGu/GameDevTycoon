using TMPro;
using UnityEngine;
using UnityEngine.UI;

// EmployeePanel의 직원 정보 패널
// 직원 클릭 시 이름 표시 + maxGrade 이하 슬롯은 원래 색, 이상은 lockColor로 덮음
public class EmployeePanelDescriptionUI : MonoBehaviour
{
    [Header("Name")]
    public TMP_Text nameText;

    [Header("Grade Slots — 인스펙터에서 Normal, Rare, Epic, Unique, Legendary 순서로 등록")]
    public Image[] gradeSlots;

    [Header("Lock")]
    public Color lockColor = new Color(0f, 0f, 0f, 1f);

    private Color[] _originalColors;

    void Awake() { CacheOriginals(); }

    public void ApplyEmployee(EmployeeData emp)
    {
        if (emp == null) return;
        if (nameText != null) nameText.text = emp.employeeName;
        ApplyMaxGrade(emp.maxGrade);
    }

    void CacheOriginals()
    {
        if (gradeSlots == null) { _originalColors = null; return; }
        _originalColors = new Color[gradeSlots.Length];
        for (int i = 0; i < gradeSlots.Length; i++)
            if (gradeSlots[i] != null) _originalColors[i] = gradeSlots[i].color;
    }

    public void ApplyMaxGrade(EmployeeGrade maxGrade)
    {
        if (gradeSlots == null) return;
        if (_originalColors == null || _originalColors.Length != gradeSlots.Length) CacheOriginals();

        int max = (int)maxGrade;
        for (int i = 0; i < gradeSlots.Length; i++)
        {
            if (gradeSlots[i] == null) continue;
            gradeSlots[i].color = (i <= max && i < _originalColors.Length)
                ? _originalColors[i]
                : lockColor;
        }
    }

    public void ResetAll()
    {
        if (gradeSlots == null || _originalColors == null) return;
        for (int i = 0; i < gradeSlots.Length; i++)
            if (gradeSlots[i] != null && i < _originalColors.Length)
                gradeSlots[i].color = _originalColors[i];
    }
}
