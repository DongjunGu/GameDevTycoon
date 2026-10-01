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
    [Tooltip("GradeExtraNum — Epic/Unique 합성 단계(1·2). 비우면 이름으로 자동 탐색")]
    public TMP_Text gradeExtraNum;
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

        var grade = OutGameEmployeeManager.Instance != null
            ? OutGameEmployeeManager.Instance.GetMaxGrade(emp.id)
            : EmployeeGrade.Normal;
        // 합성 단계 — Epic/Unique 에서만 s1/s2 가 존재한다
        bool stageGrade = grade == EmployeeGrade.Epic || grade == EmployeeGrade.Unique;
        int stage = (unlocked && stageGrade && OwnedCardManager.Instance != null)
            ? OwnedCardManager.Instance.GetHighestStage(emp.id, grade) : 0;
        Apply(emp, unlocked, grade, stage);
    }

    // 합성 결과 미리보기 — 보유 최고 등급 대신 지정한 등급/단계로 표시
    public void SetPreview(EmployeeData emp, EmployeeGrade grade, int stage)
    {
        Data = emp;
        IsUnlocked = true;
        if (emp == null) return;
        Apply(emp, true, grade, stage);
    }

    void Apply(EmployeeData emp, bool unlocked, EmployeeGrade grade, int stage)
    {
        _pendingGrade = grade;
        ApplyGradeFrame(grade);

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

        ApplyStage(stage);

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

    // 합성 단계 표시 — s0 은 숨김
    void ApplyStage(int stage)
    {
        if (gradeExtraNum == null)
        {
            var t = transform.Find("GradeExtraNum");
            if (t != null) gradeExtraNum = t.GetComponent<TMP_Text>();
            if (gradeExtraNum == null) return;
        }

        gradeExtraNum.text = stage.ToString();
        gradeExtraNum.gameObject.SetActive(stage > 0);
    }

    // 등급 = 프레임 스프라이트 교체. 색 틴트는 쓰지 않는다(항상 흰색)
    void ApplyGradeFrame(EmployeeGrade grade)
    {
        if (gradeBackground == null) return;
        gradeBackground.color = Color.white;
        GradeSpriteSet.Apply(gradeBackground, gradeFrameSet, grade);
    }
}
