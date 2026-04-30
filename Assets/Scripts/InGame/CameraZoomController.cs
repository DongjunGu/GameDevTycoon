using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Orthographic 카메라 줌 컨트롤러
// - 시뮬레이터/실기기: 두 손가락 핀치
// - 에디터/PC: 마우스 휠
// - UI 위에서는 입력 무시 (옵션)
// - GameTimeManager 정지 중에도 동작 (Time.unscaledDeltaTime)
[RequireComponent(typeof(Camera))]
public class CameraZoomController : MonoBehaviour
{
    [Header("Zoom Range (orthographicSize)")]
    public float minSize = 2f;
    public float maxSize = 5.5f;

    [Header("Sensitivity")]
    [Tooltip("핀치 거리 1픽셀당 size 변화량")]
    public float pinchSensitivity = 0.01f;
    [Tooltip("마우스 휠 한 틱당 size 변화량")]
    public float wheelSensitivity = 0.4f;

    [Header("Smoothing")]
    [Tooltip("0이면 즉시 적용, >0이면 지수 보간 시간상수(초)")]
    public float smoothing = 0.12f;

    [Header("UI 위 입력 무시")]
    public bool ignoreOverUIForWheel = true;
    public bool ignoreOverUIForPinch = false;
    public bool ignoreOverUIForPan   = true;

    [Header("Pan (드래그)")]
    [Tooltip("PC: 마우스 좌버튼 드래그 / 모바일: 한 손가락 드래그")]
    public bool enablePan = true;
    [Tooltip("드래그 시작이 이 값 이상 움직였을 때부터 카메라 이동 (픽셀)")]
    public float panMoveThreshold = 4f;

    [Header("Clamp (월드 영역)")]
    [Tooltip("카메라 viewport가 이 영역 내에 머물도록 제한")]
    public bool enableClamp = true;
    public Vector2 worldMin = new Vector2(-10f, -5f);
    public Vector2 worldMax = new Vector2( 10f,  5f);

    Camera _cam;
    float _targetSize;
    float _prevPinchDistance;
    bool _pinching;

    bool _panActive;          // 한 번 시작되면 release까지 true
    bool _panMoved;           // threshold 넘어 실제 이동 시작했는지
    Vector2 _panLastScreenPos;

    static readonly List<RaycastResult> _raycastResults = new();

    void Awake()
    {
        _cam = GetComponent<Camera>();
        _targetSize = _cam.orthographicSize;
    }

    void Update()
    {
        HandlePinch();
        HandleWheel();
        HandlePan();

        _targetSize = Mathf.Clamp(_targetSize, minSize, maxSize);

        if (smoothing > 0f)
        {
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / smoothing);
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _targetSize, k);
        }
        else
        {
            _cam.orthographicSize = _targetSize;
        }

        ApplyClamp();
    }

    void ApplyClamp()
    {
        if (!enableClamp) return;

        float halfH = _cam.orthographicSize;
        float halfW = halfH * _cam.aspect;

        Vector3 p = transform.position;

        // 영역이 viewport보다 작으면 중점에 고정, 크면 가장자리 클램프
        float spanX = worldMax.x - worldMin.x;
        if (spanX <= halfW * 2f)
            p.x = (worldMin.x + worldMax.x) * 0.5f;
        else
            p.x = Mathf.Clamp(p.x, worldMin.x + halfW, worldMax.x - halfW);

        float spanY = worldMax.y - worldMin.y;
        if (spanY <= halfH * 2f)
            p.y = (worldMin.y + worldMax.y) * 0.5f;
        else
            p.y = Mathf.Clamp(p.y, worldMin.y + halfH, worldMax.y - halfH);

        transform.position = p;
    }

#if UNITY_EDITOR
    [Header("Editor")]
    [Tooltip("씬 뷰에 클램프 영역을 항상 표시")]
    public bool drawGizmoAlways = true;
    public Color gizmoColor = new Color(0f, 1f, 1f, 0.8f);

    void OnDrawGizmos()
    {
        if (!enableClamp || !drawGizmoAlways) return;
        DrawClampGizmo();
    }

    void OnDrawGizmosSelected()
    {
        if (!enableClamp || drawGizmoAlways) return; // Always 모드면 OnDrawGizmos에서 이미 그림
        DrawClampGizmo();
    }

    void DrawClampGizmo()
    {
        Gizmos.color = gizmoColor;
        Vector3 bl = new Vector3(worldMin.x, worldMin.y, 0f);
        Vector3 br = new Vector3(worldMax.x, worldMin.y, 0f);
        Vector3 tl = new Vector3(worldMin.x, worldMax.y, 0f);
        Vector3 tr = new Vector3(worldMax.x, worldMax.y, 0f);
        Gizmos.DrawLine(bl, br);
        Gizmos.DrawLine(br, tr);
        Gizmos.DrawLine(tr, tl);
        Gizmos.DrawLine(tl, bl);

        // 모서리 표시 (코너 작은 십자)
        float t = 0.3f;
        Gizmos.DrawLine(bl + Vector3.right * t,  bl - Vector3.right * t);
        Gizmos.DrawLine(bl + Vector3.up    * t,  bl - Vector3.up    * t);
        Gizmos.DrawLine(tr + Vector3.right * t,  tr - Vector3.right * t);
        Gizmos.DrawLine(tr + Vector3.up    * t,  tr - Vector3.up    * t);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(bl, $"Min ({worldMin.x:F1}, {worldMin.y:F1})");
        UnityEditor.Handles.Label(tr, $"Max ({worldMax.x:F1}, {worldMax.y:F1})");
#endif
    }
