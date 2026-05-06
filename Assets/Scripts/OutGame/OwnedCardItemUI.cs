using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 보유 카드 1장 (employeeId + grade)을 표시하는 카드 UI
// 중복은 카드 GameObject를 N개 생성해서 표현 (count 표시 없음)
// 색상 팔레트는 EmployeePanelItemUI와 동일 + Unique/Legendary shimmer
public class OwnedCardItemUI : MonoBehaviour
{
    [Header("References")]
    public Image gradeBackground;
    public Image portrait;

    private static readonly Color ColorNormal = new Color(0.92f, 0.92f, 0.92f);
    private static readonly Color ColorRare   = new Color(0.75f, 0.88f, 0.95f);
    private static readonly Color ColorEpic   = new Color(0.82f, 0.75f, 0.95f);

    public string EmployeeId { get; private set; }
    public EmployeeGrade Grade { get; private set; }

    private Coroutine _shimmerCo;

    public void SetData(string empId, EmployeeGrade grade, EmployeeData masterEmp = null)
    {
        EmployeeId = empId;
        Grade      = grade;

        ApplyGradeColor(grade);

        if (portrait != null && masterEmp != null && !string.IsNullOrEmpty(masterEmp.portraitId))
        {
            var sp = Resources.Load<Sprite>($"Portraits/{masterEmp.portraitId}");
            if (sp != null) portrait.sprite = sp;
            portrait.preserveAspect = true;
        }
    }

    void OnEnable()
    {
        if (!string.IsNullOrEmpty(EmployeeId)) ApplyGradeColor(Grade);
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
            _shimmerCo = StartCoroutine(LegendaryShimmer());
            return;
        }
        if (grade == EmployeeGrade.Unique)
        {
            _shimmerCo = StartCoroutine(UniqueShimmer());
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

    IEnumerator UniqueShimmer()
    {
        Color goldA = new Color(1.0f, 0.85f, 0.30f);
        Color goldB = new Color(0.85f, 0.65f, 0.10f);
        float speed = 2.0f;
        while (true)
        {
            float t = (Mathf.Sin(Time.time * speed) + 1f) / 2f;
            gradeBackground.color = Color.Lerp(goldB, goldA, t);
            yield return null;
        }
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
