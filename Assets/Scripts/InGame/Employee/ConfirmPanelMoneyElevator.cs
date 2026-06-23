using UnityEngine;
using UnityEngine.UI;

// ConfirmHirePanel 에 부착한다. 이 패널이 떠 있는 동안 MoneyPanel 을
// referenceCanvas(EmployeeResumePanel)와 동일한 sortingLayer/sortingOrder 로 끌어올려
// 확정 화면(모달 dim) 위에 보이게 한다. 패널이 닫히면(SetActive(false)) 원래 정렬로 원복.
//
// MoneyPanel 은 HUDCanvas(UI 레이어, order 11) 자식이라 ConfirmHirePanel(MenuCanvas)의
// dim 배경 + EmployeeResumePanel(Default 레이어) 아래로 가려진다.
// → EmployeeResumePanel 과 같은 정렬로 올려 함께 위에 뜨게 한다.
[DisallowMultipleComponent]
public class ConfirmPanelMoneyElevator : MonoBehaviour
{
    [Tooltip("끌어올릴 대상 — MoneyPanel (HUDCanvas 자식, 자체 Canvas 없음)")]
    [SerializeField] GameObject moneyPanel;
    [Tooltip("정렬 기준 — EmployeeResumePanel 의 Canvas (동일 sortingLayer/order 를 복사)")]
    [SerializeField] Canvas referenceCanvas;

    Canvas _moneyCanvas;
    GraphicRaycaster _addedRaycaster;
    bool _hadCanvas;
    bool _prevOverride;
    int  _prevOrder;
    int  _prevLayerID;

    void OnEnable()
    {
        if (moneyPanel == null || referenceCanvas == null) return;

        _moneyCanvas = moneyPanel.GetComponent<Canvas>();
        _hadCanvas = _moneyCanvas != null;
        if (_moneyCanvas == null) _moneyCanvas = moneyPanel.AddComponent<Canvas>();

        _prevOverride = _moneyCanvas.overrideSorting;
        _prevOrder    = _moneyCanvas.sortingOrder;
        _prevLayerID  = _moneyCanvas.sortingLayerID;

        // EmployeeResumePanel 과 동일한 정렬 (Default 레이어 + order 1) 로 끌어올림
        _moneyCanvas.overrideSorting = true;
        _moneyCanvas.sortingLayerID  = referenceCanvas.sortingLayerID;
        _moneyCanvas.sortingOrder    = referenceCanvas.sortingOrder;

        // 중첩 Canvas 위 그래픽의 클릭 유지용 레이캐스터 (없을 때만 추가)
        if (moneyPanel.GetComponent<GraphicRaycaster>() == null)
            _addedRaycaster = moneyPanel.AddComponent<GraphicRaycaster>();
    }

    void OnDisable()
    {
        if (_moneyCanvas == null) return;

        if (_hadCanvas)
        {
            // 원래 Canvas 가 있던 패널이면 값만 원복
            _moneyCanvas.overrideSorting = _prevOverride;
            _moneyCanvas.sortingOrder    = _prevOrder;
            _moneyCanvas.sortingLayerID  = _prevLayerID;
        }
        else
        {
            // 우리가 붙인 Canvas/Raycaster 제거 → 원래 HUDCanvas 정렬로 복귀
            if (_addedRaycaster != null) Destroy(_addedRaycaster);
            Destroy(_moneyCanvas);
        }
        _moneyCanvas = null;
        _addedRaycaster = null;
    }
}
