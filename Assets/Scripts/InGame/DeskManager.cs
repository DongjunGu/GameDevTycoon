using System.Collections.Generic;
using UnityEngine;

public class DeskManager : MonoBehaviour
{
    public static DeskManager Instance { get; private set; }

    [Header("Desks")]
    public List<WorkStation> allDesks;

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