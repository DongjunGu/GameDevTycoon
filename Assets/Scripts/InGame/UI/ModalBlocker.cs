using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
    const float DIM_ALPHA  = 0.45f; // 배경 어둡기 (약간 dim)

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

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureBlocker();
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

        _image = gameObject.AddComponent<Image>();
        _image.color = new Color(0f, 0f, 0f, DIM_ALPHA);
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

        if (_stack.Count == 0) { _canvas.enabled = false; return; }

        // 등록 순서대로 BASE+2, +4, ... 재할당 → 마지막(맨 위) 모달만 블로커 위, 나머지는 아래로 차단.
        if (_canvas.worldCamera == null) _canvas.worldCamera = Camera.main; // 씬 전환 후 카메라 재참조
        for (int i = 0; i < _stack.Count; i++)
            _stack[i].ApplyOrder(BASE_ORDER + (i + 1) * STEP);

        _canvas.sortingOrder = _stack[_stack.Count - 1].AssignedOrder - 1;
        _canvas.enabled = true;
        _image.transform.SetAsLastSibling();
    }
}
