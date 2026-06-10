using UnityEngine;
using UnityEngine.UI;

// 파견중 직원 슬롯 공통 표시 — 희미하게(CanvasGroup alpha) + '파견중' badge 활성 + (옵션)선택 차단.
// 강화/해고/팀장선택/아이템 리스트는 blockSelect=true(선택 불가), 직원 상태바는 false(카드 열람 허용).
public static class DispatchSlotVisual
{
    public static void Apply(MonoBehaviour slot, GameObject dispatchedBadge, string employeeId, bool blockSelect)
    {
        bool dispatched = DispatchManager.Instance != null && DispatchManager.Instance.IsDispatched(employeeId);

        // badge 필드 미연결이어도 dispatchBadge 스프라이트를 쓰는 자식 Image 를 자동 탐색해 활성화
        var badge = dispatchedBadge;
        if (badge == null && slot != null) badge = FindBadgeBySprite(slot.transform);
        if (badge != null) badge.SetActive(dispatched);

        if (slot == null) return;

        var cg = slot.GetComponent<CanvasGroup>();
        if (cg == null) cg = slot.gameObject.AddComponent<CanvasGroup>();
        cg.alpha          = dispatched ? 0.5f : 1f;
        cg.interactable   = !(dispatched && blockSelect);
        cg.blocksRaycasts = true;
    }

    // 자식 Image 중 sprite 이름에 "dispatchbadge" 가 포함된 것을 badge 로 간주 (비활성 포함 탐색)
    static GameObject FindBadgeBySprite(Transform root)
    {
        foreach (var img in root.GetComponentsInChildren<Image>(true))
        {
            if (img.sprite != null && img.sprite.name.ToLowerInvariant().Contains("dispatchbadge"))
                return img.gameObject;
        }
        return null;
    }
}
