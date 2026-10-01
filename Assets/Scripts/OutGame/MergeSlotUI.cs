using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 합성 슬롯 1개 (메인 또는 재료) — 카드 정보 + 시각화 + 클릭으로 비우기
// 슬롯 배경 Image 의 색/알파는 건드리지 않는다 (인스펙터 값 그대로). 등급은 카드 프레임 스프라이트로 표시.
public class MergeSlotUI : MonoBehaviour
{
    [Header("References")]
    public Image portrait;
    public TMP_Text stageLabel;
    [Tooltip("슬롯 클릭 시 카드를 다시 풀로 되돌리는 버튼")]
    public Button button;
    [Tooltip("빈 슬롯 placeholder (가운데 + 마크 등) — 카드 채워지면 비활성")]
    public GameObject placeholder;
    [Tooltip("카드형 표시 (자식 NewPortrait) — 있으면 카드 채울 때 켜고 SetPreview, 비우면 끔")]
    public EmployeePanelItemUI card;

    public string EmployeeId { get; private set; }
    public EmployeeGrade Grade { get; private set; }
    public int Stage { get; private set; }
    public OwnedCardItemUI Source { get; private set; }
    public bool IsEmpty => string.IsNullOrEmpty(EmployeeId);

    public event Action<MergeSlotUI> OnClicked;

    void Awake()
    {
        // 자동 매핑 (인스펙터 비어있어도 자식 이름으로 찾음)
        if (button == null) button = GetComponent<Button>();
        if (placeholder == null) { var t = transform.Find("Placeholder"); if (t != null) placeholder = t.gameObject; }
        if (card == null) { var t = transform.Find("NewPortrait"); if (t != null) card = t.GetComponent<EmployeePanelItemUI>(); }
        // 카드형 슬롯은 초상화/단계를 카드가 표시 — 옛 자식(Portrait/StageLabel)은 자동 매핑 안 함
        if (card == null)
        {
            if (portrait == null) { var t = transform.Find("Portrait"); if (t != null) portrait = t.GetComponent<Image>(); }
            if (stageLabel == null) { var t = transform.Find("StageLabel"); if (t != null) stageLabel = t.GetComponent<TMP_Text>(); }
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnClicked?.Invoke(this));
        }
        // 카드가 슬롯 위를 덮어 클릭을 먼저 받으므로 슬롯 클릭으로 전달
        if (card != null) card.OnClicked += _ => OnClicked?.Invoke(this);
        Clear();
    }

    public void SetCard(OwnedCardItemUI source, EmployeeData masterEmp)
    {
        Source     = source;
        EmployeeId = source.EmployeeId;
        Grade      = source.Grade;
        Stage      = source.Stage;
        ApplyVisual(masterEmp);
        if (source != null) source.SetSelected(true);
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
        if (card != null)
        {
            card.gameObject.SetActive(true);
            card.SetPreview(masterEmp, Grade, Stage);
        }
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
        if (Source != null) Source.SetSelected(false);
        Source     = null;
        EmployeeId = null;
        Grade      = EmployeeGrade.Normal;
        Stage      = 0;

        if (portrait != null) { portrait.sprite = null; portrait.enabled = false; }
        if (card != null) card.gameObject.SetActive(false);
        if (stageLabel != null) stageLabel.gameObject.SetActive(false);
        if (placeholder != null) placeholder.SetActive(true);
    }
}
