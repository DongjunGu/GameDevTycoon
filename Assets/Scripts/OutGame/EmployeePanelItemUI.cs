using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 아웃게임 EmployeePanel 카드 UI — ItemPrefab에 부착
// SetData()로 직군 아이콘/이름/등급 배경/초상화 세팅
// 등급 색상은 OwnedEmployeeSlotUI와 동일 팔레트 (Unique는 황금 shimmer)
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

    [Header("Locked Visual")]
    public Color lockedTint = new Color(0f, 0f, 0f, 0.6f);

    // OwnedEmployeeSlotUI와 동일 팔레트
    private static readonly Color ColorNormal = new Color(0.92f, 0.92f, 0.92f);
    private static readonly Color ColorRare   = new Color(0.75f, 0.88f, 0.95f);
    private static readonly Color ColorEpic   = new Color(0.55f, 0.30f, 0.85f);

    public EmployeeData Data { get; private set; }
    public bool IsUnlocked { get; private set; }

    public event Action<EmployeePanelItemUI> OnClicked;

    private EmployeeGrade _pendingGrade;
    private Coroutine _shimmerCo;

    public void SetData(EmployeeData emp, bool unlocked)
    {
        Data = emp;
        IsUnlocked = unlocked;
        if (emp == null) return;

        _pendingGrade = OutGameEmployeeManager.Instance != null
            ? OutGameEmployeeManager.Instance.GetMaxGrade(emp.id)
            : EmployeeGrade.Normal;
        ApplyGradeColor(_pendingGrade);

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

    void OnEnable()
    {
        if (Data != null) ApplyGradeColor(_pendingGrade);
    }

    void OnDisable()
    {
        if (_shimmerCo != null) { StopCoroutine(_shimmerCo); _shimmerCo = null; }
    }

    void ApplyGradeColor(EmployeeGrade grade)
    {
        if (gradeBackground == null) return;
        if (_shimmerCo != null) { StopCoroutine(_shimmerCo); _shimmerCo = null; }

        if (grade == EmployeeGrade.Legendary)
        {
            if (isActiveAndEnabled) _shimmerCo = StartCoroutine(LegendaryShimmer());
            else gradeBackground.color = Color.HSVToRGB(0f, 0.55f, 1f);
            return;
        }

        if (grade == EmployeeGrade.Unique)
        {
            gradeBackground.color = new Color(1.0f, 0.85f, 0.30f); // 금색 단색 (반짝임 제거)
            return;
        }

        gradeBackground.color = grade switch
        {
            EmployeeGrade.Normal => ColorNormal,
            EmployeeGrade.Rare   => ColorRare,
            EmployeeGrade.Epic   => ColorEpic,
            _                    => ColorNormal
        };
    }

    IEnumerator LegendaryShimmer()
    {
        while (true)
        {
            float h = Mathf.Repeat(Time.time * 0.25f, 1f);
            gradeBackground.color = Color.HSVToRGB(h, 0.55f, 1f);
            yield return null;
        }
    }
}
