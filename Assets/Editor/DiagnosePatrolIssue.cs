using UnityEditor;
using UnityEngine;

// 일회성 진단 툴 — p3/p4 patrol이 왜 안 걸리는지 단계별로 로그 찍어서 확인.
public static class DiagnosePatrolIssue
{
    [MenuItem("Tools/GameDevTycoon/Diagnose Patrol Issue (Play Mode)")]
    public static void Diagnose()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Diag] Play 모드에서만 실행 가능"); return; }

        var points = Object.FindObjectsByType<PatrolPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"[Diag] 씬 전체 PatrolPoint 개수(비활성 포함)={points.Length}");
        foreach (var p in points)
            Debug.Log($"[Diag]   pointId={p.pointId}, active={p.gameObject.activeInHierarchy}, pos={p.transform.position}");

        if (OfficeManager.Instance == null) { Debug.LogWarning("[Diag] OfficeManager.Instance null"); return; }
        if (EmployeeManager.Instance == null) { Debug.LogWarning("[Diag] EmployeeManager.Instance null"); return; }

        var owned = EmployeeManager.Instance.ownedEmployees;
        Debug.Log($"[Diag] ownedEmployees.Count={owned.Count}");
        foreach (var e in owned)
            Debug.Log($"[Diag]   emp id={e.id}, name={e.employeeName}, assignedDeskId={e.assignedDeskId}, isPatrolling={OfficeManager.Instance.IsPatrolling(e.id)}, state={OfficeManager.Instance.GetState(e.id)}");

        OfficeManager.Instance.RefreshPatrolPoints();

        if (owned.Count == 0) return;
        var target = owned[0];
        Debug.Log($"[Diag] 테스트 대상: {target.employeeName} ({target.id}) → p3로 ForceCharacterToPatrolPoint 호출");
        OfficeManager.Instance.ForceCharacterToPatrolPoint(target.id, "p3");

        Debug.Log($"[Diag] 호출 직후 IsPatrolling={OfficeManager.Instance.IsPatrolling(target.id)}, state={OfficeManager.Instance.GetState(target.id)}");
    }

    [MenuItem("Tools/GameDevTycoon/Diagnose - Click NextLevelBtn")]
    public static void ClickNextLevelBtn()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Diag] Play 모드에서만 실행 가능"); return; }

        var btn = Object.FindAnyObjectByType<TestMenuButtons>(FindObjectsInactive.Include);
        if (btn == null) { Debug.LogWarning("[Diag] TestMenuButtons 컴포넌트를 씬에서 찾을 수 없음"); return; }

        Debug.Log("[Diag] === OnClickNextLevel 직접 호출 시작 ===");
        btn.OnClickNextLevel();
        Debug.Log("[Diag] === OnClickNextLevel 호출 끝 ===");
    }

    [MenuItem("Tools/GameDevTycoon/Diagnose - Check Desk Positions")]
    public static void CheckDeskPositions()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Diag] Play 모드에서만 실행 가능"); return; }
        if (DeskManager.Instance == null) { Debug.LogWarning("[Diag] DeskManager.Instance null"); return; }

        foreach (var deskId in new[] { "desk_05", "desk_06", "desk_07", "desk_08" })
        {
            var desk = DeskManager.Instance.GetDeskById(deskId);
            if (desk == null) { Debug.Log($"[Diag] {deskId}: GetDeskById 결과 null"); continue; }
            Debug.Log($"[Diag] {deskId}: deskObj.transform.position={desk.transform.position}, GetWorkWorldPos()={desk.GetWorkWorldPos()}");
        }

        // 현재 스폰된 OfficeCharacter들의 실제 위치도 같이 찍는다.
        var all = Object.FindObjectsByType<OfficeCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var oc in all)
        {
            if (string.IsNullOrEmpty(oc.employeeId)) continue;
            string deskName = oc.assignedDesk != null ? oc.assignedDesk.deskId : "null";
            Debug.Log($"[Diag] character employeeId={oc.employeeId}, assignedDesk={deskName}, transform.position={oc.transform.position}");
        }
    }
}
