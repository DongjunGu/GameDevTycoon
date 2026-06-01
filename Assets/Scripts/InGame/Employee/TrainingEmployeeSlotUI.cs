using UnityEngine;
using UnityEngine.UI;
using TMPro;

// TrainingPanelUI 우측 직원 목록용 간단 슬롯 (EmployeeSlotPrefab3).
// 구조: employeePortraitImage(컨테이너) ─ bgImage(등급 색) + portraitImage(초상화), nameText, selectButton.
// 클릭 시 onSelect 콜백으로 해당 직원 전달.
public class TrainingEmployeeSlotUI : MonoBehaviour
{
    [Header("UI")]
    public Image employeePortraitImage; // 초상화 컨테이너 (자식 bgImage/portraitImage 의 탐색 기준)
    public TextMeshProUGUI nameText;
    public Button selectButton;

    [Header("선택 — 비우면 employeePortraitImage 자식에서 자동 탐색")]
    public Image portraitImage; // 초상화 sprite 대상 (앞)
    public Image bgImage;       // 등급 색 대상 (뒤)

    private Coroutine _gradeCo;

    public void Setup(EmployeeData data, System.Action<EmployeeData> onSelect)
    {
        if (nameText != null) nameText.text = data.employeeName;

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
