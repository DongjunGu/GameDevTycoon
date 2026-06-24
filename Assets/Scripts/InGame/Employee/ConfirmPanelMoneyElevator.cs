using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ConfirmHirePanel 에 부착한다. 이 패널이 떠 있는 동안 확정화면의 정렬(레이어) 스택을 관리한다.
//
// 모달(ModalLayer/ModalBlocker)이 ConfirmHirePanel 을 order b(예: 52, 블로커는 b-1=51)로 올린다.
// ResumeFlipper 가 이력서 카드(종이)를 b(쉴 때)/b+1(넘기는 중)로 둔다.
// → 그 위에 와야 하는 것들을 b 기준으로 더 높은 order 로 끌어올린다:
//     카드 = b ~ b+1  /  MoneyPanel = b+MONEY  /  topObjects(ConfirmUI/AlertUI) = b+TOP (최상단)
// b 는 매 프레임 다시 읽어(LateUpdate) 모달 등록 타이밍/스택 변화에도 맞춘다.
//
// 추가로, 이 패널이 떠 있는 동안 HUDCanvas 자식들을 MoneyPanel 만 남기고 숨겼다가 닫히면 복구한다.
[DisallowMultipleComponent]
public class ConfirmPanelMoneyElevator : MonoBehaviour
{
    const int MONEY_OFFSET = 4; // MoneyPanel
    const int TOP_OFFSET   = 6; // 최상단 — 다이얼로그(ConfirmUI/AlertUI)

    [Tooltip("MoneyPanel (HUDCanvas 자식). b+4 로 정렬 + HUD 숨김에서 제외.")]
    [SerializeField] GameObject moneyPanel;
    [Tooltip("최상단으로 올릴 것들 — ConfirmUI / AlertUI 등. b+6(모두 위)로 정렬 + HUD 숨김에서도 제외.")]
    [SerializeField] GameObject[] topObjects;
    [Tooltip("이 패널이 떠 있는 동안 자식들을 숨길 HUDCanvas. MoneyPanel/topObjects(및 그 컨테이너)은 제외하고 모두 비활성, 닫히면 복구.")]
    [SerializeField] Transform hudCanvas;
    [Tooltip("정렬 기준 — 가운데 이력서 카드(EmployeeResumePanel)의 Canvas. 이 카드의 layer/order 를 기준으로 그 위로 올린다.")]
    [SerializeField] Canvas referenceCanvas;

    // 끌어올린 대상의 원상복구 정보
    class Raised
    {
        public GameObject go;
        public int orderOffset;       // b 로부터의 오프셋
        public Canvas canvas;
        public GraphicRaycaster addedRay;
        public bool hadCanvas;
        public bool prevOverride;
        public int  prevOrder;
        public int  prevLayer;
    }
    readonly List<Raised> _raised = new();
    readonly List<GameObject> _hiddenHudChildren = new();
    readonly List<Transform> _keepTargets = new();

    void OnEnable()
    {
        BuildRaised();
        ApplyOrders();
        HideHudExceptMoney();
    }

    void OnDisable()
    {
        RestoreRaised();
        RestoreHud();
    }

    // 모달 등록이 OnEnable 직후일 수 있어(타이밍), 매 프레임 b 를 다시 읽어 정렬 반영.
    void LateUpdate()
    {
        if (_raised.Count > 0) ApplyOrders();
    }

    // ── 정렬 스택 ─────────────────────────────────────────
    // 기준 = 가운데 카드(referenceCanvas)의 현재 order/layer. ResumeFlipper 가 매 프레임 갱신하는 값이라
    // 버튼·MoneyPanel 을 "카드와 같은 layer 에서 카드보다 높은 order" 로 올리면 항상 카드 위에 온다.
    int BaseOrder() => referenceCanvas != null ? referenceCanvas.sortingOrder
                     : (GetComponent<Canvas>() != null ? GetComponent<Canvas>().sortingOrder : 1);
    int BaseLayerId() => referenceCanvas != null ? referenceCanvas.sortingLayerID : 0;

