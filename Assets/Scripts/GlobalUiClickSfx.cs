using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// 전역 버튼 클릭음 — 어떤 버튼을 눌러도 소리가 나게 한다.
// 버튼마다 리스너를 다는 대신, 클릭 순간 UI 레이캐스트로 Button 이 맞았는지 보고 SFX 재생.
// → 런타임에 생성된 버튼(카드/슬롯 등)까지 자동 커버.
// 배치: 영속 오브젝트(SoundManager) 에 붙이면 모든 씬에서 동작.
public class GlobalUiClickSfx : MonoBehaviour
{
    [SerializeField] private SoundData clickSound;

    private readonly List<RaycastResult> results = new List<RaycastResult>();

    void Update()
    {
        if (clickSound == null) return;

        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return;

        var es = EventSystem.current;
        if (es == null) return;

        var data = new PointerEventData(es) { position = pointer.position.ReadValue() };
        results.Clear();
        es.RaycastAll(data, results);

        // 맨 위(실제로 클릭을 받는) 결과만 본다 — 뒤 결과까지 훑으면 dim 에 가려진 버튼에서도 소리가 난다
        if (results.Count == 0) return;
        var btn = results[0].gameObject.GetComponentInParent<Button>();
        if (btn != null && btn.interactable)
            SoundManager.Instance?.PlaySFX(clickSound);
    }
}
