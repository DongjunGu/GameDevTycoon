using UnityEngine;

// 기준 위치(base position) 주변에서 sin파로 위아래 둥실거리는 아이들 애니메이션.
// 외부에서 위치를 재배치할 때는 SetBasePosition을 호출해야 기준점도 같이 갱신됨(단순 position 대입은 다음 프레임에 덮임).
public class FloatBob : MonoBehaviour
{
    [Tooltip("위아래로 흔들리는 폭 (world unit)")]
    public float amplitude = 0.06f;
    [Tooltip("흔들리는 속도 (클수록 빠름)")]
    public float speed = 4f;

    private Vector3 _basePos;

    void OnEnable()
    {
        _basePos = transform.position;
    }

    public void SetBasePosition(Vector3 pos)
    {
        _basePos = pos;
        transform.position = pos;
    }

    void Update()
    {
        transform.position = _basePos + Vector3.up * (Mathf.Sin(Time.time * speed) * amplitude);
    }
}
