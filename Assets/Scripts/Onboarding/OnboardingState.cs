using UnityEngine;

// 첫 게스트 온보딩(컷씬 + 게임씬 비서 튜토리얼) 1회 게이트.
// 로컬(PlayerPrefs) 저장 — DialogManager 의 triggerOnce 와 동일한 기기 단위 방식.
// (테스터 PC 마다 1회씩 노출. 뒤끝 계정 단위가 필요하면 추후 서버 플래그로 교체.)
public static class OnboardingState
{
    const string KEY_INTRO      = "onboarding_intro_done";      // 컷씬 + 첫 런 진입 완료
    const string KEY_TUTORIAL   = "onboarding_tutorial_done";   // 게임씬 비서 튜토리얼 완료
    const string KEY_FIRST_HIRE = "onboarding_first_hire_done"; // 첫 채용(온보딩) 완료 — 1회만 1주 면접
    const string KEY_TUT3       = "onboarding_tutorial3_done";  // 튜토리얼 3-1(ConfirmHirePanel 첫 노출) 완료
    const string KEY_TUT5       = "onboarding_tutorial5_done";  // 튜토리얼 5-1~5-4 완료
    // 5-1~5-4 무장(진행 중, 아직 미완료) 여부 — 이 구간은 3-1~4-2와 달리 직원 2명이 이미 서버에 커밋된
    // 뒤라 재접속해도 게임 상태가 자연히 리셋되지 않는다. pending 없이는 완료 전 재접속 시 통째로 유실됨.
    const string KEY_TUT5_PEND  = "onboarding_tutorial5_pending";
    // 튜토리얼 6-1~6-3 완료 — 기획팀장 선택(DispatchPanelUI, Planner). pending 불필요: DevelopmentManager가
    // pendingLeaderSelect 를 자체 영속화해 재접속 시 이 패널을 자연히 다시 열어주므로(3-1~4-2와 동일한 이유),
    // Tutorial6Done 이 false인 동안엔 패널이 열릴 때마다 매번 6-1부터 재생하면 된다.
    const string KEY_TUT6       = "onboarding_tutorial6_done";
    // 튜토리얼 7-1~7-6 완료 — 첫 기획팀장 점수 화면(DevelopmentManager.BuildAndShowLeaderScore, Planner).
    // pending 불필요: 6단계와 동일한 이유(재접속 재개도 BuildAndShowLeaderScore를 다시 타므로 자연 재생).
    const string KEY_TUT7       = "onboarding_tutorial7_done";
    // 튜토리얼 8-1 완료 — 팀장점수 패널이 실제로 닫히고(LeaderScoreUI.OnConfirmClosed) 개발이 시작된 뒤
    // SupriseQuestUI 강조. pending 불필요: CurrentStage==Developing 자체가 이미 서버에 커밋되는 상태라
    // (7단계까지 끝났다는 건 개발이 진짜 시작됐다는 뜻) 재접속해도 그 상태 그대로 자연히 재개된다.
    const string KEY_TUT8       = "onboarding_tutorial8_done";
    // 튜토리얼 9-1/9-2 완료 — 개발팀장 점수(첫 Programmer 팀장점수 화면) 4회차 조준 대기 시점 안내(9-1) +
    // 실제 조준 버튼 클릭 직후 선택지별 반응(9-2, 약/중 vs 강). pending 불필요: 7단계와 동일 이유
    // (BuildAndShowLeaderScore가 재접속 재개도 다시 타고, 4회차 결과는 오버플로 때만 저장되므로 중단해도
    // 자연히 처음부터 재생됨).
    const string KEY_TUT9       = "onboarding_tutorial9_done";
    // 튜토리얼 10-1~10-3 완료 — 직원 카드/만족도 소개 + AcWar(에어컨 전쟁) 이벤트 체험까지.
    const string KEY_TUT10      = "onboarding_tutorial10_done";
    // 튜토리얼 12-1 완료 — 아트팀장 선택(75% 진행도). 아트 직원이 없어 CEO가 유일한 후보인 상황 안내.
    // 11단계는 아직 미구현(추후 추가 예정) — 번호가 이어지지 않아도 각 플래그는 독립적이라 문제 없음.
    const string KEY_TUT12      = "onboarding_tutorial12_done";
    // 튜토리얼 13-1/13-2 완료 — 진행도 ~95% 시점 첫 직원의 확정 창의성 틱 발동 직후, 창의성 점수/블록 안내.
    const string KEY_TUT13      = "onboarding_tutorial13_done";
    // 튜토리얼 13-4 완료 — 창의성 미니게임 진입 시 Sq/T_U 블록을 지정 위치(b1.png 기준)에만 놓게 유도.
    const string KEY_TUT13_4    = "onboarding_tutorial13_4_done";
    // 튜토리얼 13-5 완료 — 디버깅 단계 시작 직후, 창의성 능력치가 버그 제거에도 쓰인다는 안내.
    const string KEY_TUT13_5    = "onboarding_tutorial13_5_done";
    // 튜토리얼 14-1 완료 — 디버깅 끝나고 DevelopmentResultPanel 활성화 직후, 첫 게임 완성 + 기여도 확인 안내.
    const string KEY_TUT14_1    = "onboarding_tutorial14_1_done";
    // 튜토리얼 15-1/15-2 완료 — 마케팅 패널 열릴 때, 마케팅 중요성 + LeftPanel 두 번째 슬롯 안내.
    const string KEY_TUT15      = "onboarding_tutorial15_done";
    // 튜토리얼 16-1 완료 — 판매 패널 열리고 1주차 매출 bar가 오르는 동안(시간 정지 없이), 대박 반응.
    const string KEY_TUT16_1    = "onboarding_tutorial16_1_done";
    // 튜토리얼 17-1 완료 — 첫 판매가 끝난 직후(SalesUI.OnSalesComplete), 직원 강화 메뉴 유도.
    const string KEY_TUT17_1    = "onboarding_tutorial17_1_done";
    // 튜토리얼 17-2~17-5 완료 — 강화 패널이 처음 열릴 때(EmployeeListUI.OpenListForEnhance), 강화
    // 안내 + 4강까지 강제성공 체험 + 마무리 대사까지 한 세션.
    const string KEY_TUT17_2    = "onboarding_tutorial17_2_done";
    // 튜토리얼 17-7 완료 — 상인 상점(MerchantShopPanel)이 처음 열릴 때, 구매+닫기 유도 + 17-8/17-9까지
    // 이어지는 전체 세션 완료. 아래 두 서브 플래그는 그 세션 안에서 실제로 서버에 커밋되는 두 지점
    // (상점 닫기=구매 확정 / 아이템 사용=소비 확정)을 별도로 기록해, 재접속 시 이미 커밋된 부분을
    // 중복 실행(재구매/재사용)하지 않고 정확히 그 다음 지점부터 재개하기 위함.
    const string KEY_TUT17_7    = "onboarding_tutorial17_7_done";
    // 17-7 서브: 상점을 실제로 닫아 구매가 서버에 커밋된 시점(MerchantManager.OnShopClosed).
    const string KEY_TUT17_7_SHOP = "onboarding_tutorial17_7_shop_done";
    // 17-8 서브: 아이템을 실제로 사용해 소비/효과가 서버에 커밋된 시점(ItemManager.UseItem).
    const string KEY_TUT17_8_USED = "onboarding_tutorial17_8_used_done";
    // 17-8 내부 잠금 해제 신호 — EmployeeCardUI 아이템/강화 버튼이 이 플래그가 true가 될 때까지
    // 잠겨있음(ApplyItemTrainingLock). ⚠️ 예전엔 "18-1"이라는 이름을 썼지만 실제 18-1(파산 경고 대사)과는
    // 무관한, 17-8 흐름 내부의 구현 디테일이라 이름 충돌을 피하려고 17_8_UNLOCK으로 개명.
    const string KEY_TUT17_8_UNLOCK = "onboarding_tutorial17_8_unlock_done";
    // 튜토리얼 18-1~18-4 완료 — 지출/연봉·사무실비/파산 경고 대사 4개짜리 all-or-nothing 구간
    // (중간 상태 커밋이 전혀 없으므로, 5-1~6-2/10-1~10-3/17-1~17-6과 동일하게 끊기면 18-1부터 다시 재생).
    const string KEY_TUT18      = "onboarding_tutorial18_done";
    // 튜토리얼 19-1 완료 — "이제 혼자 해보라"는 마무리 대사(강조 없음).
    const string KEY_TUT19      = "onboarding_tutorial19_done";
    // 튜토리얼 20-1/20-2 완료 — 2번째 프로젝트(2사이클)의 기획팀장 점수에서 강제 burst 연출 후 반응
    // 대사(강조 없음). ⚠️ 1사이클의 기획팀장점수 튜토리얼(Tutorial7Done)과는 완전히 별개 — 이 단계는
    // CompletedProjectManager.completedProjects.Count==1(=2번째 프로젝트 진행 중)로 구분해 절대 혼동되지 않는다.
    const string KEY_TUT20      = "onboarding_tutorial20_done";

