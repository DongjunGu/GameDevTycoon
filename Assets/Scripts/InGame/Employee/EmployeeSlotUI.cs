using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EmployeeSlotUI : MonoBehaviour
{
    [Header("UI")]
    public Image bgImage;          // 등급 배경색
    public Image portraitImage;    // 초상화
    public TextMeshProUGUI nameText;
    public Button selectButton;    // 슬롯 선택(채용 후보 클릭)
    public GameObject ownedBadge;  // 보유중 뱃지 이미지
    public GameObject dispatchedBadge; // 파견중 뱃지 (해고 리스트 등 — 후보엔 항상 비활성)

    [Header("등급 테두리 (공용 GradeSpriteSet 에셋)")]
    public Image gradeBorder;
    public GradeSpriteSet gradeBorderSet;

    [Header("역할 아이콘 (공용 RoleIconSet 에셋)")]
    public Image roleIcon;
    public RoleIconSet roleIconSet;
    private static readonly Color ColorNormal = new Color(0.92f, 0.92f, 0.92f);
    private static readonly Color ColorRare = new Color(0.75f, 0.88f, 0.95f);
    private static readonly Color ColorEpic = new Color(0.55f, 0.30f, 0.85f);
    private EmployeeGrade _pendingGrade;

    // onSelect: 슬롯 클릭 시 콜백 (채용=HiringUI.OnSelectEmployee, 해고=EmployeeListUI.OnSelectEmployee 등 공용)
    public void Setup(EmployeeData data, System.Action<EmployeeData> onSelect)
    {
        // 표시: 초상화 / 등급 배경색 / 이름 / 보유중 뱃지 만
        if (nameText != null) nameText.text = data.employeeName;
        if (portraitImage != null && !string.IsNullOrEmpty(data.portraitId))
        {
            var sprite = Resources.Load<Sprite>($"Portraits/Mini/{data.portraitId}");
            if (sprite != null) portraitImage.sprite = sprite;
        }
        _pendingGrade = data.grade;

        // 등급 테두리 / 역할 아이콘 (공용 SO — EmployeeSatisfactionSlider 와 동일 방식)
        EnsureVisualRefs();
        GradeSpriteSet.Apply(gradeBorder, gradeBorderSet, data.grade);
        RoleIconSet.Apply(roleIcon, roleIconSet, data.role);

        // 보유중 뱃지 (masterEmployeeId 공백이면 이름으로 대조)
        bool isOwned = EmployeeManager.Instance.ownedEmployees
            .Exists(e => IsSameEmployee(e, data));
        if (ownedBadge != null) ownedBadge.SetActive(isOwned);

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => onSelect?.Invoke(data));

        // 파견중(보유 직원 리스트)이면 희미하게 + badge + 선택 차단. 채용 후보는 파견중일 수 없어 무영향.
        DispatchSlotVisual.Apply(this, dispatchedBadge, data.id, blockSelect: true);

        // 새로고침 등 이미 활성 상태에서 재생성될 때는 OnEnable 이 Setup 보다 먼저 실행되어
        // _pendingGrade 가 기본값일 때 색이 칠해짐 → 활성 상태면 여기서 다시 적용.
        // 비활성이면 코루틴 에러 방지 위해 건너뛰고 OnEnable 에 위임.
        if (isActiveAndEnabled) ApplyGradeColor(_pendingGrade);
    }
    void OnEnable()
    {
        ApplyGradeColor(_pendingGrade); // 활성화될 때 실행
    }

    // 인스펙터 미할당 시 자식 이름으로 자동 탐색 (EmployeeSatisfactionSlider 와 동일: "ImagePanel" 테두리 / "roleIcon").
    void EnsureVisualRefs()
    {
        if (gradeBorder == null) gradeBorder = FindChildImage("ImagePanel");
        if (roleIcon    == null) roleIcon    = FindChildImage("roleIcon");
    }

    Image FindChildImage(string childName)
    {
        var t = transform.Find(childName);
        return t != null ? t.GetComponent<Image>() : null;
    }

    void ApplyGradeColor(EmployeeGrade grade)
    {
        if (bgImage == null) return;

        StopAllCoroutines();

        if (grade == EmployeeGrade.Legendary)
        {
            StartCoroutine(LegendaryShimmer());
            return;
        }

        if (grade == EmployeeGrade.Unique)
        {
            bgImage.color = new Color(1f, 0.85f, 0.30f);
            return;
        }

        bgImage.color = grade switch
        {
            EmployeeGrade.Normal => ColorNormal,
            EmployeeGrade.Rare => ColorRare,
            EmployeeGrade.Epic => ColorEpic,
            _ => ColorNormal
        };
    }

    // masterEmployeeId가 없는 구형 데이터는 이름으로 대조
    public static bool IsSameEmployee(EmployeeData owned, EmployeeData candidate)
    {
        if (!string.IsNullOrEmpty(owned.masterEmployeeId))
            return owned.masterEmployeeId == candidate.id;
        return owned.employeeName == candidate.employeeName;
    }

    System.Collections.IEnumerator LegendaryShimmer()
    {
        // 무지개 HSV cycling — Unique보다 화려
        while (true)
        {
            float h = Mathf.Repeat(Time.time * 0.25f, 1f);
            bgImage.color = Color.HSVToRGB(h, 0.55f, 1f);
            yield return null;
        }
    }
}