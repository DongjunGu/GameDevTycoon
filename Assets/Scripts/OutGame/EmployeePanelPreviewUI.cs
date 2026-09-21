using TMPro;
using UnityEngine;
using UnityEngine.UI;

// EmployeePanel/MiddlePanel/PreviewContainer2 — 선택된 직원에 맞춰 프리뷰 비주얼을 교체한다.
// 갤러리에서 직원 카드를 누르면 OutGameEmployeePanelGallery 가 ApplyEmployee 를 호출한다.
//
// 스프라이트는 전부 Resources 규약 로드 (인스펙터 등록 불필요):
//   PCBigImage   ← Resources/Portraits/{portraitId}        (대형 일러스트)
//   PCPixelImage ← Resources/Portraits/Pixel/{portraitId}  (도트 idle)
//   GradePanel/Icon ← Resources/Images/Job_{Plan|Dev|Art}_L (직군 아이콘)
//
// 자식 참조는 인스펙터 미등록 시 이름으로 자동 탐색한다.
// 하이어라키 이름을 바꾸면 조용히 null 이 되므로, 이름을 바꿀 거면 인스펙터에 직접 등록할 것.
public class EmployeePanelPreviewUI : MonoBehaviour
{
    [Header("References — 비우면 이름으로 자동 탐색")]
    public Image    bigImage;   // PCBigImage
    public Image    pixelImage; // PCPixelImage
    public TMP_Text gradeText;  // EmployeeInfoPanel/GradePanel/GPGrade
    public Image    roleIcon;   // EmployeeInfoPanel/GradePanel/GPIcon
    public TMP_Text nameText;   // EmployeeInfoPanel/GPNameText

    [Header("Locked")]
    public Color lockedTint     = new Color(0f, 0f, 0f, 0.6f);
    public Color lockedTextColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    // EmployeeGradeColor 와 같은 팔레트 (Unique/Legendary 는 거기서 private 이라 여기 재정의)
    static readonly Color GradeUnique    = new Color(1.00f, 0.85f, 0.30f);
    static readonly Color GradeLegendary = new Color(0.95f, 0.55f, 0.85f);

    bool _resolved;

    void Awake() => EnsureReferences();

    void EnsureReferences()
    {
        if (_resolved) return;
        _resolved = true;

        if (bigImage   == null) bigImage   = FindImage("PCBigImage");
        if (pixelImage == null) pixelImage = FindImage("PCPixelImage");

        var info = transform.Find("EmployeeInfoPanel");
        if (info == null) return;
        var gradePanel = info.Find("GradePanel");
        if (gradeText == null && gradePanel != null)
        {
            var t = gradePanel.Find("GPGrade");
            if (t != null) gradeText = t.GetComponent<TMP_Text>();
            if (gradeText == null) gradeText = gradePanel.GetComponentInChildren<TMP_Text>(true);
        }
        if (roleIcon == null && gradePanel != null)
        {
            var t = gradePanel.Find("GPIcon");
            if (t != null) roleIcon = t.GetComponent<Image>();
        }
        if (nameText == null)
        {
            var t = info.Find("GPNameText");
            if (t != null) nameText = t.GetComponent<TMP_Text>();
        }
    }

    Image FindImage(string childName)
    {
        var t = transform.Find(childName);
        return t != null ? t.GetComponent<Image>() : null;
    }

    public void ApplyEmployee(EmployeeData emp, bool unlocked)
    {
        if (emp == null) return;
        EnsureReferences();

        var grade = OutGameEmployeeManager.Instance != null
            ? OutGameEmployeeManager.Instance.GetMaxGrade(emp.id)
            : EmployeeGrade.Normal;

        ApplySprite(bigImage,   $"Portraits/{emp.portraitId}",       unlocked);
        ApplySprite(pixelImage, $"Portraits/Pixel/{emp.portraitId}", unlocked);

        if (roleIcon != null)
        {
            var sp = Resources.Load<Sprite>($"Images/{RoleIconName(emp.role)}");
            if (sp != null) roleIcon.sprite = sp;
            roleIcon.color = unlocked ? Color.white : lockedTint;
        }

        if (gradeText != null)
        {
            gradeText.text  = grade.ToString().ToUpperInvariant();
            gradeText.color = unlocked ? GradeColor(grade) : lockedTextColor;
        }

        if (nameText != null)
        {
            nameText.text  = emp.employeeName;
            nameText.color = unlocked ? Color.white : lockedTextColor;
        }
    }

    // 스프라이트가 없으면 기존 것을 유지한다 (빈 사각형으로 깜빡이는 것 방지)
    void ApplySprite(Image img, string resourcePath, bool unlocked)
    {
        if (img == null) return;
        var sp = Resources.Load<Sprite>(resourcePath);
        if (sp != null) img.sprite = sp;
        img.color = unlocked ? Color.white : lockedTint;
    }

    static string RoleIconName(EmployeeRole role) => role switch
    {
        EmployeeRole.Programmer => "Job_Dev_L",
        EmployeeRole.Artist     => "Job_Art_L",
        _                       => "Job_Plan_L",
    };

    static Color GradeColor(EmployeeGrade grade) => grade switch
    {
        EmployeeGrade.Rare      => EmployeeGradeColor.Rare,
        EmployeeGrade.Epic      => EmployeeGradeColor.Epic,
        EmployeeGrade.Unique    => GradeUnique,
        EmployeeGrade.Legendary => GradeLegendary,
        _                       => EmployeeGradeColor.Normal,
    };
}
