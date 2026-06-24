using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 패널(ConfirmHirePanel / EmployeePanel 등)에 부착해, 그 패널이 떠 있는 동안
// (1) MoneyPanel·다이얼로그를 그 패널 위로 끌어올리고 (2) HUD/지정 오브젝트를 숨겼다 닫히면 복구한다.
// SetActive 토글되는 패널이면 어디든 재사용 가능 (OnEnable/OnDisable 로 동작).
//
// 기준 order b = referenceCanvas.sortingOrder (없으면 이 오브젝트의 Canvas.sortingOrder).
//   · ConfirmHirePanel: referenceCanvas = 가운데 이력서 카드(ResumeFlipper 가 b/b+1 로 둠) → 그 위로 올림.
//   · EmployeePanel 등 카드 없는 패널: referenceCanvas = 그 패널의 Canvas(또는 비우면 자기 Canvas).
// → MoneyPanel = b+MONEY, topObjects(ConfirmUI/AlertUI) = b+TOP(최상단). 같은 sortingLayer 복사.
// b 는 매 프레임 다시 읽어(LateUpdate) 모달 등록 타이밍/스택 변화에도 맞춘다.
//
// HUD 숨김: hudCanvas 자식들을 MoneyPanel/topObjects(및 컨테이너) 제외하고 비활성 + alsoHide(HUD 밖) → 닫히면 복구.
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
    [Tooltip("HUDCanvas 밖이지만 같이 숨길 것들 — EmployeeStatusPanel 등. 패널 열릴 때 비활성, 닫히면 복구.")]
    [SerializeField] GameObject[] alsoHide;
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
    // 기준 = referenceCanvas(있으면) 의 order/layer, 없으면 이 패널을 실제로 지배하는 캔버스.
    //   · ConfirmHirePanel: referenceCanvas=가운데 카드 → 그 위로.
    //   · EmployeePanel 등: referenceCanvas 비우면 패널이 속한 실제 렌더 캔버스(예: MenuCanvas order 12) 를 찾아 그 위로.
    // ⚠️ 단순 GetComponent<Canvas>().sortingOrder 는 overrideSorting=false 면 0(필드값)이라 실제 order 가 아님 →
    //    overrideSorting=true 이거나 루트인 "지배 캔버스" 까지 위로 올라가 그 order/layer 를 쓴다.
    Canvas EffectiveCanvas()
    {
        if (referenceCanvas != null) return referenceCanvas;
        var c = GetComponentInParent<Canvas>();
        while (c != null && !c.overrideSorting)
        {
            var parent = c.transform.parent;
            var up = parent != null ? parent.GetComponentInParent<Canvas>() : null;
            if (up == null || up == c) break;   // 루트 도달
            c = up;
        }
        return c;
    }
    int BaseOrder()   { var c = EffectiveCanvas(); return c != null ? c.sortingOrder   : 1; }
    int BaseLayerId() { var c = EffectiveCanvas(); return c != null ? c.sortingLayerID : 0; }

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

    // ── HUDCanvas 자식 숨김 (MoneyPanel + topObjects 제외) + alsoHide ──
    void HideHudExceptMoney()
    {
        _hiddenHudChildren.Clear();

        if (hudCanvas != null)
        {
            // 유지(숨기지 않을) 대상: MoneyPanel + topObjects(ConfirmUI/AlertUI)
            _keepTargets.Clear();
            if (moneyPanel != null) _keepTargets.Add(moneyPanel.transform);
            if (topObjects != null)
                foreach (var go in topObjects) if (go != null) _keepTargets.Add(go.transform);

            HideSiblingsAlongPath(hudCanvas);
        }

        // HUDCanvas 밖이지만 같이 숨길 것들 (EmployeeStatusPanel 등)
        if (alsoHide != null)
            foreach (var go in alsoHide)
            {
                if (go == null || !go.activeSelf) continue; // 이미 꺼진 건 패스(복구 대상 아님)
                go.SetActive(false);
                _hiddenHudChildren.Add(go);
            }
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
