using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// 아웃게임 → 인게임 진입 시 인게임 데이터 일제 리셋 오케스트레이션
//
// Phase 0: RunState.resetInProgress=true 저장 (중간 실패 자동복구용 플래그)
// Phase 1: 메모리 only 매니저 리셋 (동기, 즉시)
// Phase 2: 뒤끝 row 삭제/덮어쓰기 매니저 리셋 (병렬, 콜백 카운터)
// Phase 3: 전체 완료되면 RunState.StartRun → playing=true + resetInProgress=false → GameScene 로드
//
// 보존(메타): OutGameCurrency / OwnedCard / OwnedTrait / OutGameEmployee
public static class NewRunInitializer
{
    // tutorial=true 는 LogoScenario 의 최초 온보딩 진입에서만 넘어옴 — 이번 런을 RunState.tutorial=true 로 시작해
    // GameScene 의 TutorialController 가 스크립트된 튜토리얼을 실행하도록 만든다.
    public static void StartNewRun(Action onComplete = null, bool tutorial = false)
    {
        Debug.Log($"[NewRun] 시작 (tutorial={tutorial})");

        if (RunStateManager.Instance != null)
        {
            RunStateManager.Instance.SetResetInProgress(true, success =>
            {
                if (!success)
                    Debug.LogError("[NewRun] resetInProgress 저장 실패 - 진행은 강행 (다음 로딩 시 자동복구 미보장)");
                RunResets(onComplete, tutorial);
            });
        }
        else
        {
            RunResets(onComplete, tutorial);
        }
    }

    static void RunResets(Action onComplete, bool tutorial)
    {
        // 아웃게임에서 만진 장착 슬롯 변경 등 pending 저장 즉시 flush
        OwnedTraitManager.Instance?.FlushPendingSave();
        CEOManager.Instance?.FlushPendingSave();
        StoneManager.Instance?.FlushPendingSave();
        ResetMemoryOnly();

        int pending = 0;
        bool issuedAll = false;

        Action onOneDone = null;
        onOneDone = () =>
        {
            pending--;
            Debug.Log($"[NewRun] reset 진행 중: 남음 {pending}");
            if (issuedAll && pending == 0) FinalizeRun(onComplete, tutorial);
        };

        void Issue(Action<Action> resetCall)
        {
            pending++;
            resetCall(onOneDone);
        }

        if (MoneyManager.Instance != null)            Issue(MoneyManager.Instance.ResetForNewRun);
        // EmployeeManager 가 GameTimeManager 보다 먼저 — GameTimeManager.SaveGameTime 이 내부적으로
        // SaveAllEmployees 를 호출하므로, ownedEmployees 가 먼저 비워져 있어야 stale UpdateV2 가 새 런의
        // DeleteV2 와 충돌해 "gameInfo not found" 404 를 내는 race 를 차단.
        if (EmployeeManager.Instance != null)         Issue(EmployeeManager.Instance.ResetForNewRun);
        if (GameTimeManager.Instance != null)         Issue(GameTimeManager.Instance.ResetForNewRun);
        if (CompletedProjectManager.Instance != null) Issue(CompletedProjectManager.Instance.ResetForNewRun);
        if (ItemManager.Instance != null)             Issue(ItemManager.Instance.ResetForNewRun);
        if (SalesSaveManager.Instance != null)        Issue(SalesSaveManager.Instance.ResetForNewRun);
        if (ProjectSaveManager.Instance != null)      Issue(ProjectSaveManager.Instance.ResetForNewRun);
        if (TechTreeManager.Instance != null)         Issue(TechTreeManager.Instance.ResetForNewRun);
        if (LoanManager.Instance != null)             Issue(LoanManager.Instance.ResetForNewRun);
        if (QuestManager.Instance != null)            Issue(QuestManager.Instance.ResetForNewRun);

        issuedAll = true;
        if (pending == 0) FinalizeRun(onComplete, tutorial);
    }

    static void ResetMemoryOnly()
    {
        StageManager.Instance?.ResetForNewRun();
        GenreFatigueManager.Instance?.ResetForNewRun();
        GenrePopularityManager.Instance?.ResetForNewRun();
        MasteryManager.Instance?.ResetForNewRun();
        SalaryNegotiationManager.Instance?.ResetForNewRun();
        RandomEventManager.Instance?.ResetForNewRun();
        TraitEffectApplier.ResetForNewRun();  // _firstSaleConsumed / _brokeRescueFired 등 1회성 플래그 리셋
        MerchantManager.Instance?.ResetForNewRun();
        DispatchManager.Instance?.ResetForNewRun();
        ProjectSetupUI.ResetForNewRun();  // 마지막 프로젝트 선택(규모/장르/플랫폼) 기억 초기화
    }

    static void FinalizeRun(Action onComplete, bool tutorial)
    {
        Debug.Log($"[NewRun] 데이터 리셋 완료 → 특성 효과 적용 → RunState.StartRun (tutorial={tutorial})");

        // 장착 특성 run-start 효과 (예: 금수저/은수저 startGold)
        // ResetForNewRun 으로 매니저 초기값 세팅 후, RunState 저장 전에 적용
        TraitEffectApplier.ApplyOnRunStart();

        if (RunStateManager.Instance == null)
        {
            SceneManager.LoadScene("GameScene");
            onComplete?.Invoke();
            return;
        }
        RunStateManager.Instance.StartRun(success =>
        {
            if (!success)
            {
                // 데이터는 다 비웠지만 playing=true 저장 실패 → 다음 로딩 시 resetInProgress=true 그대로라 자동복구 진입
                Debug.LogError("[NewRun] StartRun 저장 실패 - GameScene 진입 보류");
                if (AlertUI.Instance != null)
                    AlertUI.Instance.Show("진행 상태 저장에 실패했습니다.\n인터넷 상태 확인 후 다시 시도해주세요.");
                onComplete?.Invoke();
                return;
            }
            SceneManager.LoadScene("GameScene");
            onComplete?.Invoke();
        }, tutorial);
    }
}
