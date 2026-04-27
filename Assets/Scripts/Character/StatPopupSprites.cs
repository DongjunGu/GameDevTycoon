using UnityEngine;

// 상시 개발틱 팝업에 쓰일 스탯 스프라이트 등록.
// 씬에 게임오브젝트로 배치하고 인스펙터에서 4개 스프라이트 할당.
public class StatPopupSprites : MonoBehaviour
{
    public static StatPopupSprites Instance { get; private set; }

    [Header("스탯 아이콘")]
    public Sprite planning;
    public Sprite develop;
    public Sprite art;
    public Sprite bug;
    public Sprite blank;   // 꽝 표시용

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public Sprite GetStatIcon(string key) => key switch
    {
        "planning" => planning,
        "develop"  => develop,
        "art"      => art,
        "bug"      => bug,
        "blank"    => blank,
        _ => null
    };
}