#endif

    void HandlePan()
    {
        if (!enablePan) { _panActive = false; _panMoved = false; return; }
        if (_pinching)  { _panActive = false; _panMoved = false; return; }

        // 1차: 활성 입력 위치 가져오기 (한 손가락 우선, 없으면 마우스 좌버튼)
        bool pressed = false;
        Vector2 pos = Vector2.zero;

        var touch = Touchscreen.current;
        if (touch != null)
        {
            int activeCount = 0;
            Vector2 first = Vector2.zero;
            foreach (var t in touch.touches)
            {
                if (!t.press.isPressed) continue;
                if (activeCount == 0) first = t.position.ReadValue();
                activeCount++;
                if (activeCount >= 2) break;
            }
            if (activeCount == 1)
            {
                pressed = true;
                pos = first;
            }
        }
        if (!pressed)
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                pressed = true;
                pos = mouse.position.ReadValue();
            }
        }

        if (!pressed)
        {
            _panActive = false;
            _panMoved = false;
            return;
        }

        if (!_panActive)
        {
            // 시작 시점에 UI 위였으면 이번 누름 동안 팬 비활성
            if (ignoreOverUIForPan && IsPointerOverUI(pos)) return;
            _panActive = true;
            _panMoved = false;
            _panLastScreenPos = pos;
            return;
        }

        Vector2 delta = pos - _panLastScreenPos;
        if (!_panMoved)
        {
            if (delta.sqrMagnitude < panMoveThreshold * panMoveThreshold) return;
            _panMoved = true;
        }

        // 픽셀 → 월드 변환 (orthographic 기준 화면 높이당 size*2 월드)
        float worldPerPixel = (_cam.orthographicSize * 2f) / Mathf.Max(1, Screen.height);
        Vector3 worldDelta = new Vector3(delta.x * worldPerPixel, delta.y * worldPerPixel, 0f);
        // 손가락이 오른쪽으로 가면 월드는 왼쪽으로 가야 화면이 따라가는 느낌 → 카메라는 반대로 이동
        transform.position -= worldDelta;
        _panLastScreenPos = pos;
    }

    void HandlePinch()
    {
        var touch = Touchscreen.current;
        if (touch == null) { _pinching = false; return; }

        Vector2 p0 = Vector2.zero, p1 = Vector2.zero;
        int activeCount = 0;
        foreach (var t in touch.touches)
        {
            if (!t.press.isPressed) continue;
            if (activeCount == 0) p0 = t.position.ReadValue();
            else if (activeCount == 1) p1 = t.position.ReadValue();
            activeCount++;
            if (activeCount >= 2) break;
        }

        if (activeCount < 2) { _pinching = false; return; }

        // UI 위 핀치 무시 옵션 — 두 점 중 하나라도 UI 위면 무시
        if (ignoreOverUIForPinch && (IsPointerOverUI(p0) || IsPointerOverUI(p1)))
        {
            _pinching = false;
            return;
        }

        float dist = Vector2.Distance(p0, p1);
        if (!_pinching)
        {
            _pinching = true;
            _prevPinchDistance = dist;
            return;
        }

        float delta = dist - _prevPinchDistance;
        _targetSize -= delta * pinchSensitivity; // 거리가 늘면 확대(=size 감소)
        _prevPinchDistance = dist;
    }

    void HandleWheel()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (ignoreOverUIForWheel && IsPointerOverUI(mouse.position.ReadValue())) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.01f) return;

        // OS별로 scroll 단위가 다르므로 부호만 사용
        _targetSize -= Mathf.Sign(scroll) * wheelSensitivity;
    }

    // 진짜 UI(Canvas의 GraphicRaycaster) 위에 있을 때만 true.
    // Physics2DRaycaster가 잡는 2D 게임 오브젝트(캐릭터/책상 등)는 제외.
    bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        var ped = new PointerEventData(EventSystem.current) { position = screenPos };
        _raycastResults.Clear();
        EventSystem.current.RaycastAll(ped, _raycastResults);
        for (int i = 0; i < _raycastResults.Count; i++)
        {
            if (_raycastResults[i].module is UnityEngine.UI.GraphicRaycaster)
                return true;
        }
        return false;
    }
}
