using UnityEngine;
using UnityEngine.EventSystems;

// eventText(TMP) 에 런타임 부착되는 클릭 핸들러 — 클릭 시 해당 직원의 전용 이벤트 설명을 AlertUI 로 표시.
// 부착/바인딩은 CharacterUniqueEvents.SetupEventText 가 코드로 처리(에디터 배선 불필요).
// traitText 의 TraitDescriptionButton 과 동일 패턴.
public class EventDescriptionButton : MonoBehaviour, IPointerClickHandler
{
    private EmployeeData _emp;

    public void Bind(EmployeeData emp) => _emp = emp;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_emp == null) return;
        CharacterUniqueEvents.ShowEventDescription(_emp);
    }
}
