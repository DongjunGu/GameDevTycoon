using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 세로 스냅 리스트 (모바일 드래그 + 관성 + 끝 탄성 + 화살표 + 클릭 스냅) — ScrollRect 대체.
// 손으로 스와이프하면 가속도(관성)로 흐르다 감속, 멈추면 가장 가까운 슬롯을 "선택 슬롯"으로 스르륵 스냅.
// 맨 처음/끝을 넘겨 끌면 러버밴드로 저항하다 용수철처럼 반동(탄성).
// 선택 인덱스가 바뀌면 OnSelectedChanged(index) 발생.
//
// 부착 위치: 마스크 ScrollView 오브젝트(레이캐스트 타깃 Image 보유). content/슬롯/초기인덱스는 Setup()으로 주입.
// VerticalLayoutGroup.childScaleHeight=true 로 두어, 슬롯 localScale(선택=1/비선택=축소)을 레이아웃이 반영 →
// 크기와 무관하게 슬롯 사이 간격이 항상 visualSpacing(16) 으로 균일.
[DisallowMultipleComponent]
public class VerticalSnapList : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Snap 설정")]
    [Tooltip("뷰포트 위에서 몇 번째 위치를 선택 슬롯으로 쓸지 (0=맨 위, 1=2번째)")]
    public int selectionSlotIndex = 1;
    [Tooltip("스냅 이동 시간(초) — 스르륵 빠칭코 느낌")]
    public float snapDuration = 0.30f;

    [Header("아이템 크기 (0이면 첫 슬롯에서 자동 측정)")]
    public float itemHeight = 0f;
    [Tooltip("슬롯 사이 간격(px). childScaleHeight 로 축소/확대 모두 이 간격이 균일하게 적용됨")]
    public float visualSpacing = 16f;
    [Tooltip("비선택 슬롯 축소 비율 (0이면 첫 슬롯 EmployeeSlotListUI.unselectedScale 사용)")]
    public float shrinkScaleOverride = 0f;

    [Header("관성 / 탄성")]
    [Tooltip("스와이프 후 관성으로 계속 흐르게")]
    public bool inertia = true;
    [Tooltip("관성 감속률 (작을수록 빨리 멈춤, ScrollRect 기본 0.135)")]
    public float decelerationRate = 0.135f;
    [Tooltip("이 속도(px/s) 미만이면 관성 대신 바로 스냅")]
    public float flickThreshold = 60f;
    [Tooltip("끝을 넘겼을 때 되돌아오는 탄성(작을수록 빠르게 튕겨 돌아옴)")]
    public float elasticity = 0.12f;
    [Tooltip("맨 처음/끝에서 넘겨 끌 수 있는 최대 여유 공간(px) — 용수철 반동 범위")]
    public float overscroll = 90f;

    [Header("뷰포트 마스크")]
    [Tooltip("선택 슬롯 glow 가로 잘림 방지 — 뷰포트를 RectMask2D 로 바꾸고 좌우로 이만큼 클립 확장(세로는 유지). 0=확장 안 함")]
    public float horizontalMaskExpand = 60f;

    [Header("화살표 (비우면 형제 'TopArrow'/'BottomArrow' 자동 탐색)")]
    public Button upButton;
    public Button downButton;

    public event Action<int> OnSelectedChanged;

    private RectTransform _content;
    private RectTransform _viewport;
    private VerticalLayoutGroup _layout;
    private ContentSizeFitter _fitter;
    private Canvas _canvas;

    private readonly List<RectTransform> _items = new();
    private float _stride;          // 슬롯 간 중심 거리 = 축소높이 + spacing
    private float _effItemHeight;   // 원본(선택) 높이
    private float _shrunkHeight;    // 축소 높이
    private float _effSpacing;
    private int _selectedIndex = -1;
    private Coroutine _snapCo;
    private bool _dragging;
    private bool _ready;

    // 물리
    private float _velocity;        // content.y px/s
    private float _springVel;
    private bool _inertiaActive;

    public int SelectedIndex => _selectedIndex;
    public int Count => _items.Count;

    void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null) _canvas = _canvas.rootCanvas;
        ResolveArrows();
        if (upButton != null)   { upButton.onClick.RemoveListener(SelectPrevious);   upButton.onClick.AddListener(SelectPrevious); }
        if (downButton != null) { downButton.onClick.RemoveListener(SelectNext);     downButton.onClick.AddListener(SelectNext); }
    }

    // 비어 있으면 형제(같은 ScrollPanel 아래) 'TopArrow'/'BottomArrow' 버튼을 찾는다.
    void ResolveArrows()
    {
        var parent = transform.parent;
        if (parent == null) return;
        if (upButton == null)
        {
            var t = parent.Find("TopArrow");
            if (t != null) upButton = t.GetComponent<Button>();
        }
        if (downButton == null)
        {
            var t = parent.Find("BottomArrow");
            if (t != null) downButton = t.GetComponent<Button>();
        }
    }

    // EmployeeListUI 가 BuildList 직후 호출. 동기로 즉시 배치 — 패널이 뜨는 프레임에 이미 initialIndex 위치에 있음(점프/버벅임 없음).
    public void Setup(RectTransform content, List<RectTransform> items, int initialIndex)
    {
        _ready = false;
        _content = content;
        _items.Clear();
        if (items != null)
            foreach (var it in items)
                if (it != null) _items.Add(it);

        if (_content == null || _items.Count == 0)
        {
            _selectedIndex = -1;
            return;
        }
        _viewport = _content.parent as RectTransform;
        _layout = _content.GetComponent<VerticalLayoutGroup>();
        _fitter = _content.GetComponent<ContentSizeFitter>();
        if (_canvas == null)
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null) _canvas = _canvas.rootCanvas;
        }

        _velocity = 0f; _springVel = 0f; _inertiaActive = false;
        if (_snapCo != null) { StopCoroutine(_snapCo); _snapCo = null; }

        // 레이아웃을 즉시(동기) 확정한 뒤 같은 프레임에 위치/선택 적용 → 렌더 전에 타겟에 배치됨
        ConfigureContent();
        ConfigureViewportMask();
        MeasureMetrics();
        ApplyPadding();                                       // 내부에서 ForceRebuildLayoutImmediate
        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);

        _ready = true;
        int idx = Mathf.Clamp(initialIndex, 0, _items.Count - 1);
        SetContentY(TargetY(idx));
        SetSelectedIndex(idx, fire: true, force: true);
        UpdateArrows();
    }

    // content 를 스냅 스크롤에 맞게 재구성 (top-anchor, pivot top, ContentSizeFitter, childScaleHeight)
    void ConfigureContent()
    {
        if (_content == null) return;
        _content.anchorMin = new Vector2(0.5f, 1f);
        _content.anchorMax = new Vector2(0.5f, 1f);
        _content.pivot     = new Vector2(0.5f, 1f);

        if (_fitter == null) _fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
        _fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        _fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        if (_layout != null)
        {
            _layout.childControlHeight = false;
            _layout.childForceExpandHeight = false;
            _layout.childScaleHeight = true;   // 슬롯 localScale 을 레이아웃 높이에 반영 → 간격 균일
        }
    }

    // 뷰포트의 스텐실 Mask(가로·세로 모두 클립) → RectMask2D 로 교체하고 좌우 클립을 확장.
    void ConfigureViewportMask()
    {
        if (_viewport == null || horizontalMaskExpand <= 0f) return;

        var stencil = _viewport.GetComponent<Mask>();
        if (stencil != null)
        {
            Destroy(stencil);
            var img = _viewport.GetComponent<Image>();
            if (img != null) img.enabled = false; // 마스크 그래픽 불필요 → 흰 박스 방지(드래그는 ScrollView Image가 받음)
        }

        var rm = _viewport.GetComponent<RectMask2D>();
        if (rm == null) rm = _viewport.gameObject.AddComponent<RectMask2D>();
        // padding: x=Left, y=Bottom, z=Right, w=Top. 음수 = 바깥으로 확장. 세로(y,w)=0 유지.
        rm.padding = new Vector4(-horizontalMaskExpand, 0f, -horizontalMaskExpand, 0f);
    }

    void MeasureMetrics()
    {
        _effItemHeight = itemHeight > 0f
            ? itemHeight
            : (_items.Count > 0 ? Mathf.Max(1f, _items[0].rect.height) : 156f);

        float shrink = ResolveShrinkScale();
        _shrunkHeight = _effItemHeight * shrink;
        _effSpacing = visualSpacing;
        // childScaleHeight 덕분에 비선택 슬롯은 축소높이(_shrunkHeight)만 차지 → 슬롯 간 중심거리 = 축소높이 + spacing
        _stride = _shrunkHeight + _effSpacing;
        if (_stride <= 0f) _stride = 1f;

        if (_layout != null) _layout.spacing = _effSpacing;
    }

    // 비선택 슬롯 축소 비율 — override 우선, 없으면 첫 슬롯의 EmployeeSlotListUI.unselectedScale
    float ResolveShrinkScale()
    {
        if (shrinkScaleOverride > 0f) return shrinkScaleOverride;
        if (_items.Count > 0 && _items[0] != null)
        {
            var slot = _items[0].GetComponent<EmployeeSlotListUI>();
            if (slot != null) return Mathf.Clamp(slot.unselectedScale.y, 0.01f, 1f);
        }
        return 0.8125f;
    }

    void ApplyPadding()
    {
        if (_layout == null) return;
        float vpH = _viewport != null ? _viewport.rect.height : 0f;
        int padTop = Mathf.RoundToInt(selectionSlotIndex * _stride);
        int padBot = Mathf.Max(0, Mathf.RoundToInt(vpH - selectionSlotIndex * _stride - _shrunkHeight));
        _layout.padding.top = padTop;
        _layout.padding.bottom = padBot;
        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
    }

    // 인덱스 i 를 선택 슬롯에 맞추는 content.anchoredPosition.y (top 패딩 보정으로 i*stride)
    float TargetY(int i) => Mathf.Clamp(i, 0, Mathf.Max(0, _items.Count - 1)) * _stride;

    float MaxY() => Mathf.Max(0, _items.Count - 1) * _stride;

    // 인바운드로 클램프해 설정 (스냅/초기화용)
    void SetContentY(float y)
    {
        if (_content == null) return;
        var p = _content.anchoredPosition;
        p.y = Mathf.Clamp(y, 0f, MaxY());
        _content.anchoredPosition = p;
    }

    // 오버스크롤 허용 설정 (드래그/관성/탄성용)
    void SetContentYRaw(float y)
    {
        if (_content == null) return;
        y = Mathf.Clamp(y, -overscroll, MaxY() + overscroll);
        var p = _content.anchoredPosition;
        p.y = y;
        _content.anchoredPosition = p;
    }

    // 경계를 벗어난 양(부호 있음): y<0 이면 음수, y>MaxY 이면 양수, 인바운드면 0
    float OverAmount(float y)
    {
        if (y < 0f) return y;
        float m = MaxY();
        if (y > m) return y - m;
        return 0f;
    }

    int NearestIndex()
    {
        if (_content == null || _stride <= 0f) return 0;
        return Mathf.Clamp(Mathf.RoundToInt(_content.anchoredPosition.y / _stride), 0, _items.Count - 1);
    }

    static float RubberDelta(float overStretching, float viewSize)
        => (1f - (1f / ((Mathf.Abs(overStretching) * 0.55f / Mathf.Max(1f, viewSize)) + 1f))) * Mathf.Max(1f, viewSize);

    // ── 외부 API ───────────────────────────────
    public void SnapToIndex(int index, bool fireImmediate = true)
    {
        if (!_ready || _items.Count == 0) return;
        index = Mathf.Clamp(index, 0, _items.Count - 1);
        _inertiaActive = false; _velocity = 0f; _springVel = 0f;
        if (fireImmediate) SetSelectedIndex(index, fire: true, force: false);
        if (_snapCo != null) StopCoroutine(_snapCo);
        _snapCo = StartCoroutine(SnapRoutine(index, snapDuration, liveSelect: true));
        UpdateArrows();
    }

    public void SelectPrevious() => SnapToIndex(_selectedIndex - 1);
    public void SelectNext()     => SnapToIndex(_selectedIndex + 1);

    // liveSelect=true: 스크롤 중 지나가는 슬롯으로 선택 갱신(드래그/클릭 스냅). false: 선택 고정, 위치만 이동(인트로 스크롤).
    IEnumerator SnapRoutine(int index, float dur, bool liveSelect)
    {
        float from = _content.anchoredPosition.y;
        float to = TargetY(index);
        dur = Mathf.Max(0.01f, dur);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            SetContentY(Mathf.Lerp(from, to, k));
            if (liveSelect) SetSelectedIndex(NearestIndex(), fire: true, force: false);
            yield return null;
        }
        SetContentY(to);
        SetSelectedIndex(index, fire: true, force: false);
        _snapCo = null;
    }

    void SetSelectedIndex(int index, bool fire, bool force)
    {
        index = Mathf.Clamp(index, 0, Mathf.Max(0, _items.Count - 1));
        if (index == _selectedIndex && !force) return;

        if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
        {
            var prev = _items[_selectedIndex] != null ? _items[_selectedIndex].GetComponent<EmployeeSlotListUI>() : null;
            if (prev != null) prev.SetSelected(false);
        }
        _selectedIndex = index;
        var cur = _items[_selectedIndex] != null ? _items[_selectedIndex].GetComponent<EmployeeSlotListUI>() : null;
        if (cur != null) cur.SetSelected(true);

        if (fire) OnSelectedChanged?.Invoke(_selectedIndex);
        UpdateArrows();
    }

    void UpdateArrows()
    {
        // 처음/끝이면 해당 화살표 비활성
        if (upButton != null)   upButton.interactable   = _selectedIndex > 0;
        if (downButton != null) downButton.interactable = _selectedIndex < _items.Count - 1;
    }

    // ── 드래그 ─────────────────────────────────
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!_ready) return;
        if (_snapCo != null) { StopCoroutine(_snapCo); _snapCo = null; }
        _inertiaActive = false;
        _velocity = 0f; _springVel = 0f;
        _dragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_ready || !_dragging) return;
        float scale = _canvas != null ? _canvas.scaleFactor : 1f;
        if (scale <= 0f) scale = 1f;
        float dt = Mathf.Max(1e-4f, Time.unscaledDeltaTime);
        float vh = _viewport != null ? _viewport.rect.height : 500f;

        float dy = eventData.delta.y / scale;          // 손가락 위로 = 콘텐츠 위로(아래 슬롯 노출)
        float y = _content.anchoredPosition.y;
        float target = y + dy;

        // 경계 밖이면 러버밴드 저항
        float max = MaxY();
        if (target < 0f)       target = -RubberDelta(-target, vh);
        else if (target > max) target = max + RubberDelta(target - max, vh);

        float vy = (target - y) / dt;
        _velocity = Mathf.Lerp(_velocity, vy, 0.7f);   // 속도 추적(스무딩)

        SetContentYRaw(target);
        SetSelectedIndex(NearestIndex(), fire: true, force: false); // 드래그 중 실시간 강조
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_ready) return;
        _dragging = false;

        if (OverAmount(_content.anchoredPosition.y) != 0f)
        {
            _inertiaActive = false; // LateUpdate 탄성 반동이 처리 후 스냅
        }
        else if (inertia && Mathf.Abs(_velocity) > flickThreshold)
        {
            _inertiaActive = true;  // 관성 흐름
        }
        else
        {
            SnapToIndex(NearestIndex(), fireImmediate: false);
        }
    }

    void LateUpdate()
    {
        if (!_ready || _dragging || _snapCo != null) return;
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;

        float y = _content.anchoredPosition.y;
        float over = OverAmount(y);

        if (_inertiaActive)
        {
            if (over != 0f)
            {
                // 관성이 경계를 넘김 → 탄성으로 되돌리며 감속
                float bound = y - over;
                float ny = Mathf.SmoothDamp(y, bound, ref _velocity, elasticity, Mathf.Infinity, dt);
                if (Mathf.Abs(ny - bound) < 1f)
                {
                    SetContentYRaw(bound); _velocity = 0f; _inertiaActive = false;
                    SnapToIndex(NearestIndex(), fireImmediate: false);
                }
                else SetContentYRaw(ny);
            }
            else
            {
                _velocity *= Mathf.Pow(decelerationRate, dt);
                SetContentYRaw(y + _velocity * dt);
                SetSelectedIndex(NearestIndex(), fire: true, force: false);
                if (Mathf.Abs(_velocity) < flickThreshold)
                {
                    _inertiaActive = false;
                    SnapToIndex(NearestIndex(), fireImmediate: false);
                }
            }
        }
        else if (over != 0f)
        {
            // 드래그를 경계 밖에서 천천히 놓음 → 용수철 반동
            float bound = y - over;
            float ny = Mathf.SmoothDamp(y, bound, ref _springVel, elasticity, Mathf.Infinity, dt);
            if (Mathf.Abs(ny - bound) < 1f)
            {
                SetContentYRaw(bound); _springVel = 0f;
                SnapToIndex(NearestIndex(), fireImmediate: false);
            }
            else SetContentYRaw(ny);
        }
    }
}