    // 튜토리얼 dim 이 떠 있는 동안 true (세션 전용, 저장 안 함).
    // 시간 정지 중에도 메뉴 버튼을 숨기지 않도록 MenuController 가 참조.
    private static bool _tutorialActiveFlag;
    public static bool TutorialActive
    {
        // 19-1(핸드오프 대사) 완료 ~ 20(2번째 프로젝트 기획팀장점수) 완료 사이엔 dim 없이도 시간이 계속
        // 멈춰있는 구간이라(GameSceneInitializer가 재접속 시 재조립) 세션 플래그(_tutorialActiveFlag)만으로는
        // Start() 실행 순서에 따라 타이밍이 어긋날 수 있다 — 그래서 이 구간은 저장된 플래그로 직접 계산해
        // 순서와 무관하게 항상 정확하게 만든다.
        get => _tutorialActiveFlag || (Tutorial19Done && !Tutorial20Done);
        set => _tutorialActiveFlag = value;
    }

    public static bool IntroDone => PlayerPrefs.GetInt(KEY_INTRO, 0) == 1;
    public static void MarkIntroDone()
    {
        PlayerPrefs.SetInt(KEY_INTRO, 1);
        PlayerPrefs.Save();
    }

    public static bool TutorialDone => PlayerPrefs.GetInt(KEY_TUTORIAL, 0) == 1;
    public static void MarkTutorialDone()
    {
        PlayerPrefs.SetInt(KEY_TUTORIAL, 1);
        PlayerPrefs.Save();
    }

