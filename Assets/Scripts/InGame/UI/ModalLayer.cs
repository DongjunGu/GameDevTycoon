using UnityEngine;
using UnityEngine.UI;

// 모달 패널에 붙이는 컴포넌트 — "Show 시 SetActive(true) 되는" 패널(=토글 대상 오브젝트)에 부착한다.
// 활성화되는 동안 ModalBlocker 에 등록되어, 자신을 블로커 위 정렬(overrideSorting)로 올리고
// 뒤 UI 는 블로커가 가려 클릭이 차단된다. 비활성화 시 등록 해제.
//
// 모달 자체 스크립트는 건드리지 않는다 (OnEnable/OnDisable 로 동작). 인게임 모달에만 부착.
[DisallowMultipleComponent]
public class ModalLayer : MonoBehaviour
{
    [Tooltip("켜면 이 모달이 떠 있는 동안 배경 블러를 적용. 끄면 입력 차단(dim)만. 다이얼로그/이벤트만 켠다.")]
    [SerializeField] bool _useBlur = false;
    public bool UseBlur => _useBlur;

    // 켜면 이 모달은 "공유 딤(ModalBlocker 전역 어둡게+차단)"을 자기 밑으로 끌어올리지 않는다.
    // AlertUI(1~6) 처럼 자체 반투명 배경 + EnsureTopMost(항상 최상단)를 이미 갖춘 "패널 위에 얹히는 정보 팝업"용.
    // 끄지 않으면(false) 이 모달이 스택 최상단이 될 때마다 공유 딤이 바로 밑 패널까지 밀어올려 그 패널이
    // (특히 어두운 배경색 UI일 때) 거의 안 보이게 덮여버린다 — DispatchPanel 위에 AlertUI3 뜰 때 재현된 버그.
    [Tooltip("켜면 이 모달이 최상단이어도 공유 딤을 자기 밑으로 끌어올리지 않음 (자체 반투명 배경이 있는 정보 팝업용, 예: AlertUI)")]
    [SerializeField] bool _excludeFromSharedDim = false;
    public bool ExcludeFromSharedDim => _excludeFromSharedDim;

    public int AssignedOrder { get; private set; }

    Canvas _canvas;
    bool   _hadCanvas;

    void OnEnable()
    {
        ModalBlocker.Instance.Register(this);
    }

    void OnDisable()
    {
        if (ModalBlocker.HasInstance) ModalBlocker.Instance.Unregister(this);
        // 정렬 오버라이드 원복 (숨겨진 동안 다른 정렬에 영향 X)
        if (_canvas != null && !_hadCanvas) _canvas.overrideSorting = false;
    }

    // ModalBlocker 가 호출 — 이 모달을 블로커 위 order 로 올림 (중첩 Canvas overrideSorting).
    public void ApplyOrder(int order)
    {
        AssignedOrder = order;
        if (_canvas == null)
        {
            _canvas = GetComponent<Canvas>();
            _hadCanvas = _canvas != null;          // 원래 Canvas 가 있던 패널이면 비활성 시 건드리지 않음
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
        }
        _canvas.overrideSorting = true;
        _canvas.sortingOrder    = order;
        // 중첩 Canvas 위 그래픽도 클릭 받도록 자체 레이캐스터 보장 (없을 때만)
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
    }
}
