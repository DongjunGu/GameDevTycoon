using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// EmployeePanel의 직원 정보 패널
// 직원 클릭 시 이름 표시 + 등급 슬롯 잠금/해제 + ContainerBG 등급 색
// - unlocked=false: 모든 슬롯이 lockColor + 자식 텍스트 lockTextColor
// - unlocked=true : OutGameEmployeeManager가 보유한 도달 maxGrade까지 색, 위는 lockColor
public class EmployeePanelDescriptionUI : MonoBehaviour
{
    [Header("Name")]
    public TMP_Text nameText;

    [Header("Grade Slots — 인스펙터에서 Normal, Rare, Epic, Unique, Legendary 순서로 등록")]
    public Image[] gradeSlots;

    [Header("ContainerBG (등급 배경)")]
    [Tooltip("PreviewContainer/ContainerBG의 Image. 도달 maxGrade 색 적용. Unique=황금 shimmer, Legendary=HSV cycle")]
    public Image containerBG;

    [Header("Lock")]
    public Color lockColor     = new Color(0f, 0f, 0f, 1f);
    public Color lockTextColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    // EmployeePanelItemUI와 동일 팔레트 — 슬롯과 ContainerBG 공통
    private static readonly Color BgNormal    = new Color(0.92f, 0.92f, 0.92f);
    private static readonly Color BgRare      = new Color(0.75f, 0.88f, 0.95f);
    private static readonly Color BgEpic      = new Color(0.55f, 0.30f, 0.85f);
    private static readonly Color BgUnique    = new Color(1.0f, 0.85f, 0.30f);
    private static readonly Color BgLegendary = new Color(0.95f, 0.55f, 0.85f);

    private Color[][] _originalTextColors; // [slotIndex][textIndex]
    private Coroutine _bgShimmerCo;
    private EmployeeData _currentEmp;
    private bool _currentUnlocked;

    void Awake() { CacheTextOriginals(); }

    void OnEnable()
    {
        if (OutGameEmployeeManager.Instance != null)
            OutGameEmployeeManager.Instance.OnChanged += HandleMaxGradeChanged;
        // 패널 비활성 동안 maxGrade가 갱신됐을 수 있으므로 강제 재적용
        if (_currentEmp != null) ApplyEmployee(_currentEmp, _currentUnlocked);
    }

    void OnDisable()
    {
        if (OutGameEmployeeManager.Instance != null)
            OutGameEmployeeManager.Instance.OnChanged -= HandleMaxGradeChanged;
        if (_bgShimmerCo != null) { StopCoroutine(_bgShimmerCo); _bgShimmerCo = null; }
    }

    void HandleMaxGradeChanged()
    {
        if (_currentEmp != null) ApplyEmployee(_currentEmp, _currentUnlocked);
    }

    public void ApplyEmployee(EmployeeData emp, bool unlocked)
    {
        if (emp == null) return;
        _currentEmp = emp;
        _currentUnlocked = unlocked;
        if (nameText != null) nameText.text = emp.employeeName;
        var current = OutGameEmployeeManager.Instance != null
            ? OutGameEmployeeManager.Instance.GetMaxGrade(emp.id)
            : EmployeeGrade.Normal;
        ApplyLockState(unlocked, current);
        ApplyContainerBg(current);
    }

    void CacheTextOriginals()
    {
        if (gradeSlots == null) { _originalTextColors = null; return; }
        _originalTextColors = new Color[gradeSlots.Length][];
        for (int i = 0; i < gradeSlots.Length; i++)
        {
            if (gradeSlots[i] == null) continue;
            var texts = gradeSlots[i].GetComponentsInChildren<TMP_Text>(true);
            _originalTextColors[i] = new Color[texts.Length];
            for (int j = 0; j < texts.Length; j++) _originalTextColors[i][j] = texts[j].color;
        }
    }

    void ApplyLockState(bool unlocked, EmployeeGrade maxGrade)
    {
        if (gradeSlots == null) return;
        if (_originalTextColors == null || _originalTextColors.Length != gradeSlots.Length) CacheTextOriginals();

        int max = (int)maxGrade;
        for (int i = 0; i < gradeSlots.Length; i++)
        {
            // unlocked=false면 전부 잠금. unlocked=true면 maxGrade 위만 잠금.
            bool locked = !unlocked || i > max;
            ApplySlot(i, locked);
        }
    }

    // 슬롯 색은 인덱스 기반 등급 팔레트 (CacheOriginals 의존 제거)
    static Color SlotColorByIndex(int i) => i switch
    {
        0 => BgNormal,
        1 => BgRare,
        2 => BgEpic,
        3 => BgUnique,
        4 => BgLegendary,
        _ => Color.white
    };

    void ApplySlot(int i, bool locked)
    {
        if (gradeSlots[i] == null) return;
        gradeSlots[i].color = locked ? lockColor : SlotColorByIndex(i);

        var texts = gradeSlots[i].GetComponentsInChildren<TMP_Text>(true);
        var origs = (_originalTextColors != null && i < _originalTextColors.Length)
            ? _originalTextColors[i] : null;
        for (int j = 0; j < texts.Length; j++)
        {
            texts[j].color = locked ? lockTextColor
                : (origs != null && j < origs.Length ? origs[j] : Color.white);
        }
    }

    // 외부에서 등급 잠금만 적용하고 싶을 때 (해금 직원 가정)
    public void ApplyMaxGrade(EmployeeGrade maxGrade) => ApplyLockState(true, maxGrade);

    void ApplyContainerBg(EmployeeGrade grade)
    {
        if (containerBG == null) return;
        if (_bgShimmerCo != null) { StopCoroutine(_bgShimmerCo); _bgShimmerCo = null; }

        if (grade == EmployeeGrade.Legendary)
        {
            if (isActiveAndEnabled) _bgShimmerCo = StartCoroutine(BgLegendaryShimmer());
            else containerBG.color = BgLegendary;
            return;
        }
        if (grade == EmployeeGrade.Unique)
        {
            if (isActiveAndEnabled) _bgShimmerCo = StartCoroutine(BgUniqueShimmer());
            else containerBG.color = BgUnique;
            return;
        }

        containerBG.color = grade switch
        {
            EmployeeGrade.Normal => BgNormal,
            EmployeeGrade.Rare   => BgRare,
            EmployeeGrade.Epic   => BgEpic,
            _                    => BgNormal
        };
    }

    IEnumerator BgUniqueShimmer()
    {
        Color goldA = new Color(1.0f, 0.85f, 0.30f);
        Color goldB = new Color(0.85f, 0.65f, 0.10f);
        float speed = 2.0f;
        while (true)
        {
            float t = (Mathf.Sin(Time.time * speed) + 1f) / 2f;
            containerBG.color = Color.Lerp(goldB, goldA, t);
            yield return null;
        }
    }

    IEnumerator BgLegendaryShimmer()
    {
        while (true)
        {
            float h = Mathf.Repeat(Time.time * 0.25f, 1f);
            containerBG.color = Color.HSVToRGB(h, 0.55f, 1f);
            yield return null;
        }
    }
}