    // 첫 채용(온보딩) — 면접 대기를 3주가 아닌 1주로 단축, 1회만.
    public static bool FirstHireDone => PlayerPrefs.GetInt(KEY_FIRST_HIRE, 0) == 1;
    public static void MarkFirstHireDone()
    {
        PlayerPrefs.SetInt(KEY_FIRST_HIRE, 1);
        PlayerPrefs.Save();
    }

    // 튜토리얼 3-1 — 첫 ConfirmHirePanel 노출 시 1회. 버튼 강조 없이 TutorialPanel 대사만 재생.
    public static bool Tutorial3Done => PlayerPrefs.GetInt(KEY_TUT3, 0) == 1;
    public static void MarkTutorial3Done()
    {
        PlayerPrefs.SetInt(KEY_TUT3, 1);
        PlayerPrefs.Save();
    }

    // 튜토리얼 5-1~5-4 — 두 번째(진짜) 채용이 확정되고 2초 뒤 시작, 프로젝트 개발 시작 확정까지.
    // (Tutorial5Pending은 여기서 해제 안 함 — 아래 참고)
    public static bool Tutorial5Done => PlayerPrefs.GetInt(KEY_TUT5, 0) == 1;
    public static void MarkTutorial5Done()
    {
        PlayerPrefs.SetInt(KEY_TUT5, 1);
        PlayerPrefs.Save();
    }

