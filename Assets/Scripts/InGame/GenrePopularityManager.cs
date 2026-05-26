using System.Collections.Generic;
using UnityEngine;

public class GenrePopularityManager : MonoBehaviour
{
    public static GenrePopularityManager Instance { get; private set; }

    public event System.Action OnPopularityChanged;

    private Dictionary<ProjectGenre, int> _popularity = new();
    private int _weekCounter = 0;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        RandomizeAll();
    }

    void Start()
    {
        GameTimeManager.Instance.OnTimeChanged += OnWeekChanged;
    }

    void OnDestroy()
    {
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnTimeChanged -= OnWeekChanged;
    }

    void OnWeekChanged()
    {
        _weekCounter++;
        if (_weekCounter >= 2)
        {
            _weekCounter = 0;
            RandomizeAll();
        }
    }

    void RandomizeAll()
    {
        foreach (ProjectGenre g in System.Enum.GetValues(typeof(ProjectGenre)))
            _popularity[g] = UnityEngine.Random.Range(1, 4); // 1~3
        Debug.Log("[인기도] 갱신됨");
        OnPopularityChanged?.Invoke();
    }

    // 새 런 시작 — 인기도 새로 랜덤 + 주차 카운터 초기화
    public void ResetForNewRun()
    {
        _weekCounter = 0;
        RandomizeAll();
    }

    public int GetPopularity(ProjectGenre genre)
    {
        return _popularity.TryGetValue(genre, out int v) ? v : 1;
    }

    // 특정 장르 인기도 강제 설정 (버튜버 데뷔 이벤트 등). 1~3 클램프.
    public void SetPopularity(ProjectGenre genre, int value)
    {
        _popularity[genre] = Mathf.Clamp(value, 1, 3);
        OnPopularityChanged?.Invoke();
    }
}
