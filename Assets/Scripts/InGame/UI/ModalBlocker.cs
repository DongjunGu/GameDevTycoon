using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

// 모달 입력 차단 매니저 (인게임 전용).
//
// 인게임 UI 가 여러 캔버스(Menu=10 / HUD=11 / Dialog=13 / Creativity=14)에 흩어져 있어,
// 패널마다 배경을 까는 방식으로는 다른 캔버스의 버튼을 못 막는다. 그래서:
//   - 전용 캔버스(ScreenSpaceCamera, sortingOrder 높음)에 풀스크린 dim+raycast 이미지를 두고,
//   - 모달이 열리면 그 모달만 블로커 "위" 정렬(overrideSorting)로 올린다.
// → 열린 모달 중 가장 위(마지막 등록)만 상호작용 가능, 그 아래 모든 UI(다른 모달·HUD·메뉴 포함)는 클릭 차단.
//
// 모달은 ModalLayer 컴포넌트로 Register/Unregister 한다. 씬 배치 불필요(런타임 자동 생성).
public class ModalBlocker : MonoBehaviour
{
    const int   BASE_ORDER = 50;   // 인게임 캔버스(10~14)보다 충분히 높은 기준 정렬값
    const int   STEP       = 2;    // 모달마다 +2 (모달 order, 블로커는 그 바로 아래)

    [Header("Dim 배경 어둡기 (0~1, 클수록 진함)")]
    [Range(0f, 1f)]
    [SerializeField] float dimAlpha = 0.7f; // 기존 고정값(0.30) 대비 기본을 더 진하게 + 조절 가능하게 노출
                                             // 런타임 자동생성 오브젝트라 Play 모드에서 Hierarchy 의 [ModalBlocker] 선택 후 조절

    // 배경 블러 (모달 뒤 화면). 모달=시간정지라 열릴 때 1회만 렌더. RT 는 화면 1/4 다운샘플.
    const float BLUR_SIZE  = 1.0f;  // 셰이더 _BlurSize (px)