    // 두 번째(진짜) 채용 확정 시 HiringUI.DoHire가 호출 — 5-1 재생 시작 "전"에 무장해 둬야 그 사이에
    // 재접속해도(2초 지연 포함) 다음 진입 시 TutorialController.Start()가 5-1부터 재개할 수 있다.
    // ⚠️ 5-4(프로젝트 개발 시작)~6-3(팀장 확정)까지는 서버에 아무것도 저장되지 않는다(팀장점수 burst
    // 시점에만 값 잠금 저장) — 그래서 이 pending은 Tutorial5Done이 아니라 Tutorial6Done에서 해제한다.
    // 즉 5-1~6-2 구간 어디서 중단/재접속하든(플랫폼/장르를 이미 골랐어도, 개발을 이미 시작했어도) 서버
    // 상태는 그 이전으로 되돌아가 있으므로 처음(5-1)부터 다시 재생하는 게 실제 게임 상태와 일치한다.
    public static bool Tutorial5Pending => PlayerPrefs.GetInt(KEY_TUT5_PEND, 0) == 1;
    public static void ArmTutorial5()
    {
        if (Tutorial5Done) return;
        PlayerPrefs.SetInt(KEY_TUT5_PEND, 1);
        PlayerPrefs.Save();
    }

    // 튜토리얼 6-1~6-3 — 기획팀장 선택 패널(DispatchPanelUI, Planner)이 처음 열릴 때(재접속 시 자연
    // 재오픈 포함) 마다 재생하다가, 실제로 팀장을 확정하면 1회만 마크.
    public static bool Tutorial6Done => PlayerPrefs.GetInt(KEY_TUT6, 0) == 1;
    public static void MarkTutorial6Done()
    {
        PlayerPrefs.SetInt(KEY_TUT6, 1);
        PlayerPrefs.DeleteKey(KEY_TUT5_PEND); // 5-1 재생 무장 해제 — 이제부터는 재접속해도 5-1로 안 돌아감(7단계 자체 재생 로직으로 이어짐)
        PlayerPrefs.Save();
    }

    // 튜토리얼 7-1~7-6 — 첫 기획팀장 점수 화면이 열릴 때마다(재접속 자연 재오픈 포함) 재생하다가,
    // 4회차(burst)까지 연출이 끝나면 1회만 마크.
    public static bool Tutorial7Done => PlayerPrefs.GetInt(KEY_TUT7, 0) == 1;
    public static void MarkTutorial7Done()
    {
        PlayerPrefs.SetInt(KEY_TUT7, 1);
        PlayerPrefs.Save();
    }

    // 튜토리얼 8-1 — 팀장점수 패널을 실제로 닫은(confirm) 직후, 개발 화면의 SupriseQuestUI(도전 과제)
    // 강조. 강조만 하고 클릭 대기는 없음(대사 3줄 끝나면 자동 종료).
    public static bool Tutorial8Done => PlayerPrefs.GetInt(KEY_TUT8, 0) == 1;
    public static void MarkTutorial8Done()
    {
        PlayerPrefs.SetInt(KEY_TUT8, 1);
        PlayerPrefs.Save();
    }

    public static bool Tutorial9Done => PlayerPrefs.GetInt(KEY_TUT9, 0) == 1;
    public static void MarkTutorial9Done()
    {
        PlayerPrefs.SetInt(KEY_TUT9, 1);
        PlayerPrefs.Save();
    }

    // 튜토리얼 10단계 — 직원 카드/만족도 소개 + AcWar 이벤트 체험(10-1~10-3).
    public static bool Tutorial10Done => PlayerPrefs.GetInt(KEY_TUT10, 0) == 1;
    public static void MarkTutorial10Done()
    {
        PlayerPrefs.SetInt(KEY_TUT10, 1);
        PlayerPrefs.Save();
    }

