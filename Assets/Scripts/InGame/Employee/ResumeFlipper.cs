using System;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 이력서 캐러셀 — "회전문" 연출. 좌·중·우 3슬롯을 종이들이 돈다.
/// - 다음(+1): 오른쪽 종이가 앞으로 나오며 가운데로, 가운데 카드는 뒤로 물러나며 왼쪽으로 (왼쪽 종이는 오른쪽으로 비켜남)
/// - 이전(-1): 왼쪽 종이가 앞으로 나오며 가운데로, 가운데 카드는 뒤로 물러나며 오른쪽으로
/// 좌·우 슬롯은 "가운데 카드 위치 + 인스펙터 오프셋(sideOffsetX/Y, sideScale, sideTiltZ)" 으로 계산한다.
///   → 좌우로 벌어지는 정도/스케일/기울기를 인스펙터에서 바로 조절 가능. (에디터에서 값 변경 시 종이가 즉시 따라옴)
/// 슬라이드 중간(교차점)에서 들어오는 종이의 sortingOrder 를 올려 "앞으로", 나가는 카드를 내려 "뒤로"(회전문 깊이 교체).
///   → 깊이 교체에는 종이/가운데 카드에 Canvas(overrideSorting) 필요. 없으면 깊이 효과만 생략.
/// 실제 이력서 내용은 가운데 카드(이 오브젝트)에만 있으므로, 끝나면 세 종이를 원 슬롯으로 복귀시키고 onSwap 으로 가운데 내용을 교체.
/// 채용 패널은 게임시간 정지(timeScale=0 가능)라 모든 트윈은 SetUpdate(true).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class ResumeFlipper : MonoBehaviour
{
    [Header("Carousel papers (siblings)")]
    [Tooltip("왼쪽 슬롯 종이 (PaperBehindLeft)")]
    [SerializeField] RectTransform leftPaper;
    [Tooltip("오른쪽 슬롯 종이 (PaperBehindRight)")]
    [SerializeField] RectTransform rightPaper;

    [Header("Count Panel (카드에 고정 — 넘김 영향 X)")]
    [Tooltip("CountPanel(1/4 표시). 가운데 카드의 '자식이 아닌' 형제로 두고 할당. 카드 홈(쉬는 상태) 좌측상단 기준으로 매 프레임 고정 위치된다.")]
    [SerializeField] RectTransform countPanel;
    [Tooltip("카드 좌측상단 코너 기준 오프셋(캔버스 px). 예: (-110, 0)")]
    [SerializeField] Vector2 countOffsetFromTopLeft = new Vector2(-110f, 0f);

    [Header("Slot layout (가운데 기준 좌/우 종이 배치 — 인스펙터 조절)")]
    [Tooltip("가운데에서 좌·우 종이까지의 가로 거리. 클수록 좌우로 더 벌어진다.")]
    [SerializeField] float sideOffsetX = 140f;
    [Tooltip("좌·우 종이의 세로 오프셋")]
    [SerializeField] float sideOffsetY = 0f;
    [Tooltip("좌·우 종이 스케일")]
    [SerializeField] float sideScale = 0.9f;
    [Tooltip("좌·우 종이 기울기(도). 왼쪽 +, 오른쪽 −")]
    [SerializeField] float sideTiltZ = 12f;

    [Header("Timing")]
    [SerializeField] float slideDuration = 0.32f;
    [SerializeField] Ease slideEase = Ease.InOutCubic;
    [Tooltip("새 이력서가 가운데 안착 시 살짝 튕기는 정도(스케일 비율). 0이면 없음")]
    [SerializeField] float settlePunch = 0.06f;
    [SerializeField] float settleDuration = 0.12f;

    [Header("Shuffle (후보 새로고침 연출)")]
    [Tooltip("세 종이를 가운데로 모으는 시간")]
    [SerializeField] float shuffleGatherDuration = 0.22f;
    [Tooltip("모았을 때 스택 스케일 비율(가운데 기준). 작을수록 더 작게 겹친다")]
    [SerializeField] float shuffleStackScale = 0.55f;
    [Tooltip("모으는 동안 회전량(도). 카드를 섞는 느낌")]
    [SerializeField] float shuffleSpin = 360f;

    RectTransform _center;
    Vector2 _centerHome;          // 슬라이드 시작 시점(쉬는 상태)의 가운데 위치
    Vector3 _centerScale = Vector3.one, _centerRot = Vector3.zero;
    Canvas _centerCanvas, _leftCanvas, _rightCanvas;
    int _centerSort = 1; // 모달 밖 폴백용 기준 order (ModalBaseOrder 에서 사용)
    bool _init;
    Tween _tween;

    public bool IsFlipping { get; private set; }

    // ── 슬롯 계산 (가운데 기준 상대) ──
    Vector2 LeftSlot(Vector2 c)  => c + new Vector2(-sideOffsetX, sideOffsetY);
    Vector2 RightSlot(Vector2 c) => c + new Vector2( sideOffsetX, sideOffsetY);
    Vector3 SideScaleV => new Vector3(sideScale, sideScale, 1f);
    Vector3 LeftRot    => new Vector3(0f, 0f,  sideTiltZ);
    Vector3 RightRot   => new Vector3(0f, 0f, -sideTiltZ);

    void Awake() => EnsureInit();

    void EnsureInit()
    {
        if (_init) return;
        _center = (RectTransform)transform;
        _centerHome  = _center.anchoredPosition;
        _centerScale = _center.localScale;
        _centerRot   = _center.localEulerAngles;
        _centerCanvas = GetComponent<Canvas>();
        _centerSort = _centerCanvas != null ? _centerCanvas.sortingOrder : 1;
        if (leftPaper  != null) _leftCanvas  = leftPaper.GetComponent<Canvas>();
        if (rightPaper != null) _rightCanvas = rightPaper.GetComponent<Canvas>();
        _init = true;
    }

    // 세 종이를 홈/슬롯으로 (런타임 슬라이드 후 복귀용)
    void ApplyHome()
    {
        if (_center == null) _center = (RectTransform)transform;
        _center.anchoredPosition3D = new Vector3(_centerHome.x, _centerHome.y, 0f);
        _center.localScale = _centerScale;
        _center.localEulerAngles = _centerRot;
        if (leftPaper  != null) { var p = LeftSlot(_centerHome);  leftPaper.anchoredPosition3D  = new Vector3(p.x, p.y, 0f); leftPaper.localScale  = SideScaleV; leftPaper.localEulerAngles  = LeftRot; }
        if (rightPaper != null) { var p = RightSlot(_centerHome); rightPaper.anchoredPosition3D = new Vector3(p.x, p.y, 0f); rightPaper.localScale = SideScaleV; rightPaper.localEulerAngles = RightRot; }
    }

    // 쉬는 상태: 가운데는 "모달 기준 order"(b), 좌/우는 그보다 1 낮게(b-1).
    // - 셋 다 블로커보다는 위라 좌/우 종이도 클릭 OK.
    // - 좌/우가 가운데와 겹치는 구간이 있어(카드 폭/스케일에 따라) 같은 order 면 그 구간 레이캐스트가 애매해져
    //   터치가 안 먹는 문제가 있었음 → 가운데를 항상 1 위로 둬서 겹침 구간은 가운데가 우선되게 함.
    void RestoreSorting()
    {
        int b = ModalBaseOrder();
        if (_centerCanvas != null) { _centerCanvas.overrideSorting = true; _centerCanvas.sortingOrder = b; }
        if (_leftCanvas  != null) { _leftCanvas.overrideSorting  = true; _leftCanvas.sortingOrder  = b - 1; }
        if (_rightCanvas != null) { _rightCanvas.overrideSorting = true; _rightCanvas.sortingOrder = b - 1; }
    }

    // 종이 Canvas 가 따라갈 기준 정렬값 = 위쪽(모달) Canvas 의 sortingOrder.
    // 종이 자신의 Canvas 는 건너뛰려 transform.parent 부터 위로 탐색. 모달이면 블로커보다 1 높은 값(예: 52).
    // 이 값을 기준으로 쉴 땐 셋 다 b, 넘기는 중 들어오는 종이만 b+1 로 올려 "앞으로" 나오게 한다.
    // (브라우징 중엔 ConfirmHirePanel 만 모달이라 b+1 이 위 모달과 충돌하지 않음. 모달 밖이면 부모 캔버스 order 그대로.)
    int ModalBaseOrder()
    {
        var p = transform.parent;
        var c = (p != null) ? p.GetComponentInParent<Canvas>() : null;
        return (c != null) ? c.sortingOrder : _centerSort;
    }

    readonly Vector3[] _cornerBuf = new Vector3[4];

    // CountPanel 을 가운데 카드의 "좌측상단 코너 + 오프셋"에 고정.
    // 카드의 실제 월드 코너에서 계산하므로 해상도/캔버스 스케일에 독립적이고,
    // 넘기는 중(IsFlipping)에는 호출하지 않아 카드가 움직여도 CountPanel 은 제자리에 머문다.
    // ※ CountPanel 은 카드의 '자식이 아닌' 형제여야 함(자식이면 카드 트랜스폼을 따라가 움직임).
    void PinCountPanel()
    {
        if (countPanel == null || _center == null) return;
        _center.GetWorldCorners(_cornerBuf);          // 0:BL 1:TL 2:TR 3:BR
        Vector3 worldTopLeft = _cornerBuf[1];
        // 오프셋은 캔버스(부모) px 기준 → 월드로 변환해 해상도/스케일 반영.
        var parent = _center.parent as RectTransform;
        Vector3 worldOffset = (parent != null)
            ? parent.TransformVector(countOffsetFromTopLeft.x, countOffsetFromTopLeft.y, 0f)
            : (Vector3)countOffsetFromTopLeft;
        countPanel.position = worldTopLeft + worldOffset;
    }

    /// <summary>
    /// direction: +1 다음 / -1 이전. onSwap: 슬라이드 끝(가운데 복귀)에서 호출 — 이력서 데이터 교체.
    /// </summary>
    public void Flip(int direction, Action onSwap, Action onComplete = null)
    {
        EnsureInit();
        _tween?.Kill();

        // 현재(쉬는) 위치를 홈으로 갱신 후 정렬 초기화
        _centerHome  = _center.anchoredPosition;
        _centerScale = _center.localScale;
        _centerRot   = _center.localEulerAngles;
        ApplyHome();
        RestoreSorting();
        IsFlipping = true;

        bool next = direction >= 0;
        Canvas incomingCanvas = next ? _rightCanvas : _leftCanvas;

        // 넘기는 즉시 들어오는 패널을 가운데보다 앞으로 → 누르자마자 기존 가운데가 가려지고 들어오는 게 앞으로 온다.
        // 모달 기준 order(b) 위로 +1 만 올림 — 셋 다 블로커 위라 클릭 유지하면서 들어오는 종이만 앞.
        if (incomingCanvas != null) { incomingCanvas.overrideSorting = true; incomingCanvas.sortingOrder = ModalBaseOrder() + 1; }

        Vector2 leftSlot  = LeftSlot(_centerHome);
        Vector2 rightSlot = RightSlot(_centerHome);

        Vector2 centerTo  = next ? leftSlot : rightSlot;
        Vector3 centerToR = next ? LeftRot  : RightRot;

        var seq = DOTween.Sequence();
        Slide(seq, _center, centerTo, SideScaleV, centerToR);

        if (next)
        {
            Slide(seq, rightPaper, _centerHome, _centerScale, _centerRot); // 오른쪽→가운데(앞으로)
            Slide(seq, leftPaper,  rightSlot,   SideScaleV,   RightRot);   // 왼쪽→오른쪽(비켜남)
        }
        else
        {
            Slide(seq, leftPaper,  _centerHome, _centerScale, _centerRot); // 왼쪽→가운데(앞으로)
            Slide(seq, rightPaper, leftSlot,    SideScaleV,   LeftRot);    // 오른쪽→왼쪽(비켜남)
        }

        seq.OnComplete(() =>
        {
            onSwap?.Invoke();
            ApplyHome();
            RestoreSorting();   // 들어왔던 패널 정렬을 원래(뒤)로 복구 — 가운데 패널이 다시 맨 앞
            IsFlipping = false;
            if (settlePunch > 0f)
            {
                _center.localScale = _centerScale * (1f - settlePunch);
                _center.DOScale(_centerScale, settleDuration).SetEase(Ease.OutBack).SetUpdate(true);
            }
            onComplete?.Invoke();
        });
        seq.SetUpdate(true);
        _tween = seq;
    }

    void Slide(Sequence seq, RectTransform rt, Vector2 pos, Vector3 scale, Vector3 rot)
    {
        if (rt == null) return;
        seq.Join(rt.DOAnchorPos(pos, slideDuration).SetEase(slideEase));
        seq.Join(rt.DOScale(scale, slideDuration).SetEase(slideEase));
        seq.Join(rt.DOLocalRotate(rot, slideDuration, RotateMode.Fast).SetEase(slideEase));
    }

    /// <summary>
    /// 후보 새로고침 셔플 연출 — 세 종이를 가운데로 모아 스택(서로 다른 방향 회전)했다가
    /// onMidpoint 에서 내용 교체 후 다시 좌/중/우 슬롯으로 펼친다.
    /// onMidpoint 에서 HiringUI 가 새 후보로 세 패널을 다시 채운다 → 스택돼 가려진 순간이라 교체가 안 보인다.
    /// 채용 패널은 시간 정지라 모든 트윈 SetUpdate(true).
    /// </summary>
    public void Shuffle(Action onMidpoint, Action onComplete = null)
    {
        EnsureInit();
        _tween?.Kill();

        // 현재(쉬는) 위치를 홈으로 갱신 후 정렬/배치 초기화
        _centerHome  = _center.anchoredPosition;
        _centerScale = _center.localScale;
        _centerRot   = _center.localEulerAngles;
        ApplyHome();
        RestoreSorting();
        IsFlipping = true;

        Vector3 stackScale = _centerScale * shuffleStackScale;

        var seq = DOTween.Sequence();
        // ── 모으기 ── 세 종이가 가운데로 겹치며 작아지고 서로 다른 방향으로 회전(섞는 느낌)
        Gather(seq, _center,    _centerHome, stackScale,  shuffleSpin);
        Gather(seq, leftPaper,  _centerHome, stackScale, -shuffleSpin);
        Gather(seq, rightPaper, _centerHome, stackScale,  shuffleSpin);

        // 스택돼 가려진 순간에 내용 교체 (보이지 않음)
        seq.AppendCallback(() => onMidpoint?.Invoke());

        // ── 펼치기 ── center 부터 Append 로 타임라인을 이어붙이고 좌/우는 Join 으로 동시에 홈 슬롯 복귀
        seq.Append(_center.DOAnchorPos(_centerHome, slideDuration).SetEase(slideEase));
        seq.Join(_center.DOScale(_centerScale, slideDuration).SetEase(slideEase));
        seq.Join(_center.DOLocalRotate(_centerRot, slideDuration, RotateMode.Fast).SetEase(slideEase));
        SpreadJoin(seq, leftPaper,  LeftSlot(_centerHome),  SideScaleV, LeftRot);
        SpreadJoin(seq, rightPaper, RightSlot(_centerHome), SideScaleV, RightRot);

        seq.OnComplete(() =>
        {
            ApplyHome();
            RestoreSorting();
            IsFlipping = false;
            if (settlePunch > 0f)
            {
                _center.localScale = _centerScale * (1f - settlePunch);
                _center.DOScale(_centerScale, settleDuration).SetEase(Ease.OutBack).SetUpdate(true);
            }
            onComplete?.Invoke();
        });
        seq.SetUpdate(true);
        _tween = seq;
    }

    // 모으기 단계 — 가운데로 이동+축소+회전 (seq 시작에 Join, 동시 진행)
    void Gather(Sequence seq, RectTransform rt, Vector2 pos, Vector3 scale, float spin)
    {
        if (rt == null) return;
        seq.Join(rt.DOAnchorPos(pos, shuffleGatherDuration).SetEase(Ease.InCubic));
        seq.Join(rt.DOScale(scale, shuffleGatherDuration).SetEase(Ease.InCubic));
        seq.Join(rt.DOLocalRotate(new Vector3(0f, 0f, spin), shuffleGatherDuration, RotateMode.FastBeyond360).SetEase(Ease.InCubic));
    }

    // 펼치기 단계의 좌/우 종이 — center Append 와 같은 시점에 Join (동시 진행)
    void SpreadJoin(Sequence seq, RectTransform rt, Vector2 pos, Vector3 scale, Vector3 rot)
    {
        if (rt == null) return;
        seq.Join(rt.DOAnchorPos(pos, slideDuration).SetEase(slideEase));
        seq.Join(rt.DOScale(scale, slideDuration).SetEase(slideEase));
        seq.Join(rt.DOLocalRotate(rot, slideDuration, RotateMode.Fast).SetEase(slideEase));
    }

    // 패널이 열릴 때(혹은 컴파일/리로드 후) 옆 패널을 슬롯 위치/스케일/회전으로 즉시 배치 — 첫 넘김 전에도 올바른 캐러셀 구도.
    void OnEnable()
    {
        EnsureInit();
        _centerHome  = _center.anchoredPosition;
        _centerScale = _center.localScale;
        _centerRot   = _center.localEulerAngles;
        ApplyHome();
        RestoreSorting(); // 모달 기준 order 로 세 종이 정렬 (블로커 위 → 클릭 OK)
        PinCountPanel();  // CountPanel 첫 위치 고정
    }

    void OnDisable()
    {
        if (!Application.isPlaying) return;
        _tween?.Kill();
        IsFlipping = false;
        ApplyHome();
        RestoreSorting();
    }

    // 쉬는 동안 매 프레임 모달 기준 order 재적용 — OnEnable 시점에 모달(ModalLayer)이 아직 order 를
    // 할당하기 전이라 종이가 블로커 아래로 깔려 "처음에 안 보이는" 문제를 자동 보정.
    // (채용 화면은 시간정지라 비용 무의미. 넘기는 중엔 들어오는 종이 +1 유지를 위해 건드리지 않음.)
    void LateUpdate()
    {
        if (!Application.isPlaying || IsFlipping) return;
        RestoreSorting();
        PinCountPanel(); // 쉬는 동안 카드 우측상단에 고정 (해상도 변화도 자동 추종)
    }

    // ── 에디터 프리뷰: 인스펙터 값/가운데 위치 변화 시 종이를 슬롯으로 즉시 배치 (변화 있을 때만 적용) ──
#if UNITY_EDITOR
    Vector2 _lastCenter; float _lastOX, _lastOY, _lastS, _lastT; Vector2 _lastCountOff; bool _previewValid;
    void Update()
    {
        if (Application.isPlaying) return;
        if (_center == null) _center = (RectTransform)transform;
        Vector2 c = _center.anchoredPosition;
        if (_previewValid && c == _lastCenter && _lastOX == sideOffsetX && _lastOY == sideOffsetY && _lastS == sideScale && _lastT == sideTiltZ && _lastCountOff == countOffsetFromTopLeft)
            return;
        if (leftPaper  != null) { var pl = LeftSlot(c);  leftPaper.anchoredPosition3D  = new Vector3(pl.x, pl.y, 0f); leftPaper.localScale  = SideScaleV; leftPaper.localEulerAngles  = LeftRot; }
        if (rightPaper != null) { var pr = RightSlot(c); rightPaper.anchoredPosition3D = new Vector3(pr.x, pr.y, 0f); rightPaper.localScale = SideScaleV; rightPaper.localEulerAngles = RightRot; }
        PinCountPanel(); // 에디터에서도 카드 우측상단 고정 위치 미리보기 (-110x 조절 시 즉시 반영)
        _lastCenter = c; _lastOX = sideOffsetX; _lastOY = sideOffsetY; _lastS = sideScale; _lastT = sideTiltZ; _lastCountOff = countOffsetFromTopLeft; _previewValid = true;
    }
#endif
}