    static ModalBlocker _instance;
    public static bool HasInstance => _instance != null;
    public static ModalBlocker Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[ModalBlocker]");
                _instance = go.AddComponent<ModalBlocker>();
            }
            return _instance;
        }
    }

    Canvas _canvas;
    Image  _image;
    readonly List<ModalLayer> _stack = new();

    // 블러
    RawImage      _blurImage;
    RenderTexture _blurRT;
    Material      _blurMat;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureBlocker();
    }

    // Play 모드에서 Hierarchy 의 [ModalBlocker] 를 선택해 dimAlpha 슬라이더를 조절하면 즉시 반영.
    void OnValidate()
    {
        if (_image != null) _image.color = new Color(0f, 0f, 0f, dimAlpha);
    }

    void EnsureBlocker()
    {
        if (_canvas != null) return;

        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceCamera;
        _canvas.worldCamera = Camera.main;       // 인게임 캔버스와 동일 카메라(스크린스페이스-카메라) 정렬
        _canvas.planeDistance = 100f;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = BASE_ORDER;
        gameObject.AddComponent<GraphicRaycaster>();

        // 블러(_blurImage)와의 렌더 순서를 sibling index 로 확실히 제어하기 위해 딤도 캔버스의 "자식"으로 둔다.
        // (루트(캔버스) 자신에 바로 붙이면 자식인 블러가 항상 그 위에 그려져서 SetAsLastSibling 이 무의미해짐)
        var dimGo = new GameObject("DimBG");
        dimGo.transform.SetParent(_canvas.transform, false);
        _image = dimGo.AddComponent<Image>();
        _image.color = new Color(0f, 0f, 0f, dimAlpha);
        _image.raycastTarget = true;             // 알파 0 이어도 raycast 는 막힘 (여기선 dim 까지)
        var rt = (RectTransform)_image.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        _canvas.enabled = false;                 // 등록된 모달이 없으면 꺼둠
    }

    public void Register(ModalLayer layer)
    {
        if (layer != null && !_stack.Contains(layer)) _stack.Add(layer);
        Refresh();
    }

    public void Unregister(ModalLayer layer)
    {
        _stack.Remove(layer);
        Refresh();
    }

    void Refresh()
    {
        _stack.RemoveAll(l => l == null);
        EnsureBlocker();

        if (_stack.Count == 0)
        {
            _canvas.enabled = false;
            if (_blurImage != null) _blurImage.enabled = false;
            return;
        }

        // 등록 순서대로 BASE+2, +4, ... 재할당 → 마지막(맨 위) 모달만 블로커 위, 나머지는 아래로 차단.
        if (_canvas.worldCamera == null) _canvas.worldCamera = Camera.main; // 씬 전환 후 카메라 재참조
        for (int i = 0; i < _stack.Count; i++)
            _stack[i].ApplyOrder(BASE_ORDER + (i + 1) * STEP);

        // 공유 딤은 ExcludeFromSharedDim 인 모달(예: AlertUI — 자체 반투명 배경 있음)을 건너뛰고,
        // 그 아래에서 가장 위에 있는 "일반" 모달 바로 밑에 놓는다. 전부 제외 대상이면 딤 자체를 끈다
        // (그 모달들이 각자 자기 배경으로 이미 가리고 있으므로 공유 딤이 필요 없음).
        int dimBelowOrder = -1;
        for (int i = _stack.Count - 1; i >= 0; i--)
        {
            if (_stack[i] != null && !_stack[i].ExcludeFromSharedDim) { dimBelowOrder = _stack[i].AssignedOrder; break; }
        }
        if (dimBelowOrder < 0)
        {
            _canvas.enabled = false;
            if (_blurImage != null) _blurImage.enabled = false;
            return;
        }

        _canvas.sortingOrder = dimBelowOrder - 1;
        _canvas.enabled = true;

        // 블러를 요청한 모달(UseBlur)이 하나라도 떠 있을 때만 블러 적용.
        bool wantBlur = false;
        for (int i = 0; i < _stack.Count; i++)
            if (_stack[i] != null && _stack[i].UseBlur) { wantBlur = true; break; }

        if (wantBlur)
        {
            // 블러가 막 켜지는 순간에만 1회 렌더 (모달=시간정지라 정적).
            // ApplyOrder 로 모달 Canvas 가 생성된 뒤라야 RenderBlur 가 모달을 숨겨 캡처에서 제외할 수 있음.
            bool blurWasOn = _blurImage != null && _blurImage.enabled;
            if (!blurWasOn) RenderBlur();
            if (_blurImage != null) { _blurImage.transform.SetAsFirstSibling(); _blurImage.enabled = true; } // dim 뒤(아래)
        }
        else if (_blurImage != null)
        {
            _blurImage.enabled = false;
        }

        _image.transform.SetAsLastSibling();
    }

    // 블러용 RT/카메라/RawImage 준비 (런타임 1회 생성).
    void EnsureBlur()
    {
        if (_blurMat == null)
        {
            // 사용자가 만든 머티리얼 우선(Resources/BlurMaterial), 없으면 셰이더로 직접 생성.
            _blurMat = Resources.Load<Material>("BlurMaterial");
            if (_blurMat == null)
            {
                var sh = Shader.Find("UI/GaussianBlur");
                if (sh != null)
                {
                    _blurMat = new Material(sh);
                    _blurMat.SetFloat("_BlurSize", BLUR_SIZE);
                }
            }
        }

        if (_blurRT == null)
        {
            // 화면 비율 유지하며 1/4 다운샘플 (다운샘플 자체가 블러 강화). depth=24 (SubmitRenderRequest 요구).
            int w = Mathf.Max(64, Screen.width  / 4);
            int h = Mathf.Max(64, Screen.height / 4);
            _blurRT = new RenderTexture(w, h, 24, RenderTextureFormat.Default)
            {
                filterMode = FilterMode.Bilinear,
                name = "[ModalBlurRT]"
            };
            _blurRT.Create();
        }

        if (_blurImage == null && _canvas != null)
        {
            var go = new GameObject("BlurBG");
            go.transform.SetParent(_canvas.transform, false);
            _blurImage = go.AddComponent<RawImage>();
            _blurImage.material      = _blurMat;
            _blurImage.texture       = _blurRT;
            _blurImage.raycastTarget = false;
            var rt = (RectTransform)_blurImage.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            _blurImage.transform.SetAsFirstSibling();          // dim 보다 뒤(아래)
        }
    }

    // 현재 화면(게임 + 인게임 UI)을 RT 에 1회 렌더 → 블러 RawImage 에 표시.
    // 모달/블로커 캔버스는 캡처 직전 잠깐 꺼서 블러에서 제외 (모달은 선명 유지, 피드백 루프 방지).
    void RenderBlur()
    {
        EnsureBlur();
        var src = Camera.main;
        if (_blurRT == null || src == null) return;

        // 1) 블러에 포함되면 안 되는 캔버스 숨김: 블로커(dim+블러이미지) + 현재 떠 있는 모달들
        bool blockerWas = _canvas.enabled;
        _canvas.enabled = false;
        var hidden = new List<Canvas>();
        foreach (var layer in _stack)
        {
            if (layer == null) continue;
            // 모달 자신 + 하위의 모든 Canvas(중첩 overrideSorting 캔버스 포함)를 캡처에서 제외.
            // 캐러셀 종이처럼 자체 Canvas 를 가진 자식이 블러 배경에 섞여 들어가는 것을 방지.
            foreach (var c in layer.GetComponentsInChildren<Canvas>(false))
                if (c != null && c.enabled) { c.enabled = false; hidden.Add(c); }
        }

        // 2) 메인 카메라를 RT 로 1회 렌더 (게임 + ScreenSpaceCamera UI 포함). 카메라 팬/줌도 반영됨.
        var req = new RenderPipeline.StandardRequest();
        if (RenderPipeline.SupportsRenderRequest(src, req))
        {
            req.destination = _blurRT;
            RenderPipeline.SubmitRenderRequest(src, req);
        }

        // 3) 복원
        foreach (var c in hidden) c.enabled = true;
        _canvas.enabled = blockerWas;

        if (_blurImage != null)
        {
            if (_blurImage.material == null) _blurImage.material = _blurMat;
            _blurImage.texture = _blurRT;
        }
    }
}
