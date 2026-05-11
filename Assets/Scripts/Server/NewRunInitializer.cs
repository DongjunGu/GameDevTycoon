using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// 아웃게임 → 인게임 진입 시 인게임 데이터 일제 리셋 오케스트레이션
//
// Phase 1: 메모리 only 매니저 리셋 (동기, 즉시)
// Phase 2: 뒤끝 row 삭제/덮어쓰기 매니저 리셋 (병렬, 콜백 카운터)
// Phase 3: 전체 완료되면 RunState.StartRun → GameScene 로드
//
// 보존(메타): OutGameCurrency / OwnedCard / OwnedTrait / OutGameEmployee
public static class NewRunInitializer
{
    public static void StartNewRun(Action onComplete = null)
    {
        Debug.Log("[NewRun] 시작");
        // 아웃게임에서 만진 장착 슬롯 변경 등 pending 저장 즉시 flush
        OwnedTraitManager.Instance?.FlushPendingSave();
        ResetMemoryOnly();

        int pending = 0;
        bool issuedAll = false;

        Action onOneDone = null;
        onOneDone = () =>
        {
            pending--;
            Debug.Log($"[NewRun] reset 진행 중: 남음 {pending}");
            if (issuedAll && pending == 0) FinalizeRun(onComplete);
        };

        void Issue(Action<Action> resetCall)
        {
            pending++;
            resetCall(onOneDone);
        }

        if (MoneyManager.Instance != null)            Issue(MoneyManager.Instance.ResetForNewRun);
        if (GameTimeManager.Instance != null)         Issue(GameTimeManager.Instance.ResetForNewRun);
        if (EmployeeManager.Instance != null)         Issue(EmployeeManager.Instance.ResetForNewRun);
        if (CompletedProjectManager.Instance != null) Issue(CompletedProjectManager.Instance.ResetForNewRun);
        if (ItemManager.Instance != null)             Issue(ItemManager.Instance.ResetForNewRun);
        if (SalesSaveManager.Instance != null)        Issue(SalesSaveManager.Instance.ResetForNewRun);
        if (ProjectSaveManager.Instance != null)      Issue(ProjectSaveManager.Instance.ResetForNewRun);
        if (TechTreeManager.Instance != null)         Issue(TechTreeManager.Instance.ResetForNewRun);
        if (LoanManager.Instance != null)             Issue(LoanManager.Instance.ResetForNewRun);
        if (QuestManager.Instance != null)            Issue(QuestManager.Instance.ResetForNewRun);

        issuedAll = true;
        if (pending == 0) FinalizeRun(onComplete);
    }

    static void ResetMemoryOnly()
    {
        StageManager.Instance?.ResetForNewRun();
        GenreFatigueManager.Instance?.ResetForNewRun();
        GenrePopularityManager.Instance?.ResetForNewRun();
        SalaryNegotiationManager.Instance?.ResetForNewRun();
        RandomEventManager.Instance?.ResetForNewRun();
    }

    static void FinalizeRun(Action onComplete)
    {
        Debug.Log("[NewRun] 데이터 리셋 완료 → RunState.StartRun");
        if (RunStateManager.Instance == null)
        {
            SceneManager.LoadScene("GameScene");
            onComplete?.Invoke();
            return;
        }
        RunStateManager.Instance.StartRun(() =>
        {
            SceneManager.LoadScene("GameScene");
            onComplete?.Invoke();
        });
    }
}
