// =====================================================
// EventDialogTable.cs
// 모든 다이얼로그 트리거 호출을 이 파일 하나에서 관리
// 코드 어디서든 이 파일의 메서드만 호출하면 됨
// =====================================================
// [OnChoiceResult 구독 예시 — GameManager.cs 등에서]
//   DialogManager.Instance.OnChoiceResult += HandleDialogResult;
//
//   void HandleDialogResult(string type, int value)
//   {
//       if (type == "GoldChange") MoneyManager.Instance.AddGold(value);
//   }
// =====================================================

public static class EventDialogTable
{
    // ─── 게임 시작 ───────────────────────────────────────────────
    public static void OnGameStart()
    {
        DialogManager.Instance.Play("event_game_start", triggerOnce: true);
    }

    // ─── 첫 고용 안내 ────────────────────────────────────────────
    public static void OnFirstHirePrompt()
    {
        DialogManager.Instance.Play("event_first_hire", triggerOnce: true);
    }

    // ─── 직원 고용 완료 ──────────────────────────────────────────
    // 직원별 전용 대화가 있으면 우선, 없으면 공통 대화
    public static void OnHireComplete(string employeeId)
    {
        string specific = $"event_hire_{employeeId}";
        string groupId  = DialogManager.Instance.HasGroup(specific)
                          ? specific
                          : "event_hire_default";
        DialogManager.Instance.Play(groupId, triggerOnce: true);
    }

    // ─── 마케팅 완료 ─────────────────────────────────────────────
    public static void OnMarketingComplete()
    {
        DialogManager.Instance.Play("event_marketing_done", triggerOnce: false);
    }

    // ─── 프로젝트 완료 ───────────────────────────────────────────
    public static void OnProjectComplete(string projectId)
    {
        string groupId = $"event_project_{projectId}";
        if (DialogManager.Instance.HasGroup(groupId))
            DialogManager.Instance.Play(groupId, triggerOnce: true);
    }

    // ─── 수동 호출 (테스트용) ────────────────────────────────────
    public static void PlayManual(string dialogGroupId, bool triggerOnce = false)
    {
        DialogManager.Instance.Play(dialogGroupId, triggerOnce);
    }
}