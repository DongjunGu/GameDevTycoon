using UnityEngine;
using UnityEngine.UI;

// 재사용 가능한 sin파 알파 깜빡임 — LeaderScoreUI.UpdateStressWarning()(LeaderScoreEmergencyImage)과
// 완전히 동일한 수식(사인파로 alphaMin~alphaMax를 부드럽게 왕복)을 재사용 가능한 컴포넌트로 뺀 버전.
// DOTween Yoyo(ImageBlink)와 달리 정확히 사인 곡선을 따라 오가며, activeInHierarchy인 동안 계속 진행된다.
[RequireComponent(typeof(Image))]
public class SineAlphaBlink : MonoBehaviour
{
    [Tooltip("최소~최대 alpha 사이를 사인파로 왕복 (0~1)")]
    public float alphaMin = 80f / 255f;
    public float alphaMax = 120f / 255f;
    [Tooltip("한쪽 끝에서 반대쪽 끝까지 걸리는 시간(초) — sin파 반주기")]
    public float halfPeriod = 0.3f;

    Image _image;

    void Awake() => _image = GetComponent<Image>();

    void LateUpdate()
    {
        float period = Mathf.Max(0.01f, halfPeriod) * 2f;
        float wave = (Mathf.Sin(Time.time * (2f * Mathf.PI / period)) + 1f) * 0.5f; // 0~1
        float a = Mathf.Lerp(alphaMin, alphaMax, wave);

        var c = _image.color;
        c.a = a;
        _image.color = c;
    }
}
