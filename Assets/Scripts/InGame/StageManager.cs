using UnityEngine;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    [Header("현재 단계")]
    [SerializeField] private int currentStage = 1;
    public int CurrentStage => currentStage;

    [Header("단계별 최대 직원 수")]
    [Tooltip("인덱스 0 = 1단계, 1 = 2단계 ...")]
    [SerializeField] private int[] maxEmployeePerStage = { 3 };

    public int MaxEmployeeCount
    {
        get
        {
            int index = currentStage - 1;
            if (index >= 0 && index < maxEmployeePerStage.Length)
                return maxEmployeePerStage[index];
            return 999;
        }
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // 새 런 시작 — Stage 1 로 복귀 (서버 저장 없음)
    public void ResetForNewRun()
    {
        currentStage = 1;
    }

    // [테스트] currentStage에 setter가 없어 외부(TestMenuButtons 등)에서 강제로 올릴 때 사용.
    public void SetStage(int stage) => currentStage = Mathf.Max(1, stage);
}