    void BuildRaised()
    {
        _raised.Clear();
        AddRaised(moneyPanel, MONEY_OFFSET);
        if (topObjects != null)
            foreach (var go in topObjects) AddRaised(go, TOP_OFFSET);
    }

    void AddRaised(GameObject go, int offset)
    {
        if (go == null) return;
        var r = new Raised { go = go, orderOffset = offset };
        r.canvas = go.GetComponent<Canvas>();
        r.hadCanvas = r.canvas != null;
        if (r.canvas == null) r.canvas = go.AddComponent<Canvas>();
        r.prevOverride = r.canvas.overrideSorting;
        r.prevOrder    = r.canvas.sortingOrder;
        r.prevLayer    = r.canvas.sortingLayerID;
        // 중첩 Canvas 위 그래픽 클릭 유지용 레이캐스터 (없을 때만 추가)
        if (go.GetComponent<GraphicRaycaster>() == null) r.addedRay = go.AddComponent<GraphicRaycaster>();
        _raised.Add(r);
    }

    void ApplyOrders()
    {
        int b = BaseOrder();
        int layer = BaseLayerId();
        for (int i = 0; i < _raised.Count; i++)
        {
            var r = _raised[i];
            if (r.canvas == null) continue;
            r.canvas.overrideSorting = true;
            r.canvas.sortingLayerID  = layer;          // 카드와 같은 레이어 (order 비교가 유효하도록)
            r.canvas.sortingOrder    = b + r.orderOffset;
        }
    }

    void RestoreRaised()
    {
        for (int i = 0; i < _raised.Count; i++)
        {
            var r = _raised[i];
            if (r.canvas == null) continue;
            if (r.hadCanvas)
            {
                r.canvas.overrideSorting = r.prevOverride;
                r.canvas.sortingOrder    = r.prevOrder;
                r.canvas.sortingLayerID  = r.prevLayer;
            }
            else
            {
                if (r.addedRay != null) Destroy(r.addedRay);
                Destroy(r.canvas);
            }
        }
        _raised.Clear();
    }

    // ── HUDCanvas 자식 숨김 (MoneyPanel + topObjects 제외) ──
    void HideHudExceptMoney()
    {
        _hiddenHudChildren.Clear();
        if (hudCanvas == null) return;

        // 유지(숨기지 않을) 대상: MoneyPanel + topObjects(ConfirmUI/AlertUI)
        _keepTargets.Clear();
        if (moneyPanel != null) _keepTargets.Add(moneyPanel.transform);
        if (topObjects != null)
            foreach (var go in topObjects) if (go != null) _keepTargets.Add(go.transform);

        HideSiblingsAlongPath(hudCanvas);
    }

    // node 의 자식들 중 "유지 대상" 경로가 아닌 것만 비활성화.
    // 유지 대상이 중첩(컨테이너 안)돼 있어도, 조상 컨테이너는 켜둔 채 그 안의 "형제"들만 끈다.
    void HideSiblingsAlongPath(Transform node)
    {
        for (int i = 0; i < node.childCount; i++)
        {
            var child = node.GetChild(i);

            bool isKeep = false, isAncestorOfKeep = false;
            for (int k = 0; k < _keepTargets.Count; k++)
            {
                var t = _keepTargets[k];
                if (t == child) { isKeep = true; break; }
                if (t.IsChildOf(child)) isAncestorOfKeep = true;   // child 가 유지대상의 상위 컨테이너
            }

            if (isKeep) continue;                                  // 유지 대상 자체 — 그대로 둠
            if (isAncestorOfKeep) { HideSiblingsAlongPath(child); continue; } // 컨테이너는 켜두고 내부만 처리
            if (!child.gameObject.activeSelf) continue;            // 이미 꺼진 건 건드리지 않음 (복구 대상 아님)
            child.gameObject.SetActive(false);
            _hiddenHudChildren.Add(child.gameObject);
        }
    }

    void RestoreHud()
    {
        for (int i = 0; i < _hiddenHudChildren.Count; i++)
            if (_hiddenHudChildren[i] != null) _hiddenHudChildren[i].SetActive(true);
        _hiddenHudChildren.Clear();
    }
}