    // 튜토리얼 12-1 — 아트팀장 선택(75% 진행도, 아트 직원이 없어 CEO만 후보인 상황) 안내 완료.
    public static bool Tutorial12Done => PlayerPrefs.GetInt(KEY_TUT12, 0) == 1;
    public static void MarkTutorial12Done()
    {
        PlayerPrefs.SetInt(KEY_TUT12, 1);
        PlayerPrefs.Save();
    }

    // 튜토리얼 13-1/13-2 — 진행도 ~95% 확정 창의성 틱 발동 직후, 창의성 점수/블록 안내 완료.
    public static bool Tutorial13Done => PlayerPrefs.GetInt(KEY_TUT13, 0) == 1;
    public static void MarkTutorial13Done()
    {
        PlayerPrefs.SetInt(KEY_TUT13, 1);
        PlayerPrefs.Save();
    }

    // 튜토리얼 13-4 — 창의성 미니게임 Sq/T_U 블록 지정 위치 유도 완료.
    public static bool Tutorial13_4Done => PlayerPrefs.GetInt(KEY_TUT13_4, 0) == 1;
    public static void MarkTutorial13_4Done()
    {
        PlayerPrefs.SetInt(KEY_TUT13_4, 1);
        PlayerPrefs.Save();
    }

    // 튜토리얼 13-5 — 디버깅 시작 직후, 창의성 능력치가 버그 제거에도 쓰인다는 안내 완료.
    // ⚠️ 여기가 "마지막 단계"라서 특별 취급하지 않는다 — MenuController는 더 이상 특정 단계 완료를
    // 직접 구독하지 않고 TutorialController.IsFullyDone()을 폴링한다. 새 단계를 추가할 때 이 파일에는
    // KEY_TUT.../Tutorial...Done/Mark...Done만 늘리고, TutorialController.IsFullyDone()에 그 단계의
    // needStepN 한 줄만 추가하면 된다(다른 파일은 손댈 필요 없음).
    public static bool Tutorial13_5Done => PlayerPrefs.GetInt(KEY_TUT13_5, 0) == 1;
    public static void MarkTutorial13_5Done()
    {
        PlayerPrefs.SetInt(KEY_TUT13_5, 1);
        PlayerPrefs.Save();
    }

    // 튜토리얼 14-1 — 디버깅 끝나고 DevelopmentResultPanel 활성화 직후, 첫 게임 완성 + 기여도 확인 안내 완료.
    public static bool Tutorial14_1Done => PlayerPrefs.GetInt(KEY_TUT14_1, 0) == 1;
    public static void MarkTutorial14_1Done()
    {
        PlayerPrefs.SetInt(KEY_TUT14_1, 1);
        PlayerPrefs.Save();
    }

    // 튜토리얼 15-1/15-2 — 마케팅 패널 열릴 때, 마케팅 중요성 + LeftPanel 두 번째 슬롯 안내 완료.
    public static bool Tutorial15Done => PlayerPrefs.GetInt(KEY_TUT15, 0) == 1;
    public static void MarkTutorial15Done()
    {
        PlayerPrefs.SetInt(KEY_TUT15, 1);
        PlayerPrefs.Save();
    }

    // 튜토리얼 16-1 — 판매 패널 열리고 1주차 매출 bar가 오르는 동안(시간 정지 없이), 대박 반응 완료.
    public static bool Tutorial16_1Done => PlayerPrefs.GetInt(KEY_TUT16_1, 0) == 1;
    public static void MarkTutorial16_1Done()
    {
        PlayerPrefs.SetInt(KEY_TUT16_1, 1);
        PlayerPrefs.Save();
    }

    // 테스트용 — 16-1 Done 플래그만 원복(ResetAll처럼 다른 단계까지 통째로 리셋하지 않음). 실제로
    // 아직 통과 안 한 단계를 테스트 중 임시로 Done 처리했다가 되돌릴 때 사용.
    public static void ResetTutorial16_1()
    {
        PlayerPrefs.DeleteKey(KEY_TUT16_1);
        PlayerPrefs.Save();
    }

