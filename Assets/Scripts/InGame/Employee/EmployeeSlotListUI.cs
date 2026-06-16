using UnityEngine;
using UnityEngine.UI;
using TMPro;

// EmployeeListUI 우측 목록용 슬롯 (TrainingEmployeeSlotUI 와 거의 동일).
// 구조: employeePortraitImage(컨테이너) ─ bgImage(등급 색) + portraitImage(초상화), nameText, selectButton.
// 클릭 시 onSelect 콜백으로 직원 전달. 파견중이면 dim + badge (단, 선택은 허용 — 좌측 상세 보기용,
// 강화/아이템/해고는 EmployeeListUI 버튼에서 AlertUI 로 차단).
public class EmployeeSlotListUI : MonoBehaviour
{
    [Header("UI")]
    public Image employeePortraitImage; // 초상화 컨테이너 (자식 bgImage/portraitImage 의 탐색 기준)
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI levelText;          // 강화 레벨 "Lv.{}"
    public Slider satisfactionSlider;          // 만족도 (0~100, 구간별 Fill sprite)
    public SatisfactionFillSet satisfactionFillSet; // 구간별 Fill sprite 묶음 (공용 에셋)
    public Button selectButton;
    [Header("파견중 표시 (옵션)")]
    public GameObject dispatchedBadge;

    [Header("선택 — 비우면 employeePortraitImage 자식에서 자동 탐색")]
    public Image portraitImage; // 초상화 sprite 대상 (앞)
    public Image bgImage;       // 등급 색 대상 (뒤)

    private Coroutine _gradeCo;

    public void Setup(EmployeeData data, System.Action<EmployeeData> onSelect)
    {
        if (nameText  != null) nameText.text  = data.employeeName;
        if (levelText != null) levelText.text = $"Lv.{data.enhancementLevel}";

        if (satisfactionSlider != null)
        {
            satisfactionSlider.minValue = 0f;
            satisfactionSlider.maxValue = 100f;
            satisfactionSlider.value = data.satisfaction;
            SatisfactionFillSet.Apply(satisfactionSlider, satisfactionFillSet, data.satisfaction); // 구간별 Fill sprite
        }

        var portrait = ResolvePortrait();
        if (portrait != null && !string.IsNullOrEmpty(data.portraitId))
        {
            var sprite = Resources.Load<Sprite>($"Portraits/{data.portraitId}");
            if (sprite != null) portrait.sprite = sprite;
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelect?.Invoke(data));
        }

        // 파견중이면 희미하게 + badge (blockSelect=false — 상세 보기는 가능)
        DispatchSlotVisual.Apply(this, dispatchedBadge, data.id, blockSelect: false);

        ApplyGrade(data.grade);
    }

    void ApplyGrade(EmployeeGrade grade)
    {
        var bg = ResolveBg();
        if (_gradeCo != null) { StopCoroutine(_gradeCo); _gradeCo = null; }
        _gradeCo = EmployeeGradeColor.Apply(this, bg, grade);
    }

    // 초상화: portraitImage 필드 → 자식 "portraitImage" → 최후수단 컨테이너 자체
    Image ResolvePortrait()
    {
        if (portraitImage == null) portraitImage = FindChildImage("portraitImage");
        return portraitImage != null ? portraitImage : employeePortraitImage;
    }

    // 등급 색: bgImage 필드 → 자식 "bgImage"
    Image ResolveBg()
    {
        if (bgImage == null) bgImage = FindChildImage("bgImage");
        return bgImage;
    }

    Image FindChildImage(string childName)
    {
        if (employeePortraitImage == null) return null;
        var t = employeePortraitImage.transform.Find(childName);
        return t != null ? t.GetComponent<Image>() : null;
    }
}
