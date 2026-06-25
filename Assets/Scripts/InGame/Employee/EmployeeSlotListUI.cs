using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// EmployeeListUI 우측 목록용 슬롯 (TrainingEmployeeSlotUI 와 거의 동일).
// 구조: employeePortraitImage(컨테이너) ─ bgImage(등급 색) + portraitImage(초상화), nameText, selectButton.
// 클릭 시 onSelect 콜백으로 직원 전달. 파견중이면 dim + badge (단, 선택은 허용 — 좌측 상세 보기용,
// 강화/아이템/해고는 EmployeeListUI 버튼에서 AlertUI 로 차단).
//
// 선택 슬롯(스냅 캐러셀의 위 2번째)은 SetSelected(true) 로 강조:
//  - 확대(130x156 -> 160x192 = 1.23배) + 등급 프레임 컬러(비선택은 흑백) + 뒤 반짝임(glow) + 최상위 렌더.
public class EmployeeSlotListUI : MonoBehaviour
{
    [Header("UI")]
    public Image employeePortraitImage; // 초상화 컨테이너 (자식 bgImage/portraitImage 의 탐색 기준)
    public TextMeshProUGUI levelText;          // 강화 레벨 "Lv.{}"
    public Button selectButton;
    [Header("파견중 표시 (옵션)")]
    public GameObject dispatchedBadge;

    [Header("선택 — 비우면 employeePortraitImage 자식에서 자동 탐색")]
    public Image portraitImage; // 초상화 sprite 대상 (앞)
    public Image bgImage;       // 등급 색 대상 (뒤)

    [Header("선택 강조 (스냅 캐러셀)")]
    [Tooltip("등급 테두리 프레임 Image (비우면 자식 'Panel' 자동 탐색)")]
    public Image gradeFrame;
    [Tooltip("등급별 프레임 스프라이트 세트 (GradeFrameSet)")]
    public GradeSpriteSet gradeFrameSet;
    [Tooltip("비선택 슬롯 흑백 처리용 머티리얼 (UI/Grayscale)")]
    public Material grayscaleMaterial;
    [Tooltip("선택 시 슬롯 뒤에 깔리는 반짝임 스프라이트 (glowEffect). 없으면 미생성")]
    public Sprite shimmerSprite;
    [Tooltip("선택 슬롯 스케일 (기본 160x192 그대로 = 1.0)")]
    public Vector3 selectedScale = Vector3.one;
    [Tooltip("비선택 슬롯 축소 스케일 (160x192 -> 130x156 = 0.8125)")]
    public Vector3 unselectedScale = new Vector3(0.8125f, 0.8125f, 0.8125f);
    [Tooltip("반짝임 이미지를 슬롯보다 얼마나 키울지 (1.0=동일)")]
    public float shimmerScale = 1.35f;
    [Tooltip("스케일 보간 시간")]
    public float scaleLerpDuration = 0.2f;

    public bool IsSelected { get; private set; }

    private Coroutine _gradeCo;
    private Coroutine _scaleCo;
    private EmployeeGrade _grade = EmployeeGrade.Normal;
    private Color _levelTextColor = Color.white;
    private bool _levelColorCached;
    private GameObject _shimmer;

    public void Setup(EmployeeData data, System.Action<EmployeeData> onSelect)
    {
        if (levelText != null)
        {
            if (!_levelColorCached) { _levelTextColor = levelText.color; _levelColorCached = true; }
            levelText.text = $"Lv.{data.enhancementLevel}";
        }

        var portrait = ResolvePortrait();
        if (portrait != null && !string.IsNullOrEmpty(data.portraitId))
        {
            var sprite = Resources.Load<Sprite>($"Portraits/Mini/{data.portraitId}");
            if (sprite != null) portrait.sprite = sprite;
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelect?.Invoke(data));
        }

        // 파견중이면 희미하게 + badge (blockSelect=false — 상세 보기는 가능)
        DispatchSlotVisual.Apply(this, dispatchedBadge, data.id, blockSelect: false);

        _grade = data.grade;
        ApplyGrade(_grade);
        ApplyGradeFrame(_grade);

