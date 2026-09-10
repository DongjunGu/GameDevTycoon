using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 붙은 패널을 같은 캔버스의 다른 UI 위에 그린다 (Canvas.overrideSorting + sortingOrder)
// 예: OutGameScene/MainCanvas/TopRightPanel — SafeAreaPanel 안의 패널들보다 위
//
// exceptionPanels 중 하나라도 켜져 있으면 overrideSorting 을 끄고 원래 하이어라키 순서로 복귀한다.
// → 그 예외 패널이 이 패널 위에 그려진다. (예: ConfirmUI, 전체화면 결과 팝업 등)
// 인스펙터에서 예외 패널을 자유롭게 추가/제거.
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(GraphicRaycaster))]
public class AlwaysOnTopUI : MonoBehaviour
{
    [Tooltip("최상단으로 올릴 때 쓸 sortingOrder. 같은 캔버스의 다른 UI(기본 0)보다 크면 된다")]
    public int sortingOrder = 100;

    [Tooltip("여기 등록된 패널이 하나라도 켜져 있으면 이 패널은 최상단을 양보한다 (원래 순서로 복귀)")]
    public List<GameObject> exceptionPanels = new();

    private Canvas _canvas;

    void Awake()
    {
        _canvas = GetComponent<Canvas>();
        Apply();
    }

    void OnEnable() => Apply();

    // ponytail: 예외 패널 on/off 를 알려주는 공용 이벤트가 없어 매 프레임 확인한다. 목록이 짧아 비용은 무시 가능
    void LateUpdate() => Apply();

    void Apply()
    {
        if (_canvas == null) return;

        bool giveWay = false;
        for (int i = 0; i < exceptionPanels.Count; i++)
        {
            var go = exceptionPanels[i];
            if (go == null) continue;           // ?. 는 파괴된 Unity Object 를 못 걸러냄 — 명시적 비교
            if (!go.activeInHierarchy) continue;
            giveWay = true;
            break;
        }

        _canvas.overrideSorting = !giveWay;
        if (!giveWay) _canvas.sortingOrder = sortingOrder;
    }
}
