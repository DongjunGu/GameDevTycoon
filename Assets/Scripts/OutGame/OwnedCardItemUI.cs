using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 보유 카드 1장 (employeeId + grade + stage)을 표시하는 카드 UI
// 중복은 카드 GameObject를 N개 생성해서 표현 (count 표시 없음)
// stage>0이면 우상단에 "+stage" 표시 (Epic 1단계, Unique 1단계)
// 색상 팔레트는 EmployeePanelItemUI와 동일 + Unique/Legendary shimmer
public class OwnedCardItemUI : MonoBehaviour
{
    [Header("References")]
    public Image gradeBackground;
    public Image portrait;
    [Tooltip("우상단 stage 라벨 (TMP). stage>0일 때만 표시")]
    public TMP_Text stageLabel;
    public Button button;
    [Tooltip("합성 매칭 안 되는 카드 dim 오버레이 (검정 알파 50%)")]
    public GameObject dimOverlay;

    private static readonly Color ColorNormal = new Color(0.92f, 0.92f, 0.92f);
    private static readonly Color ColorRare   = new Color(0.75f, 0.88f, 0.95f);
    private static readonly Color ColorEpic   = new Color(0.55f, 0.30f, 0.85f);

    public string EmployeeId { get; private set; }
    public EmployeeGrade Grade { get; private set; }
    public int Stage { get; private set; }

    public event Action<OwnedCardItemUI> OnClicked;

    private Coroutine _shimmerCo;

    void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (stageLabel == null) stageLabel = GetComponentInChildren<TMP_Text>(true);
        if (dimOverlay == null) { var t = transform.Find("DimOverlay"); if (t != null) dimOverlay = t.gameObject; }
        if (dimOverlay != null) dimOverlay.SetActive(false);
    }

    public void SetDimmed(bool dim)
    {
        if (dimOverlay != null) dimOverlay.SetActive(dim);
        if (button != null) button.interactable = !dim;
    }

    public void SetData(string empId, EmployeeGrade grade, int stage, EmployeeData masterEmp = null)
    {
        EmployeeId = empId;
        Grade      = grade;
        Stage      = stage;

        ApplyGradeColor(grade);

        if (portrait != null && masterEmp != null && !string.IsNullOrEmpty(masterEmp.portraitId))
        {
            var sp = Resources.Load<Sprite>($"Portraits/{masterEmp.portraitId}");
            if (sp != null) portrait.sprite = sp;
            portrait.preserveAspect = true;
        }

        if (stageLabel != null)
        {
            if (stage > 0) { stageLabel.text = $"+{stage}"; stageLabel.gameObject.SetActive(true); }
            else stageLabel.gameObject.SetActive(false);
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnClicked?.Invoke(this));
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
            if (isActiveAndEnabled) _shimmerCo = StartCoroutine(LegendaryShimmer());
            else gradeBackground.color = Color.HSVToRGB(0f, 0.55f, 1f);
            return;
        }
        if (grade == EmployeeGrade.Unique)
        {
            if (isActiveAndEnabled) _shimmerCo = StartCoroutine(UniqueShimmer());
            else gradeBackground.color = new Color(1.0f, 0.85f, 0.30f);
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