    // 튜토리얼 17-1 — 첫 판매가 끝난 직후, 직원 강화 메뉴 유도 완료.
    public static bool Tutorial17_1Done => PlayerPrefs.GetInt(KEY_TUT17_1, 0) == 1;
    public static void MarkTutorial17_1Done()
    {
        PlayerPrefs.SetInt(KEY_TUT17_1, 1);
        PlayerPrefs.Save();
    }

    // 테스트용 — 17-1 Done 플래그만 원복.
    public static void ResetTutorial17_1()
    {
        PlayerPrefs.DeleteKey(KEY_TUT17_1);
        PlayerPrefs.Save();
    }

    // 튜토리얼 17-2~17-5 — 강화 패널 안내 + 강화 4회 체험 + 마무리 대사 완료.
    public static bool Tutorial17_2Done => PlayerPrefs.GetInt(KEY_TUT17_2, 0) == 1;
    public static void MarkTutorial17_2Done()
    {
        PlayerPrefs.SetInt(KEY_TUT17_2, 1);
        PlayerPrefs.Save();
    }

    // 테스트용 — 17-2~17-5 Done 플래그만 원복(ResetAll처럼 다른 단계까지 통째로 리셋하지 않음).
    public static void ResetTutorial17_2()
    {
        PlayerPrefs.DeleteKey(KEY_TUT17_2);
        PlayerPrefs.Save();
    }

    // 튜토리얼 17-7 — 상인 상점이 처음 열릴 때, 구매+닫기 유도 완료.
    public static bool Tutorial17_7Done => PlayerPrefs.GetInt(KEY_TUT17_7, 0) == 1;
    public static void MarkTutorial17_7Done()
    {
        PlayerPrefs.SetInt(KEY_TUT17_7, 1);
        PlayerPrefs.Save();
    }

    // 테스트용 — 17-7~17-9-2 Done 플래그만 원복(서브 플래그 포함 — 전부 처음부터 다시 볼 수 있게).
    public static void ResetTutorial17_7()
    {
        PlayerPrefs.DeleteKey(KEY_TUT17_7);
        PlayerPrefs.DeleteKey(KEY_TUT17_7_SHOP);
        PlayerPrefs.DeleteKey(KEY_TUT17_8_USED);
        PlayerPrefs.Save();
    }

    // 17-7 서브 — 상점을 실제로 닫아 구매가 서버에 커밋된 시점 완료.
    public static bool Tutorial17_7ShopDone => PlayerPrefs.GetInt(KEY_TUT17_7_SHOP, 0) == 1;
    public static void MarkTutorial17_7ShopDone()
    {
        PlayerPrefs.SetInt(KEY_TUT17_7_SHOP, 1);
        PlayerPrefs.Save();
    }

    // 17-8 서브 — 아이템을 실제로 사용해 소비/효과가 서버에 커밋된 시점 완료.
    public static bool Tutorial17_8UsedDone => PlayerPrefs.GetInt(KEY_TUT17_8_USED, 0) == 1;
    public static void MarkTutorial17_8UsedDone()
    {
        PlayerPrefs.SetInt(KEY_TUT17_8_USED, 1);
        PlayerPrefs.Save();
    }

    // 17-8 내부 잠금 해제 신호 — 실제 콘텐츠는 17-8 흐름 자체(EmployeeCardUI.ApplyItemTrainingLock 참고).
    public static bool Tutorial17_8UnlockDone => PlayerPrefs.GetInt(KEY_TUT17_8_UNLOCK, 0) == 1;
    public static void MarkTutorial17_8UnlockDone()
    {
        PlayerPrefs.SetInt(KEY_TUT17_8_UNLOCK, 1);
        PlayerPrefs.Save();
    }

    // 테스트용 — 17-8 잠금 해제(아이템/강화 버튼) 플래그만 원복.
    public static void ResetTutorial17_8Unlock()
    {
        PlayerPrefs.DeleteKey(KEY_TUT17_8_UNLOCK);
        PlayerPrefs.Save();
    }

