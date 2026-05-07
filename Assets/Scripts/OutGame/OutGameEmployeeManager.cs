using System;
using System.Collections.Generic;
using UnityEngine;

// 직원별 도달 maxGrade 메모리 캐시 (백엔드 저장 X)
// 진실 source는 OwnedCard.cardsJson — 백엔드는 거기 한 곳만 저장하고
// 이 매니저는 OwnedCardManager.LoadAsync 후 SyncFromOwnedCards로 메모리만 채움.
// 모든 사용처(갤러리/슬롯/인게임 채용 등)는 GetMaxGrade(empId) 한 줄로 조회.
public class OutGameEmployeeManager : MonoBehaviour
{
    public static OutGameEmployeeManager Instance { get; private set; }

    private readonly Dictionary<string, EmployeeGrade> _maxGradeMap = new();

    public event Action OnChanged;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // BackendManager.LoadAllAndEnterGame 체인 호환을 위한 stub.
    // 실제 데이터는 OwnedCardManager.LoadAsync 후 SyncFromOwnedCards에서 채워짐.
    public void LoadAsync(System.Action onComplete = null)
    {
        _maxGradeMap.Clear();
        onComplete?.Invoke();
    }

    public EmployeeGrade GetMaxGrade(string empId)
    {
        return _maxGradeMap.TryGetValue(empId, out var g) ? g : EmployeeGrade.Normal;
    }

    public bool IsAcquired(string empId) => _maxGradeMap.ContainsKey(empId);

    // OwnedCard로부터 모든 직원 maxGrade 재계산 (메모리만)
    public void SyncFromOwnedCards()
    {
        if (EmployeeManager.Instance?.poolEmployees == null) return;
        if (OwnedCardManager.Instance == null) return;
        foreach (var emp in EmployeeManager.Instance.poolEmployees)
        {
            if (emp == null || string.IsNullOrEmpty(emp.id)) continue;
            _maxGradeMap[emp.id] = OwnedCardManager.Instance.GetHighestGrade(emp.id);
        }
        OnChanged?.Invoke();
    }

    // 카드 추가 시 호출 — 더 큰 grade로만 갱신
    public bool TryUpgradeMaxGrade(string empId, EmployeeGrade newGrade)
    {
        if (string.IsNullOrEmpty(empId)) return false;
        var current = GetMaxGrade(empId);
        if ((int)newGrade <= (int)current && _maxGradeMap.ContainsKey(empId)) return false;
        _maxGradeMap[empId] = newGrade;
        OnChanged?.Invoke();
        return true;
    }

    // 카드 차감 후 호출 — 보유 카드 기준 재계산 (다운그레이드 가능)
    public void RecalcMaxGrade(string empId)
    {
        if (string.IsNullOrEmpty(empId)) return;
        var newMax = OwnedCardManager.Instance != null
            ? OwnedCardManager.Instance.GetHighestGrade(empId)
            : EmployeeGrade.Normal;
        var current = GetMaxGrade(empId);
        if (current == newMax && _maxGradeMap.ContainsKey(empId)) return;
        _maxGradeMap[empId] = newMax;
        OnChanged?.Invoke();
    }
}
