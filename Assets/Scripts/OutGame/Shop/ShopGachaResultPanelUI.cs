using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 가챠 결과 패널 — 뽑은 1장(직원 카드 or 특성) 을 카드 형태로 표시.
// 아이콘 sprite 는 인스펙터에서 직원/특성용 별도 매핑 안 함 (TODO) — 일단 이름/등급 텍스트 위주.
// 특성은 등급 색을 cardBackground 에 칠해 시각 구분.
public class ShopGachaResultPanelUI : MonoBehaviour
{
    [Header("Card")]
    public Image cardBackground;
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text gradeText;

    [Header("Close")]
    public Button closeButton;

    [Header("Default Card Color (직원 카챠 시 사용)")]
    public Color employeeCardColor = new Color(0.2f, 0.22f, 0.3f, 1f);

    void OnEnable()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
    }

    public void Close() => gameObject.SetActive(false);

    public void ShowEmployee(EmployeeData emp, EmployeeGrade grade)
    {
        if (emp == null) return;
        if (cardBackground != null) cardBackground.color = employeeCardColor;
        if (iconImage != null)
        {
            // Portrait sprite — Resources/Portraits/{portraitId}.png. OwnedCardItemUI 와 동일 패턴.
            Sprite sp = null;
            if (!string.IsNullOrEmpty(emp.portraitId))
                sp = Resources.Load<Sprite>($"Portraits/{emp.portraitId}");
            iconImage.sprite = sp;
            iconImage.enabled = (sp != null);
            iconImage.preserveAspect = true;
        }
        if (nameText != null)  nameText.text  = emp.employeeName;
        if (gradeText != null) gradeText.text = grade.ToString();
        gameObject.SetActive(true);
    }

    public void ShowTrait(TraitChartRow row)
    {
        if (row == null) return;
        if (cardBackground != null) cardBackground.color = TraitGradeColors.Get(row.grade);
        if (iconImage != null) iconImage.enabled = false; // 특성 아이콘 — TODO
        if (nameText != null)  nameText.text  = row.name;
        if (gradeText != null) gradeText.text = row.grade.ToString();
        gameObject.SetActive(true);
    }
}
