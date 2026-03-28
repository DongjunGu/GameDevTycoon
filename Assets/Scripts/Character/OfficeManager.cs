using System.Collections.Generic;
using UnityEngine;

public class OfficeManager : MonoBehaviour
{
    public static OfficeManager Instance { get; private set; }

    [Header("Spawn")]
    public GameObject fallbackPrefab;   // portraitId 매핑 없을 때 기본 프리팹
    public Transform  spawnPoint;       // 스폰 위치 (문 앞 등)

    // employeeId → OfficeCharacter
    private Dictionary<string, OfficeCharacter> _characters = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private GameObject GetPrefab(string portraitId)
    {
        if (!string.IsNullOrEmpty(portraitId))
        {
            var prefab = Resources.Load<GameObject>($"Characters/{portraitId}");
            if (prefab != null) return prefab;
        }
        return fallbackPrefab;
    }

    // EmployeeManager.HireEmployee() 완료 후 호출
    public void OnEmployeeHired(EmployeeData employee)
    {
        // 빈 Desk 찾기
        var desk = DeskManager.Instance.GetEmptyDesk();
        if (desk == null)
        {
            Debug.LogWarning("배정할 Desk 없음");
            return;
        }

        // Desk 배정
        DeskManager.Instance.AssignDesk(desk.deskId, employee.id);
        employee.assignedDeskId = desk.deskId;
        EmployeeManager.Instance.UpdateEmployee(employee);

        // 캐릭터 스폰
        var obj  = Instantiate(GetPrefab(employee.portraitId), spawnPoint.position, Quaternion.identity);
        var oc   = obj.GetComponent<OfficeCharacter>();
        oc.Init(employee.id, desk);

        _characters[employee.id] = oc;

        // Desk로 이동
        oc.GoToDesk();

        Debug.Log($"{employee.employeeName} 스폰 → {desk.deskId}로 이동");
    }

    // 머리 위 수치 팝업 표시
    public void ShowStatPopup(string employeeId, string text, Color color)
    {
        if (_characters.TryGetValue(employeeId, out var oc))
            oc.ShowStatPopup(text, color);
    }

    // 해고 시 캐릭터 제거
    public void OnEmployeeFired(EmployeeData employee)
    {
        if (_characters.TryGetValue(employee.id, out var oc))
        {
            DeskManager.Instance.UnassignDesk(employee.assignedDeskId);
            Destroy(oc.gameObject);
            _characters.Remove(employee.id);
        }
    }

    // 씬 로드 시 기존 직원 복원
    public void RestoreEmployees()
    {
        foreach (var employee in EmployeeManager.Instance.ownedEmployees)
        {
            var desk = string.IsNullOrEmpty(employee.assignedDeskId)
                ? DeskManager.Instance.GetEmptyDesk()
                : DeskManager.Instance.GetDeskById(employee.assignedDeskId);

            if (desk == null) continue;

            DeskManager.Instance.AssignDesk(desk.deskId, employee.id);
            employee.assignedDeskId = desk.deskId;

            var obj = Instantiate(GetPrefab(employee.portraitId), desk.GetWorkWorldPos(), Quaternion.identity);
            var oc  = obj.GetComponent<OfficeCharacter>();
            oc.Init(employee.id, desk);

            // 마지막 방향 복원
            var anim = obj.GetComponent<CharacterAnimator>();
            anim?.SetIdle(employee.lastIsFront);


            _characters[employee.id] = oc;
            // 복원 시엔 이동 없이 바로 Desk 위치에 배치
        }
    }
}