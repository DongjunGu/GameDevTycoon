using TMPro;
using UnityEngine;
using UnityEngine.UI;

// EmployeePanel의 직원 정보 패널
// 직원 클릭 시 이름 표시 + 등급 슬롯 잠금/해제
// - unlocked=false: 모든 슬롯이 lockColor + 자식 텍스트 lockTextColor
// - unlocked=true : maxGrade 이하 원래 색, 위는 lockColor + 자식 텍스트 lockTextColor
public class EmployeePanelDescriptionUI : MonoBehaviour
{
    [Header("Name")]
    public TMP_Text nameText;

    [Header("Grade Slots — 인스펙터에서 Normal, Rare, Epic, Unique, Legendary 순서로 등록")]
    public Image[] gradeSlots;

    [Header("Lock")]
    public Color lockColor     = new Color(0f, 0f, 0f, 1f);
    public Color lockTextColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    private Color[]   _originalSlotColors;
    private Color[][] _originalTextColors; // [slotIndex][textIndex]

    void Awake() { CacheOriginals(); }

    public void ApplyEmployee(EmployeeData emp, bool unlocked)
    {
        if (emp == null) return;
        if (nameText != null) nameText.text = emp.employeeName;
        ApplyLockState(unlocked, emp.maxGrade);
    }

    void CacheOriginals()
    {
        if (gradeSlots == null) { _originalSlotColors = null; _originalTextColors = null; return; }
        _originalSlotColors = new Color[gradeSlots.Length];
        _originalTextColors = new Color[gradeSlots.Length][];
        for (int i = 0; i < gradeSlots.Length; i++)
        {
            if (gradeSlots[i] == null) continue;
            _originalSlotColors[i] = gradeSlots[i].color;
            var texts = gradeSlots[i].GetComponentsInChildren<TMP_Text>(true);
            _originalTextColors[i] = new Color[texts.Length];
            for (int j = 0; j < texts.Length; j++) _originalTextColors[i][j] = texts[j].color;
        }
    }

    void ApplyLockState(bool unlocked, EmployeeGrade maxGrade)
    {
        if (gradeSlots == null) return;
        if (_originalSlotColors == null || _originalSlotColors.Length != gradeSlots.Length) CacheOriginals();

        int max = (int)maxGrade;
        for (int i = 0; i < gradeSlots.Length; i++)
        {
            // unlocked=false면 전부 잠금. unlocked=true면 maxGrade 위만 잠금.
            bool locked = !unlocked || i > max;
            ApplySlot(i, locked);
        }
    }

    void ApplySlot(int i, bool locked)
    {
        if (gradeSlots[i] == null) return;
        gradeSlots[i].color = locked ? lockColor
            : (_originalSlotColors != null && i < _originalSlotColors.Length
                ? _originalSlotColors[i] : Color.white);

        var texts = gradeSlots[i].GetComponentsInChildren<TMP_Text>(true);
        var origs = (_originalTextColors != null && i < _originalTextColors.Length)
            ? _originalTextColors[i] : null;
        for (int j = 0; j < texts.Length; j++)
        {
            texts[j].color = locked ? lockTextColor
                : (origs != null && j < origs.Length ? origs[j] : Color.white);
        }
    }

    // 외부에서 등급 잠금만 적용하고 싶을 때 (해금 직원 가정)
    public void ApplyMaxGrade(EmployeeGrade maxGrade) => ApplyLockState(true, maxGrade);
}
