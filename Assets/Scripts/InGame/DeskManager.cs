using System.Collections.Generic;
using UnityEngine;

public class DeskManager : MonoBehaviour
{
    public static DeskManager Instance { get; private set; }

    [Header("Desks")]
    public List<WorkStation> allDesks;

    [Header("테스트")]
    [Tooltip("빈 데스크가 없을 때 desk_03 에 겹쳐 배정(테스트용). 끄면 원래대로 빈자리 없으면 스폰 안 됨.")]
    public bool overflowToDesk03 = true;

    // deskId → 사용 중인 employeeId
    private Dictionary<string, string> _deskAssignments = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // 빈 Desk 반환
    public WorkStation GetEmptyDesk()
    {
        foreach (var desk in allDesks)
        {
            if (!_deskAssignments.ContainsKey(desk.deskId))
                return desk;
        }
        // [테스트용] 빈 데스크가 없으면 desk_03 에 겹쳐 배정 → 데스크 수보다 많은 직원도 스폰돼
        // 랜덤이벤트/개발틱 등에 일반 직원처럼 가용됨. (시각적으로는 desk_03 에 겹쳐 앉음)
        if (overflowToDesk03)
        {
            var overflow = GetDeskById("desk_03");
            if (overflow != null)
            {
                Debug.LogWarning("빈 Desk 없음 → desk_03 에 겹쳐 배정(테스트용)");
                return overflow;
            }
        }
        Debug.LogWarning("빈 Desk 없음");
        return null;
    }

    public void AssignDesk(string deskId, string employeeId)
    {
        _deskAssignments[deskId] = employeeId;
    }

    public void UnassignDesk(string deskId)
    {
        _deskAssignments.Remove(deskId);
    }

    public WorkStation GetDeskById(string deskId)
    {
        return allDesks.Find(d => d.deskId == deskId);
    }
}