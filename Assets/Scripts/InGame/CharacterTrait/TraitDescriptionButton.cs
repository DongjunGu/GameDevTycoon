using UnityEngine;
using UnityEngine.EventSystems;

// traitText(TMP) 에 런타임 부착되는 클릭 핸들러 — 클릭 시 해당 직원의 특성 설명을 AlertUI 로 표시.
// 부착/바인딩은 CharacterTraitApplier.SetupTraitText 가 코드로 처리하므로 에디터 배선이 필요 없다.
// (raycastTarget on/off 도 SetupTraitText 가 특성 보유 여부에 따라 제어 — 특성 없으면 클릭이 슬롯으로 통과)
public class TraitDescriptionButton : MonoBehaviour, IPointerClickHandler
{
    private EmployeeData _emp;

    public void Bind(EmployeeData emp) => _emp = emp;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_emp == null) return;
        CharacterTraitApplier.ShowTraitDescription(_emp);
    }
}
