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
}
