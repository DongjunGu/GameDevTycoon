using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 합성 슬롯 1개 (메인 또는 재료) — 카드 정보 + 시각화 + 클릭으로 비우기
public class MergeSlotUI : MonoBehaviour
{
    [Header("References")]
    public Image portrait;
    public Image gradeBackground;
    public TMP_Text stageLabel;
    [Tooltip("슬롯 클릭 시 카드를 다시 풀로 되돌리는 버튼")]
    public Button button;
    [Tooltip("빈 슬롯 placeholder (가운데 + 마크 등) — 카드 채워지면 비활성")]
    public GameObject placeholder;

    public string EmployeeId { get; private set; }
    public EmployeeGrade Grade { get; private set; }
    public int Stage { get; private set; }
    public OwnedCardItemUI Source { get; private set; }
    public bool IsEmpty => string.IsNullOrEmpty(EmployeeId);

    public event Action<MergeSlotUI> OnClicked;

    private static readonly Color ColorNormal    = new Color(0.92f, 0.92f, 0.92f);
    private static readonly Color ColorRare      = new Color(0.75f, 0.88f, 0.95f);
    private static readonly Color ColorEpic      = new Color(0.55f, 0.30f, 0.85f);
    private static readonly Color ColorUnique    = new Color(1.0f, 0.85f, 0.30f);
    private static readonly Color ColorLegendary = new Color(0.95f, 0.55f, 0.85f);
    private static readonly Color ColorEmpty     = new Color(0.25f, 0.25f, 0.25f, 0.6f);

    void Awake()
    {
        // 자동 매핑 (인스펙터 비어있어도 자식 이름으로 찾음)
        if (gradeBackground == null) gradeBackground = GetComponent<Image>();
        if (button == null) button = GetComponent<Button>();
        if (portrait == null) { var t = transform.Find("Portrait"); if (t != null) portrait = t.GetComponent<Image>(); }
        if (stageLabel == null) { var t = transform.Find("StageLabel"); if (t != null) stageLabel = t.GetComponent<TMP_Text>(); }
        if (placeholder == null) { var t = transform.Find("Placeholder"); if (t != null) placeholder = t.gameObject; }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnClicked?.Invoke(this));
        }
        Clear();
    }

    public void SetCard(OwnedCardItemUI source, EmployeeData masterEmp)
    {
        Source     = source;
        EmployeeId = source.EmployeeId;
        Grade      = source.Grade;
        Stage      = source.Stage;
        ApplyVisual(masterEmp);
        if (source != null) source.gameObject.SetActive(false);
    }

    // 결과 미리보기 등 풀 카드 source 없이 슬롯 채울 때
    public void SetPreviewCard(string empId, EmployeeGrade grade, int stage, EmployeeData masterEmp)
    {
        Source     = null;
        EmployeeId = empId;
        Grade      = grade;
        Stage      = stage;
        ApplyVisual(masterEmp);
    }

    void ApplyVisual(EmployeeData masterEmp)
    {
        if (portrait != null)
        {
            portrait.preserveAspect = true;
            portrait.enabled = true;
            if (masterEmp != null && !string.IsNullOrEmpty(masterEmp.portraitId))
            {
                var sp = Resources.Load<Sprite>($"Portraits/{masterEmp.portraitId}");
                if (sp != null) portrait.sprite = sp;
            }
        }
        if (gradeBackground != null) gradeBackground.color = GradeColor(Grade);
        if (stageLabel != null)
        {
            bool show = Stage > 0;
            stageLabel.gameObject.SetActive(show);
            if (show) stageLabel.text = $"+{Stage}";
        }
        if (placeholder != null) placeholder.SetActive(false);
    }

    public void Clear()
    {
        if (Source != null) Source.gameObject.SetActive(true);
        Source     = null;
        EmployeeId = null;
        Grade      = EmployeeGrade.Normal;
        Stage      = 0;

        if (portrait != null) { portrait.sprite = null; portrait.enabled = false; }
        if (gradeBackground != null) gradeBackground.color = ColorEmpty;
        if (stageLabel != null) stageLabel.gameObject.SetActive(false);
        if (placeholder != null) placeholder.SetActive(true);
    }

    static Color GradeColor(EmployeeGrade g) => g switch
    {
        EmployeeGrade.Normal    => ColorNormal,
        EmployeeGrade.Rare      => ColorRare,
        EmployeeGrade.Epic      => ColorEpic,
        EmployeeGrade.Unique    => ColorUnique,
        EmployeeGrade.Legendary => ColorLegendary,
        _                       => ColorNormal
    };
}