    // 튜토리얼 18-1~18-4 — 지출/연봉·사무실비/파산 경고 대사, all-or-nothing.
    public static bool Tutorial18Done => PlayerPrefs.GetInt(KEY_TUT18, 0) == 1;
    public static void MarkTutorial18Done()
    {
        PlayerPrefs.SetInt(KEY_TUT18, 1);
        PlayerPrefs.Save();
    }

    // 테스트용 — 18-1~18-4 Done 플래그만 원복.
    public static void ResetTutorial18()
    {
        PlayerPrefs.DeleteKey(KEY_TUT18);
        PlayerPrefs.Save();
    }

    // 튜토리얼 19-1 — "이제 혼자 해보라"는 마무리 대사, 강조 없음.
    public static bool Tutorial19Done => PlayerPrefs.GetInt(KEY_TUT19, 0) == 1;
    public static void MarkTutorial19Done()
    {
        PlayerPrefs.SetInt(KEY_TUT19, 1);
        PlayerPrefs.Save();
    }

    // 테스트용 — 19-1 Done 플래그만 원복.
    public static void ResetTutorial19()
    {
        PlayerPrefs.DeleteKey(KEY_TUT19);
        PlayerPrefs.Save();
    }

    // 튜토리얼 20-1/20-2 — 2번째 프로젝트 기획팀장점수 강제 burst 반응 대사, 강조 없음.
    public static bool Tutorial20Done => PlayerPrefs.GetInt(KEY_TUT20, 0) == 1;
    public static void MarkTutorial20Done()
    {
        PlayerPrefs.SetInt(KEY_TUT20, 1);
        PlayerPrefs.Save();
    }

    // 테스트용 — 20-1/20-2 Done 플래그만 원복.
    public static void ResetTutorial20()
    {
        PlayerPrefs.DeleteKey(KEY_TUT20);
        PlayerPrefs.Save();
    }

    // 테스트용 — 온보딩 재노출 (TestResetBtn 등에서 빌드에서도 호출 가능하도록 UNITY_EDITOR 가드 제거)
    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(KEY_INTRO);
        PlayerPrefs.DeleteKey(KEY_TUTORIAL);
        PlayerPrefs.DeleteKey(KEY_FIRST_HIRE);
        PlayerPrefs.DeleteKey(KEY_TUT3);
        PlayerPrefs.DeleteKey(KEY_TUT5);
        PlayerPrefs.DeleteKey(KEY_TUT5_PEND);
        PlayerPrefs.DeleteKey(KEY_TUT6);
        PlayerPrefs.DeleteKey(KEY_TUT7);
        PlayerPrefs.DeleteKey(KEY_TUT8);
        PlayerPrefs.DeleteKey(KEY_TUT9);
        PlayerPrefs.DeleteKey(KEY_TUT10);
        PlayerPrefs.DeleteKey(KEY_TUT12);
        PlayerPrefs.DeleteKey(KEY_TUT13);
        PlayerPrefs.DeleteKey(KEY_TUT13_4);
        PlayerPrefs.DeleteKey(KEY_TUT13_5);
        PlayerPrefs.DeleteKey(KEY_TUT14_1);
        PlayerPrefs.DeleteKey(KEY_TUT15);
        PlayerPrefs.DeleteKey(KEY_TUT16_1);
        PlayerPrefs.DeleteKey(KEY_TUT17_1);
        PlayerPrefs.DeleteKey(KEY_TUT17_2);
        PlayerPrefs.DeleteKey(KEY_TUT17_7);
        PlayerPrefs.DeleteKey(KEY_TUT17_7_SHOP);
        PlayerPrefs.DeleteKey(KEY_TUT17_8_USED);
        PlayerPrefs.DeleteKey(KEY_TUT17_8_UNLOCK);
        PlayerPrefs.DeleteKey(KEY_TUT18);
        PlayerPrefs.DeleteKey(KEY_TUT19);
        PlayerPrefs.DeleteKey(KEY_TUT20);
        PlayerPrefs.Save();
        Debug.Log("[Onboarding] 플래그 리셋 — 다음 진입 시 컷씬+튜토리얼 재노출");
    }
}
