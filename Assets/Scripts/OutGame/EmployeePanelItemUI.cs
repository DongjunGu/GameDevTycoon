using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 아웃게임 EmployeePanel 카드 UI — ItemPrefab에 부착
// SetData()로 직군 아이콘/이름/등급 프레임/초상화 세팅
// 등급 구분은 색 틴트가 아니라 GradeSpriteSet(SO)의 프레임 스프라이트 교체로 한다
public class EmployeePanelItemUI : MonoBehaviour
{
    [Header("References")]
    public Image gradeBackground;
    public Image portrait;
    public Image jobIcon;
    public TMP_Text nameText;
    public Transform traitContainer; // 추후 특성 슬롯 영역
    public Button button;

    [Header("Sprite Tables (인스펙터 등록)")]
    [Tooltip("인덱스 = (int)EmployeeRole — Planner(0)/Programmer(1)/Artist(2)")]
    public Sprite[] roleIcons;

    [Header("Grade Frame (SO)")]
    [Tooltip("등급별 프레임 스프라이트 SO. 기본 에셋: Assets/ScriptableObject/OutGameEmployeeFrameSet.asset (Employee_Frame_*)")]
    public GradeSpriteSet gradeFrameSet;

    [Header("Locked Visual")]
    public Color lockedTint = new Color(0f, 0f, 0f, 0.6f);

    public EmployeeData Data { get; private set; }
    public bool IsUnlocked { get; private set; }

    public event Action<EmployeePanelItemUI> OnClicked;

    private EmployeeGrade _pendingGrade;

    public void SetData(EmployeeData emp, bool unlocked)
    {
        Data = emp;
        IsUnlocked = unlocked;
        if (emp == null) return;

        _pendingGrade = OutGameEmployeeManager.Instance != null
            ? OutGameEmployeeManager.Instance.GetMaxGrade(emp.id)
            : EmployeeGrade.Normal;
        ApplyGradeFrame(_pendingGrade);

        if (portrait != null)
        {
            portrait.preserveAspect = true;
            if (!string.IsNullOrEmpty(emp.portraitId))
            {
                var sp = Resources.Load<Sprite>($"Portraits/{emp.portraitId}");
                if (sp != null) portrait.sprite = sp;
            }
            portrait.color = unlocked ? Color.white : lockedTint;
        }

        if (jobIcon != null && roleIcons != null
            && (int)emp.role >= 0 && (int)emp.role < roleIcons.Length
            && roleIcons[(int)emp.role] != null)
        {
            jobIcon.sprite = roleIcons[(int)emp.role];
            jobIcon.enabled = true;
        }

        if (nameText != null) nameText.text = emp.employeeName;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnClicked?.Invoke(this));
        }
    }

    // 비활성 상태에서 SetData 를 받았을 수 있어 켜질 때 다시 적용
    void OnEnable()
    {
        if (Data != null) ApplyGradeFrame(_pendingGrade);
    }

    // 등급 = 프레임 스프라이트 교체. 색 틴트는 쓰지 않는다(항상 흰색)
    void ApplyGradeFrame(EmployeeGrade grade)
    {
        if (gradeBackground == null) return;
        gradeBackground.color = Color.white;
        GradeSpriteSet.Apply(gradeBackground, gradeFrameSet, grade);
    }
}
