using UnityEngine;
using UnityEngine.UI;

// 세로 게이지(Fever/Stress 등) Fill 위에 얹는 "찰랑이는 액체" 연출.
// 좌우로 이어지는(타일링) 물결 텍스처를 UV 스크롤 + 상하 bobbing 으로 흔들어서 표면이 움직이는 것처럼 보이게 한다.
// 반드시 RawImage — Image(Sprite)는 uvRect 스크롤이 스프라이트 아틀라스 좌표계라 안전하게 안 되므로,
// Texture Import 설정에서 Wrap Mode = Repeat 로 맞춘 원본 텍스처를 RawImage.texture 에 직접 물려야 한다.
// Fill의 top 앵커(anchorMin.y=1, anchorMax.y=1)에 자식으로 붙이면, 세로 슬라이더 값이 바뀌어 Fill 높이가
// 변할 때 이 오브젝트도 자동으로 그 상단 경계를 따라간다(별도 위치 추적 코드 불필요).
[ExecuteAlways]
[RequireComponent(typeof(RawImage))]
public class GaugeWaveEffect : MonoBehaviour
{
    [Tooltip("물결이 옆으로 흐르는 속도 (UV/초)")]
    public float scrollSpeed = 0.15f;
    [Tooltip("위아래로 출렁이는 폭 (로컬 단위)")]
    public float bobAmplitude = 1.5f;
    [Tooltip("출렁이는 속도 (클수록 빠름)")]
    public float bobSpeed = 2f;

    RawImage _raw;
    RectTransform _rt;
    Vector2 _baseAnchoredPos;
    float _uvOffset;

    void OnEnable()
    {
        _raw = GetComponent<RawImage>();
        _rt = transform as RectTransform;
        _baseAnchoredPos = _rt.anchoredPosition;
    }

    void Update()
    {
        _uvOffset += scrollSpeed * Time.deltaTime;
        var rect = _raw.uvRect;
        rect.x = _uvOffset;
        _raw.uvRect = rect;

#if UNITY_EDITOR
        float t = Application.isPlaying ? Time.time : (float)UnityEditor.EditorApplication.timeSinceStartup;
#else
        float t = Time.time;
#endif
        _rt.anchoredPosition = _baseAnchoredPos + Vector2.up * (Mathf.Sin(t * bobSpeed) * bobAmplitude);
    }
}
