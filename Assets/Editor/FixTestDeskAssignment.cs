using UnityEditor;
using UnityEngine;

// 일회성 툴 — WarpDesksInstant 테스트 도중 뒤이은 자동저장(SaveGameTime→SaveAllEmployees fan-out)에
// 실려서 백엔드에 영구 반영된 desk_05~08 배정을 원래 Level1 데스크(desk_01~04)로 되돌리고 저장한다.
public static class FixTestDeskAssignment
{
    [MenuItem("Tools/GameDevTycoon/Fix Stale Desk Assignment (Play Mode)")]
    public static void Fix()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Fix] Play 모드에서만 실행 가능"); return; }
        if (EmployeeManager.Instance == null || DeskManager.Instance == null) return;

        var owned = EmployeeManager.Instance.ownedEmployees;
        var level1Desks = new[] { "desk_01", "desk_02", "desk_03", "desk_04" };

        var occupied = new System.Collections.Generic.HashSet<string> { "desk_03" }; // 비서 고정석
        foreach (var e in owned) if (!string.IsNullOrEmpty(e.assignedDeskId)) occupied.Add(e.assignedDeskId);
        if (EmployeeManager.Instance.CEO != null) occupied.Add(EmployeeManager.Instance.CEO.assignedDeskId);

        var allCharacters = Object.FindObjectsByType<OfficeCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var emp in owned)
        {
            if (emp.assignedDeskId != "desk_05" && emp.assignedDeskId != "desk_06" &&
                emp.assignedDeskId != "desk_07" && emp.assignedDeskId != "desk_08")
                continue;

            string free = System.Array.Find(level1Desks, d => !occupied.Contains(d));
            if (free == null) { Debug.LogWarning($"[Fix] {emp.employeeName} 되돌릴 빈 Level1 데스크 없음"); continue; }

            string oldDesk = emp.assignedDeskId;
            emp.assignedDeskId = free;
            occupied.Add(free);

            DeskManager.Instance.UnassignDesk(oldDesk);
            DeskManager.Instance.AssignDesk(free, emp.id);

            var target = System.Array.Find(allCharacters, c => c.employeeId == emp.id);
            if (target != null)
            {
                var deskComp = DeskManager.Instance.GetDeskById(free);
                target.assignedDesk = deskComp;
                target.transform.position = deskComp.GetWorkWorldPos();
                target.ApplyDeskAnimation();
            }

            EmployeeManager.Instance.UpdateEmployee(emp);
            Debug.Log($"[Fix] {emp.employeeName}: {oldDesk} → {free} 로 되돌리고 저장 요청");
        }

        GameTimeManager.Instance?.SaveGameTime();
        ProjectSaveManager.Instance?.SaveProject();
        Debug.Log("[Fix] 완료 — SaveGameTime/SaveProject 호출함");
    }
}