        // 초기엔 비선택 상태
        IsSelected = false;
        ApplySelectionVisual(false, instant: true);
    }

    void OnDisable()
    {
        if (_gradeCo != null) { StopCoroutine(_gradeCo); _gradeCo = null; }
        if (_scaleCo != null) { StopCoroutine(_scaleCo); _scaleCo = null; }
    }

    void ApplyGrade(EmployeeGrade grade)
    {
        var bg = ResolveBg();
        if (_gradeCo != null) { StopCoroutine(_gradeCo); _gradeCo = null; }
        _gradeCo = EmployeeGradeColor.Apply(this, bg, grade);
    }

    void ApplyGradeFrame(EmployeeGrade grade)
    {
        var frame = ResolveFrame();
        GradeSpriteSet.Apply(frame, gradeFrameSet, grade);
    }

    // ── 선택 상태 ───────────────────────────────
    public void SetSelected(bool selected, bool instant = false)
    {
        IsSelected = selected;
        ApplySelectionVisual(selected, instant);
    }

    void ApplySelectionVisual(bool selected, bool instant)
    {
        // 1) 흑백 토글: 비선택 = grayscale 머티리얼(슬롯 전체), 선택 = 기본(컬러)
        Material mat = selected ? null : GetGrayMaterial();
        foreach (var img in GetComponentsInChildren<Image>(true))
        {
            if (img == null) continue;
            if (_shimmer != null && img.gameObject == _shimmer) continue; // 반짝임은 항상 원색
            img.material = mat; // null = 기본 UI 머티리얼(컬러)
        }

        // LevelText(TMP)는 머티리얼 흑백 대상이 아니므로 색으로 직접 처리 (비선택=회색)
        if (levelText != null)
        {
            if (!_levelColorCached) { _levelTextColor = levelText.color; _levelColorCached = true; }
            if (selected) levelText.color = _levelTextColor;
            else
            {
                float g = _levelTextColor.r * 0.299f + _levelTextColor.g * 0.587f + _levelTextColor.b * 0.114f;
                levelText.color = new Color(g, g, g, _levelTextColor.a);
            }
        }

        // 2) 뒤 반짝임
        EnsureShimmer();
        if (_shimmer != null) _shimmer.SetActive(selected);

        // 3) 스케일 — 선택=기본(160x192), 비선택=축소
        Vector3 target = selected ? selectedScale : unselectedScale;
        if (instant || scaleLerpDuration <= 0f || !isActiveAndEnabled)
        {
            if (_scaleCo != null) { StopCoroutine(_scaleCo); _scaleCo = null; }
            transform.localScale = target;
        }
        else
        {
            if (_scaleCo != null) StopCoroutine(_scaleCo);
            _scaleCo = StartCoroutine(LerpScale(target));
        }

    }

    IEnumerator LerpScale(Vector3 target)
    {
        Vector3 start = transform.localScale;
        float t = 0f;
        while (t < scaleLerpDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / scaleLerpDuration));
            transform.localScale = Vector3.Lerp(start, target, k);
            yield return null;
        }
        transform.localScale = target;
        _scaleCo = null;
    }

    void EnsureShimmer()
    {
        if (_shimmer != null || shimmerSprite == null) return;
        var go = new GameObject("Shimmer", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.SetAsFirstSibling();                 // 제일 뒤(맨 먼저 그려짐)
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = new Vector3(shimmerScale, shimmerScale, 1f);
        var img = go.GetComponent<Image>();
        img.sprite = shimmerSprite;
        img.raycastTarget = false;
        img.preserveAspect = false;
        // 레이아웃 그룹이 건드리지 않도록
        var le = go.AddComponent<LayoutElement>();
        le.ignoreLayout = true;
        _shimmer = go;
        _shimmer.SetActive(false);
    }

    // 흑백 머티리얼 — 인스펙터 지정(grayscaleMaterial) 우선, 없으면 UI/Grayscale 셰이더로 런타임 생성(공용).
    private static Material _sharedGray;
    Material GetGrayMaterial()
    {
        if (grayscaleMaterial != null) return grayscaleMaterial;
        if (_sharedGray == null)
        {
            var sh = Shader.Find("UI/Grayscale");
            if (sh != null)
            {
                _sharedGray = new Material(sh) { name = "UIGrayscale (runtime)" };
                _sharedGray.SetFloat("_GrayAmount", 1f);
            }
        }
        return _sharedGray;
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

    // 프레임: gradeFrame 필드 → 자식 "Frame"(구 "Panel")
    Image ResolveFrame()
    {
        if (gradeFrame == null)
        {
            var t = transform.Find("Frame");
            if (t == null) t = transform.Find("Panel");
            if (t != null) gradeFrame = t.GetComponent<Image>();
        }
        return gradeFrame;
    }

    Image FindChildImage(string childName)
    {
        if (employeePortraitImage == null) return null;
        var t = employeePortraitImage.transform.Find(childName);
        return t != null ? t.GetComponent<Image>() : null;
    }
}
