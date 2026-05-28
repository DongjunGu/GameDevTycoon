using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 이력서(EmployeeResumePanel)의 특성/이벤트 라벨(TMP)에 런타임 부착되는 클릭 토글.
/// 클릭 시 연결된 설명 패널의 Image 컴포넌트 + 자식 텍스트를 켜고/끄며 설명을 표시한다.
/// (AlertUI 를 쓰지 않으므로 ModalGate 차단 영향 없음. EmployeeResumePanel.Setup 이 매번 Setup() 으로 바인딩.)
/// </summary>
public class ResumeDescriptionToggle : MonoBehaviour, IPointerClickHandler
{
    Image _img;        // 설명 패널의 Image (평상시 enabled=false)
    TMP_Text _text;    // 설명 패널의 자식 텍스트 (평상시 GameObject 비활성)
    string _desc;
    bool _active;      // 설명 토글 가능 여부 (설명/참조 있을 때만)

    // descImg/descText/desc 중 하나라도 비면 비활성(클릭 무시). 호출 시 항상 닫힌 상태로 초기화.
    public void Setup(Image descImg, TMP_Text descText, string desc)
    {
        _img = descImg;
        _text = descText;
        _desc = desc;
        _active = !string.IsNullOrEmpty(desc) && (_img != null || _text != null);
        Hide();
    }

    void Hide()
    {
        if (_img != null) _img.enabled = false;
        if (_text != null) _text.gameObject.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_active) return;
        bool show = _img != null ? !_img.enabled
                  : !(_text != null && _text.gameObject.activeSelf);
        if (_img != null) _img.enabled = show;
        if (_text != null)
        {
            if (show) _text.text = _desc;
            _text.gameObject.SetActive(show);
        }
    }
}
