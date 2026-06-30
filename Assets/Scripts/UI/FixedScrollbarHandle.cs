using UnityEngine;
using UnityEngine.UI;

// Scrollbar 핸들 크기를 콘텐츠 양과 무관하게 고정.
// ScrollbarVertical 오브젝트에 붙이면 됨.
[RequireComponent(typeof(Scrollbar))]
public class FixedScrollbarHandle : MonoBehaviour
{
    [Range(0f, 1f)]
    [Tooltip("핸들 고정 크기 (0=최소, 1=전체)")]
    public float fixedSize = 0.2f;

    Scrollbar _sb;

    void Awake() => _sb = GetComponent<Scrollbar>();

    void LateUpdate()
    {
        if (_sb != null) _sb.size = fixedSize;
    }
}
