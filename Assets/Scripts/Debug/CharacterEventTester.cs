using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// ──────────────────────────────────────────────────────────────────────────
// 캐릭터 전용 이벤트(유니크) 테스트 패널 — 에디터 플레이 모드에서만 자동 생성.
//
// 유니크 직원을 직접 채용/조건 맞추기 어려우므로:
//   1) 6 캐릭터를 Unique 등급으로 즉시 채용 (마스터 풀 → grade=Unique, 능력치 max)
//   2) 시간/확률 게이트된 이벤트(유리멘탈/오다주웠다/신의축복)를 버튼으로 즉시 발동
//   3) 신의 축복은 주사위 1~6 을 강제 지정해 6종 효과를 각각 확인
//   4) 우기 해고 → 신의 축복 지속효과 즉시 중단 확인
//
// 화면 우상단 "이벤트 테스트" 버튼으로 패널 토글. (F9 로도 토글)
// ⚠️ 에디터 전용 — 빌드에는 자동 생성 안 됨(UNITY_EDITOR 가드). 테스트 후 따로 제거 불필요.
// ──────────────────────────────────────────────────────────────────────────
public class CharacterEventTester : MonoBehaviour
{
#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        if (FindObjectOfType<CharacterEventTester>() != null) return;
        var go = new GameObject("[CharacterEventTester]");
        go.AddComponent<CharacterEventTester>();
        DontDestroyOnLoad(go);
    }
#endif

    bool _open = false;
    Vector2 _scroll;
    string _status = "";
    int _targetEnhanceLevel = 25; // 강화 목표 레벨(등급별 최대치로 clamp)

    // 마스터 ID → 표시명 (CharacterTraitApplier.Directory 와 동일 매핑)
    static readonly (string masterId, string label)[] Chars =
    {
        ("kim_01",       "김아무개 (유리멘탈)"),
        ("otaku_01",     "오타쿠 (버튜버데뷔)"),
        ("goldspoon_01", "금수저 (오다주웠다)"),
        ("ugi_01",       "우기 (신의축복)"),
        ("genius_01",    "천재 (잠깨우기)"),
        ("hunsu_01",     "훈수쟁이 (약점극복)"),
    };

    // MCP 등 외부에서 버튼 클릭 없이 16-1 테스트를 트리거하기 위한 훅 — 인스펙터/스크립트로 true를 찍으면
    // 다음 프레임에 QuickTest16_1()이 실행되고 자동으로 false로 되돌아간다.
    public bool debugTrigger16_1 = false;

    void Update()
    {
        // New Input System (activeInputHandler=1) — UnityEngine.Input 은 예외 발생하므로 Keyboard.current 사용.
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.f9Key.wasPressedThisFrame) _open = !_open;

        if (debugTrigger16_1)
        {
            debugTrigger16_1 = false;
            QuickTest16_1(ProjectScale.Small);
        }
    }

    void OnGUI()
    {
        // 토글 버튼 (항상 우상단)
        if (GUI.Button(new Rect(Screen.width - 170, 10, 160, 32), _open ? "이벤트 테스트 ▲" : "이벤트 테스트 ▼"))
            _open = !_open;
        if (!_open) return;

        var area = new Rect(Screen.width - 470, 50, 460, Screen.height - 80);
        GUILayout.BeginArea(area, GUI.skin.box);
        _scroll = GUILayout.BeginScrollView(_scroll);

        var em = EmployeeManager.Instance;
        if (em == null) { GUILayout.Label("EmployeeManager 없음 (인게임에서 실행하세요)"); GUILayout.EndScrollView(); GUILayout.EndArea(); return; }

        GUILayout.Label("<b>━━ 유니크 직원 채용 ━━</b>", Rich());
        GUILayout.Label("마스터 풀에서 Unique 등급 + 능력치 max 로 즉시 채용");
        foreach (var (masterId, label) in Chars)
        {
            if (GUILayout.Button($"채용: {label}")) HireUnique(masterId);
        }

        GUILayout.Space(8);
        GUILayout.Label("<b>━━ 보유 유니크 직원 ━━</b>", Rich());
        foreach (var e in em.ownedEmployees)
        {
            if (e.isCEO) continue;
            string evt = CharacterTraitApplier.ResolveEventType(e);
            if (string.IsNullOrEmpty(evt) || e.grade < EmployeeGrade.Unique) continue;
            GUILayout.Label($"• {e.employeeName} [{e.grade}] / {evt} / 만족도 {e.satisfaction} / 배율 {e.cosmicEnergyPercent}%"
                + (e.cosmicFrozen ? " (고정)" : "")
                + (e.godBlessingStatPercent > 0 ? $" / 축복+{e.godBlessingStatPercent}%" : "")
                + (e.godBlessingSalesActive ? " / 매출축복" : ""));
        }

        GUILayout.Space(8);
        GUILayout.Label("<b>━━ 이벤트 강제 발동 ━━</b>", Rich());

        if (GUILayout.Button("유리멘탈 회복 (만족도50→Trigger)")) ForceKim();
        if (GUILayout.Button("오다 주웠다 (아이템 지급)")) ForceEvent("GoldspoonUnique");

        GUILayout.Label("신의 축복 — 주사위 강제:");
        GUILayout.BeginHorizontal();
        for (int d = 1; d <= 6; d++)
            if (GUILayout.Button($"{d}")) ForceGodBlessing(d);
        GUILayout.EndHorizontal();
        GUILayout.Label("1=꽝 2=우기배율고정 3=랜덤+10% 4=우기만족+20 5=전원+10 6=매출+10%");
        if (GUILayout.Button("신의 축복 (랜덤 주사위)")) ForceGodBlessing(0);

        GUILayout.Space(4);
        if (GUILayout.Button("약점 극복 (훈수, 개발중 필요)")) { CharacterUniqueEvents.CheckWeaknessOvercome(); Set("약점극복 호출 (개발중·훈수 보유 시 발동)"); }
        if (GUILayout.Button("버튜버 데뷔 (오타쿠, 개발중 필요)")) { CharacterUniqueEvents.CheckVtuberDebut(null); Set("버튜버 호출 (장르=오타쿠고정장르 일치 시 발동)"); }
        if (GUILayout.Button("잠 깨우기 (천재, 개발중 필요)")) ForceEvent("GeniusUnique");

        GUILayout.Space(8);
        GUILayout.Label("<b>━━ 채용 / 강화 테스트 ━━</b>", Rich());
        HiringUI.InstantInterview = GUILayout.Toggle(HiringUI.InstantInterview, " 채용 즉시 공개 (3주 대기 생략 → 바로 후보 리스트)");
        GUILayout.BeginHorizontal();
        GUILayout.Label($"목표 강화 레벨: {_targetEnhanceLevel}", GUILayout.Width(150));
        if (GUILayout.Button("-", GUILayout.Width(36))) _targetEnhanceLevel = Mathf.Max(0, _targetEnhanceLevel - 1);
        if (GUILayout.Button("+", GUILayout.Width(36))) _targetEnhanceLevel = Mathf.Min(25, _targetEnhanceLevel + 1);
        GUILayout.EndHorizontal();
        if (GUILayout.Button($"채용된 전 직원 +{_targetEnhanceLevel}강까지 강화 (등급 최대치 제한)")) EnhanceAll(_targetEnhanceLevel);

        GUILayout.Space(8);
        GUILayout.Label("<b>━━ 유틸 ━━</b>", Rich());
        if (GUILayout.Button("전 직원 만족도 -30 (유리멘탈 조건/효과 확인)")) AllSatisfaction(-30);
        if (GUILayout.Button("전 직원 만족도 +30")) AllSatisfaction(+30);
        if (GUILayout.Button("우기 해고 (신의축복 지속효과 즉시중단 확인)")) FireUgi();
        if (GUILayout.Button($"매출축복 활성? {CharacterUniqueEvents.GetGodBlessingSalesBonus():0.00}")) { }
        GUILayout.Label($"우기 보유: {(CharacterUniqueEvents.HasUniqueUgi() ? "O" : "X")}");

        GUILayout.Space(8);
        GUILayout.Label("<b>━━ 엘리베이터 테스트 ━━</b>", Rich());
        if (GUILayout.Button("직원 1명 → 셀 (11,2,0) 로 이동 (엘리베이터 경유)")) SendToElevatorEntry();

        GUILayout.Space(8);
        GUILayout.Label("<b>━━ 팀장점수 테스트 (프로젝트 진행에 전혀 영향 없음, 확정해도 그냥 닫힘) ━━</b>", Rich());
        foreach (var e in em.ownedEmployees)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(e.employeeName, GUILayout.Width(110));
            if (GUILayout.Button("기획")) DevelopmentManager.Instance?.TestLeaderScore(e, LeaderType.Planner);
            if (GUILayout.Button("개발")) DevelopmentManager.Instance?.TestLeaderScore(e, LeaderType.Programmer);
            if (GUILayout.Button("아트")) DevelopmentManager.Instance?.TestLeaderScore(e, LeaderType.Artist);
            if (GUILayout.Button("스탯로그", GUILayout.Width(70))) LogStatBreakdown(e);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(8);
        GUILayout.Label("<b>━━ 모달 차단 테스트 ━━</b>", Rich());
        if (GUILayout.Button("AlertUI 띄우기 (뒤 클릭 차단 확인)")) AlertUI.Instance?.Show("테스트 알림 — 이 뒤의 버튼/메뉴가 안 눌려야 정상");
        if (GUILayout.Button("ConfirmUI 띄우기")) ConfirmUI.Instance?.Show("테스트 확인", () => Set("확인됨"), () => Set("취소됨"));
        if (GUILayout.Button("LoanUI 띄우기 (MenuCanvas 크로스 차단)")) LoanUI.Instance?.Open();

        GUILayout.Space(8);
        GUILayout.Label("<b>━━ 온보딩 ━━</b>", Rich());
        GUILayout.Label($"IntroDone: {OnboardingState.IntroDone} / TutorialDone: {OnboardingState.TutorialDone} / RunState.tutorial: {RunStateManager.Instance?.IsTutorial}");
        GUILayout.Label($"3:{OnboardingState.Tutorial3Done} 5:{OnboardingState.Tutorial5Done}(pend:{OnboardingState.Tutorial5Pending}) 6:{OnboardingState.Tutorial6Done} 7:{OnboardingState.Tutorial7Done} 8:{OnboardingState.Tutorial8Done} 9:{OnboardingState.Tutorial9Done} 10:{OnboardingState.Tutorial10Done} 12:{OnboardingState.Tutorial12Done}");
#if UNITY_EDITOR
        if (GUILayout.Button("온보딩 리셋 (컷씬/튜토리얼 재노출)")) { OnboardingState.ResetAll(); Set("온보딩 리셋 — LoadingScene부터 다시 실행"); }
        if (GUILayout.Button("온보딩 들어가기 (리셋 + RunState.tutorial=true + 씬 재시작)"))
        {
            OnboardingState.ResetAll();
            if (RunStateManager.Instance != null)
            {
                RunStateManager.Instance.SetTutorial(true, success =>
                {
                    if (success) { Set("RunState.tutorial=true 저장 완료 — 씬 재시작"); SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
                    else Set("RunState.tutorial 저장 실패");
                });
            }
            else
                Set("RunStateManager 인스턴스 없음");
        }
        if (GUILayout.Button("튜토리얼 완전 해제 (전부 완료 처리 + RunState.tutorial=false)")) DisableTutorial();
        GUILayout.Label("아래 버튼은 이전 단계를 전부 '완료' 처리만 함 — 그 단계의 실제 트리거(패널 열기 등)는\n직접 눌러야 재생됨. 단, 7/9(팀장점수)는 위 '팀장점수 테스트' 버튼이 실제 게임 상태 없이\n곧바로 트리거해줌(테스트라 프로젝트/직원 데이터엔 영향 없음). TutorialController가 죽지\n않은 상태(그 단계가 아직 false)일 때만 유효.");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("3부터")) JumpToOnboardingStep(3);
        if (GUILayout.Button("5부터")) JumpToOnboardingStep(5);
        if (GUILayout.Button("6부터")) JumpToOnboardingStep(6);
        if (GUILayout.Button("7부터")) JumpToOnboardingStep(7);
        if (GUILayout.Button("8부터")) JumpToOnboardingStep(8);
        if (GUILayout.Button("9부터")) JumpToOnboardingStep(9);
        if (GUILayout.Button("10부터")) JumpToOnboardingStep(10);
        if (GUILayout.Button("12부터")) JumpToOnboardingStep(12);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("7-1~7-6 지금 바로 테스트 (기획팀장, 아무 직원)")) QuickTestLeaderTutorial(7, LeaderType.Planner);
        if (GUILayout.Button("9-1~9-3 지금 바로 테스트 (개발팀장, 아무 직원)")) QuickTestLeaderTutorial(9, LeaderType.Programmer);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("16-1만 지금 바로 테스트 (SalesUI 강제 오픈 + 그래프 애니메이션):", Rich());
        if (GUILayout.Button("소형")) QuickTest16_1(ProjectScale.Small);
        if (GUILayout.Button("중형")) QuickTest16_1(ProjectScale.Medium);
        if (GUILayout.Button("대형")) QuickTest16_1(ProjectScale.Large);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("16-1 고매출 테스트 (튜토리얼 고정값 우회, 1위대):", Rich());
        if (GUILayout.Button("소형")) QuickTest16_1HighRevenue(ProjectScale.Small);
        if (GUILayout.Button("중형")) QuickTest16_1HighRevenue(ProjectScale.Medium);
        if (GUILayout.Button("대형")) QuickTest16_1HighRevenue(ProjectScale.Large);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("평론가UI(CriticReviewUI) 점수 직접 테스트:", Rich());
        if (GUILayout.Button("45점 (Low 반응)")) QuickTestCriticReview(45);
        if (GUILayout.Button("80점 (High 반응)")) QuickTestCriticReview(80);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("2사이클 시작 (1~16 완료 처리 + 17-1부터)")) JumpToCycle2();
        if (GUILayout.Button("19부터 (1~18 완료 처리 + 19-1 강제 재생)")) JumpToTutorial19();
#endif

        GUILayout.Space(6);
        GUILayout.Label("<b>상태:</b> " + _status, Rich());

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    GUIStyle Rich() { var s = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true }; return s; }
    void Set(string msg) { _status = msg; Debug.Log("[EventTester] " + msg); }

    // step보다 앞선 온보딩 단계는 전부 완료 처리, step 자신은 안 건드림(false로 남겨 그 단계가 다시 뜨게).
    // ⚠️ 이건 OnboardingState 플래그만 조작 — 그 단계가 실제로 필요로 하는 게임 상태(직원 존재, 프로젝트
    // 진행 중 등)까지 만들어주진 않음. 팀장점수 화면(7/9단계)은 위 "팀장점수 테스트" 버튼으로 게임 상태
    // 없이도 트리거 가능. TutorialController.Instance는 needStep10(=!Tutorial10Done)이 살아있는 한 죽지
    // 않으므로, step을 10 미만으로 잡아도(예: 6부터) 10까지 전부 아직 false라 안전하게 유지됨.
    void JumpToOnboardingStep(int step)
    {
        // needStep1(=!OnboardingState.TutorialDone && IsTutorial)이 여기서 안 풀리면 이후 3~19단계를
        // 전부 완료 처리해도 IsFullyDoneInternal()의 baseDone이 영원히 false로 남아 메뉴가 절대 안 풀린다
        // (실제로 겪은 버그 — "17-1부터"/"19부터"를 눌러도 메뉴 버튼이 안 나타나던 원인).
        OnboardingState.MarkIntroDone();
        OnboardingState.MarkTutorialDone();
        if (step > 3) OnboardingState.MarkTutorial3Done();
        if (step > 5) OnboardingState.MarkTutorial5Done();
        if (step > 6) OnboardingState.MarkTutorial6Done(); // Tutorial5Pending도 여기서 같이 해제됨
        if (step > 7) OnboardingState.MarkTutorial7Done();
        if (step > 8) OnboardingState.MarkTutorial8Done();
        if (step > 9) OnboardingState.MarkTutorial9Done();
        if (step > 10) OnboardingState.MarkTutorial10Done();
        if (step > 12) OnboardingState.MarkTutorial12Done(); // 11단계는 아직 미구현
        Set($"온보딩 {step}단계 전까지 완료 처리함 — {step}단계의 실제 트리거를 직접 눌러줘");
    }

    // 온보딩 전체(컷씬 포함)를 완료 처리 + RunState.tutorial=false — 이후 세션은 튜토리얼 훅이 전혀
    // 안 걸리고 일반 플레이만 된다(다른 기능 테스트할 때 튜토리얼이 끼어드는 걸 막는 용도).
    // ⚠️ JumpToOnboardingStep(13)은 3~12단계까지만 Done 처리함 — 13단계 이후는 여기서 직접 마저 채워야
    // TutorialController.IsFullyDoneInternal()의 needStep13~19(20,21,22,23,24) 체크가 전부 풀린다. 이걸 빠뜨리면
    // IsMenuUnlockReady()가 영원히 false로 남아 MenuController.WaitOnboardingFullyDone()이 menuButton을
    // 절대 다시 켜주지 않는다(메뉴 자체를 못 열게 되어 다른 메뉴 UI 테스트도 전부 막힘).
    void DisableTutorial()
    {
        OnboardingState.MarkIntroDone();
        OnboardingState.MarkTutorialDone();
        OnboardingState.MarkFirstHireDone();
        JumpToOnboardingStep(13); // 3~12단계 완료 처리
        OnboardingState.MarkTutorial13Done();
        OnboardingState.MarkTutorial13_4Done();
        OnboardingState.MarkTutorial13_5Done();
        OnboardingState.MarkTutorial14_1Done();
        OnboardingState.MarkTutorial15Done();
        OnboardingState.MarkTutorial16_1Done();
        OnboardingState.MarkTutorial17_1Done();
        OnboardingState.MarkTutorial17_2Done();
        // ⚠️ 17-7Done(전체)만 켜고 서브 플래그(Shop/Used/Unlock)를 빠뜨리면, TutorialController.Start()의
        // 재개 로직이 "17-2는 끝났는데 17-7Shop이 아직"으로 읽어서 재접속/씬 재진입마다 PlayTutorial17_6()
        // (상인 소환+하이라이트)을 계속 다시 실행시킨다 — 실제로 겪은 버그. 21_1/22_1/23_1도 동일한 구조
        // (부모만 Done이고 서브가 비어있으면 그 사이 지점부터 재개 로직이 다시 돈다)라 전부 같이 마크해야 함.
        OnboardingState.MarkTutorial17_7ShopDone();
        OnboardingState.MarkTutorial17_8UsedDone();
        OnboardingState.MarkTutorial17_8UnlockDone();
        OnboardingState.MarkTutorial17_7Done();
        OnboardingState.MarkTutorial18Done();
        OnboardingState.MarkTutorial19Done();
        OnboardingState.MarkTutorial20Done();
        OnboardingState.MarkTutorial21Done();
        OnboardingState.MarkTutorial21_1Done();
        OnboardingState.MarkTutorial22Done();
        OnboardingState.MarkTutorial22_1Done();
        OnboardingState.MarkTutorial23Done();
        OnboardingState.MarkTutorial23_1Done();
        OnboardingState.MarkTutorial24Done(); // 13~24(서브 포함) 전부 Done — IsFullyDone/IsMenuUnlockReady 둘 다 통과
        // ⚠️ MarkTutorial24Done()은 플래그만 세울 뿐 PlayTutorial24() 코루틴을 타지 않으므로
        // GameTimeManager.TriggerBankruptcy()가 호출되지 않는다(디버그 버튼이 실제로 런을 끝내버리면 안 됨).

        // 버튼을 눌렀을 때 하필 다른 PlayTutorialX() 코루틴이 이미 화면에 하이라이트/TutorialPanel/시간정지를
        // 걸어둔 채로 진행 중이었을 수 있음 — 위에서 Mark*Done()으로 플래그만 다 켜봤자 그 코루틴 자체는
        // 안 죽어서 하이라이트/패널이 화면에 고아로 남고, 19~20 구간의 AllowMovementWhileStopped/LockTime
        // 하드락도 정상 해제 경로(DevelopmentManager.StartDeveloping())를 안 타 영구히 안 풀린다.
        // TutorialController.ForceResetToNormal()이 진행 중이던 코루틴을 전부 끊고 하이라이트/패널/시간정지/
        // 하드락을 한 번에 정리해 완전한 일반 상태로 되돌린다.
        TutorialController.Instance?.ForceResetToNormal();

        if (RunStateManager.Instance != null)
            RunStateManager.Instance.SetTutorial(false, success => Set(success ? "튜토리얼 완전 해제 완료 (RunState.tutorial=false)" : "온보딩 플래그는 껐지만 RunState 저장 실패"));
        else
            Set("온보딩 플래그는 전부 완료 처리함 (RunStateManager 없음 — tutorial 플래그는 못 끔)");
    }

    // 7단계(기획팀장)/9단계(개발팀장) 전용 — 단계 점프 + 팀장점수 테스트 진입을 한 번에.
    void QuickTestLeaderTutorial(int step, LeaderType type)
    {
        JumpToOnboardingStep(step);
        var e = EmployeeManager.Instance?.ownedEmployees.Count > 0 ? EmployeeManager.Instance.ownedEmployees[0] : null;
        if (e != null) DevelopmentManager.Instance?.TestLeaderScore(e, type);
        else Set("보유 직원이 없음");
    }

    // 16-1 전용 테스트 — 다른 온보딩 단계/진행 상태(이전에 뭘 완료했는지, 완료된 프로젝트가 있는지 등)와
    // 전혀 무관하게 16-1 대사 + SalesUI 그래프만 강제로 띄운다. SalesUI는 더미 데이터로 직접 오픈하고,
    // 16-1 대사는 TutorialController.Instance.PlayTutorial16_1()을 우리 쪽 코루틴으로 직접 실행한다
    // (TriggerTutorial16_1()을 거치지 않으므로 Tutorial16_1Done 여부와 무관하게 항상 재생됨).
    // ⚠️ 실제 튜토리얼 진행 중(아직 16-1을 실제로 통과 안 한 세이브)일 때 이 테스트가 Done 플래그를
    // 영구히 찍어버리면 나중에 진짜 16-1이 와야 할 때 스킵되는 사고가 남 — 그래서 대사 재생 "중"에만
    // 임시로 Done을 켜(SalesUI 내부 자연 트리거와 중복 방지) 두고, 재생이 끝나면 원래 상태였던 게
    // false였을 경우 다시 false로 원복한다("실제로 완료됐다"는 표시는 나중에 진짜 완료될 때 남긴다).
    void QuickTest16_1(ProjectScale scale)
    {
        if (TutorialController.Instance == null) { Set("TutorialController 인스턴스 없음 (씬에 없거나 이미 파괴됨)"); return; }
        if (SalesUI.Instance == null) { Set("SalesUI 인스턴스 없음 (인게임에서 실행하세요)"); return; }

        StartCoroutine(RunQuickTest16_1(scale, qualityScore: 70f, forceRealFormula: false));
    }

    // 원본 16-1 테스트는 "튜토리얼 첫 판매" 고정값(매출 12,000G, 순위 1·5·11 고정)을 그대로 태워서
    // 매출이 낮고 순위 축이 실제 공식과 무관하게 눌려 보인다 — 순위-매출 이중축 차트를 높은 값대에서
    // 확인하고 싶을 때는 SalesUI.DebugForceRealFormula 로 튜토리얼 고정 분기를 잠깐 우회하고
    // qualityScore를 크게 줘서 1위대 매출을 강제로 만든다(소형 기준으로도 1위를 넘도록 8000 사용 —
    // 규모가 클수록 더 여유있게 넘음).
    void QuickTest16_1HighRevenue(ProjectScale scale)
    {
        if (TutorialController.Instance == null) { Set("TutorialController 인스턴스 없음 (씬에 없거나 이미 파괴됨)"); return; }
        if (SalesUI.Instance == null) { Set("SalesUI 인스턴스 없음 (인게임에서 실행하세요)"); return; }

        StartCoroutine(RunQuickTest16_1(scale, qualityScore: 8000f, forceRealFormula: true));
    }

    IEnumerator RunQuickTest16_1(ProjectScale scale, float qualityScore, bool forceRealFormula)
    {
        bool wasDone = OnboardingState.Tutorial16_1Done;
        OnboardingState.MarkTutorial16_1Done(); // 대사 재생 동안만 임시로 켬 — SalesUI 내부 자연 트리거 중복 방지

        bool prevForceRealFormula = SalesUI.DebugForceRealFormula;
        if (forceRealFormula) SalesUI.DebugForceRealFormula = true;

        SalesUI.Instance.ShowWithProjectName(
            qualityScore: qualityScore, scale: scale, projectName: $"[테스트] 16-1 ({scale})",
            cachedScale: scale, cachedGenre: ProjectGenre.RPG, cachedPlatform: ProjectPlatform.PC,
            planning: 50f, develop: 50f, art: 50f, creativity: 50f, bug: 0f);

        // ShowInternal 이 호출된 시점(=신규 분기 진입해 revenuePerPeriod 확정)까지만 필요 — 곧바로 원복해
        // 다른 SalesUI 세션(복원 등)에 새어나가지 않게 한다.
        SalesUI.DebugForceRealFormula = prevForceRealFormula;

        // 실제 개발완료 흐름(DevelopmentResultUI)과 동일하게, 마케팅→판매 직전 랜덤 2인 patrol도 그대로 재현.
        if (DevelopmentManager.Instance != null)
        {
            OfficeManager.Instance?.TriggerDevelopmentCompletePatrol(
                DevelopmentManager.Instance.developCompletePatrolPointA,
                DevelopmentManager.Instance.developCompletePatrolPointB);
        }

        Set("SalesUI 테스트 오픈 + 16-1 튜토리얼 대사 재생 중 + 개발완료 patrol 2인 재현...");
        yield return StartCoroutine(TutorialController.Instance.PlayTutorial16_1());

        if (!wasDone)
        {
            OnboardingState.ResetTutorial16_1();
            Set("16-1 테스트 종료 — 실제 진행 상황 보존을 위해 Done 플래그 원복함");
        }
        else
        {
            Set("16-1 테스트 종료");
        }
    }

    // 평론가UI(CriticReviewUI) 점수 직접 테스트 — CalcCriticScore 공식/랜덤 변동(-5~5)을 거치지 않고
    // 지정한 점수를 그대로 띄운다. 50점 경계로 low/high 반응 이미지·멘트풀 분기를 정확히 확인할 목적.
    void QuickTestCriticReview(int score)
    {
        if (CriticReviewUI.Instance == null) { Set("CriticReviewUI 인스턴스 없음 (인게임에서 실행하세요)"); return; }
        CriticReviewUI.Instance.ShowWithScore(score, () => Set($"평론가UI {score}점 테스트 종료"));
        Set($"평론가UI {score}점 테스트 오픈");
    }

    // 튜토리얼 사이클 구분 테스트용 — 1사이클(1~16)을 전부 완료 처리하고 곧바로 2사이클의 첫 단계인
    // 17-1을 재생한다. 16-1과 달리 여기서는 완료 처리를 원복하지 않는다 — "1사이클을 끝내고 2사이클로
    // 넘어간 상태"를 실제로 만드는 게 이 버튼의 목적이라 Done이 영구히 남는 게 맞다.
    void JumpToCycle2()
    {
        JumpToOnboardingStep(13); // 1~12단계 완료 처리
        OnboardingState.MarkTutorial13Done();
        OnboardingState.MarkTutorial13_4Done();
        OnboardingState.MarkTutorial13_5Done();
        OnboardingState.MarkTutorial14_1Done();
        OnboardingState.MarkTutorial15Done();
        OnboardingState.MarkTutorial16_1Done();
        // 이전 테스트에서 이미 17-x/18-x가 전부(또는 일부) 완료 처리됐을 수 있음 — 그러면 각 자연 트리거의
        // !TutorialXDone 체크가 안 걸려서 해당 단계가 다시 안 뜬다. 17-x 이후는 전부 매번 다시 볼 수
        // 있도록 여기서 통째로 원복해둔다(17_8_UNLOCK은 17-8/17-9가 실제로 해제하는 내부 신호라 함께 원복).
        OnboardingState.ResetTutorial17_1();
        OnboardingState.ResetTutorial17_2();
        OnboardingState.ResetTutorial17_7();
        OnboardingState.ResetTutorial17_8Unlock();
        OnboardingState.ResetTutorial18();
        OnboardingState.ResetTutorial19();
        OnboardingState.ResetTutorial20();

        // 20단계(2번째 프로젝트 기획팀장) 트리거는 completedProjects.Count==1 로 판별되는데(JumpToTutorial19
        // 주석 참고), 17-1부터 자연 진행해서 18→19→20까지 도달해도 실제 1번째 프로젝트를 완주한 적이
        // 없어 카운트가 0으로 남는다 — 서버 Insert 없이 메모리에만 더미 1건을 채워 조건을 맞춰준다.
        if (CompletedProjectManager.Instance != null && CompletedProjectManager.Instance.completedProjects.Count == 0)
        {
            CompletedProjectManager.Instance.completedProjects.Add(new CompletedProjectData
            {
                projectName = "(디버그) 1사이클 완료작",
                scale = 1, genre = 0, platform = 0,
                planning = 100, develop = 100, art = 100, creativity = 100, bug = 0,
                totalRevenue = 1,
                year = GameTimeManager.Instance != null ? GameTimeManager.Instance.Year : 1, month = 1, week = 1,
                qualityScore = 100, criticTotalScore = 100, bestRank = 1,
            });
        }

        if (TutorialController.Instance == null) { Set("TutorialController 인스턴스 없음 (씬에 없거나 이미 파괴됨)"); return; }

        // ForceResetAndStart — 다른 PlayTutorialX()가 이미 진행 중이었어도(예: 스킵 버튼을 연달아 누른 경우)
        // dim/시간정지/메뉴숨김을 전부 강제로 정리한 뒤에 17-1을 새로 시작해서, 스킵이 항상 확실하게 먹히게 한다.
        TutorialController.Instance.ForceResetAndStart(TutorialController.Instance.PlayTutorial17_1());
        Set("1사이클(1~16) 완료 처리 + 17-1 튜토리얼 대사 강제 재생 (2사이클 시작)");
    }

    // 19단계(마무리 핸드오프 대사)부터 바로 보고 싶을 때 — 1~18단계를 전부 완료 처리하고 19-1을 강제
    // 재생한다. 패널을 열어서 자연 트리거할 방법이 없어 JumpToCycle2와 동일하게 코루틴을 직접
    // 시작시켜야 한다. 19-1은 대사 후 메뉴→프로젝트→게임개발 강조가 이어지므로(17-1과 동일하게)
    // 실제로 그 3단계를 클릭해줘야 완료된다. 이전 테스트에서 19~21이 이미 완료 처리돼 있을 수 있어 먼저 원복.
    void JumpToTutorial19()
    {
        JumpToOnboardingStep(13); // 1~12단계 완료 처리
        OnboardingState.MarkTutorial13Done();
        OnboardingState.MarkTutorial13_4Done();
        OnboardingState.MarkTutorial13_5Done();
        OnboardingState.MarkTutorial14_1Done();
        OnboardingState.MarkTutorial15Done();
        OnboardingState.MarkTutorial16_1Done();
        OnboardingState.MarkTutorial17_1Done();
        OnboardingState.MarkTutorial17_2Done();
        OnboardingState.MarkTutorial17_7Done();
        OnboardingState.MarkTutorial18Done();
        OnboardingState.ResetTutorial19();
        OnboardingState.ResetTutorial20();
        OnboardingState.ResetTutorial21();
        OnboardingState.ResetTutorial22();
        OnboardingState.ResetTutorial23();
        OnboardingState.ResetTutorial24();

        // 20단계(2번째 프로젝트 기획팀장) 트리거는 DevelopmentManager가
        // CompletedProjectManager.Instance.completedProjects.Count==1 인지로 "1사이클 완료 후 2번째
        // 프로젝트"를 판별한다(DevelopmentManager.cs BuildAndShowLeaderScore 계열 tutorialFixedRollsCycle2).
        // 디버그 점프는 실제 1번째 프로젝트를 완주하지 않으므로 이 카운트가 계속 0으로 남아 20단계가
        // 영영 안 뜨는 문제가 있었다 — 서버에 실제 Insert하는 SaveCompletedProject 대신 메모리 리스트에만
        // 더미 1건을 채워 카운트 조건만 맞춰준다.
        if (CompletedProjectManager.Instance != null && CompletedProjectManager.Instance.completedProjects.Count == 0)
        {
            CompletedProjectManager.Instance.completedProjects.Add(new CompletedProjectData
            {
                projectName = "(디버그) 1사이클 완료작",
                scale = 1, genre = 0, platform = 0,
                planning = 100, develop = 100, art = 100, creativity = 100, bug = 0,
                totalRevenue = 1,
                year = GameTimeManager.Instance != null ? GameTimeManager.Instance.Year : 1, month = 1, week = 1,
                qualityScore = 100, criticTotalScore = 100, bestRank = 1,
            });
        }

        if (TutorialController.Instance == null) { Set("TutorialController 인스턴스 없음 (씬에 없거나 이미 파괴됨)"); return; }

        // ForceResetAndStart — 17-1 스킵 도중 곧바로 19부터를 눌러도(또는 그 반대) 이전 코루틴이 걸어둔
        // dim/시간정지/메뉴숨김이 고아로 남지 않고 확실히 정리된 뒤 19-1이 새로 시작된다.
        TutorialController.Instance.ForceResetAndStart(TutorialController.Instance.PlayTutorial19());
        Set("1~18단계 완료 처리 + 19-1 튜토리얼 대사 강제 재생");
    }

    // ── 채용 ──
    void HireUnique(string masterId)
    {
        var em = EmployeeManager.Instance;
        var pool = em.poolEmployees.Find(e => e.id == masterId);
        if (pool == null) { Set($"마스터 풀에 '{masterId}' 없음 (차트 로드 확인)"); return; }

        var clone = pool.Clone(); // ranges + portraitId + epicTraitId + uniqueEventType 복사, id=masterId 유지
        clone.grade           = EmployeeGrade.Unique;
        clone.potential       = EmployeePotential.S;
        clone.developSkill    = pool.developMax;
        clone.planningSkill   = pool.planningMax;
        clone.artSkill        = pool.artMax;
        clone.creativitySkill = pool.creativityMax;
        clone.salary          = pool.salaryMax;
        clone.enhancementLevel = 0;
        em.HireEmployee(clone); // 비동기 Insert → 잠시 후 ownedEmployees 에 추가됨
        Set($"채용 요청: {clone.employeeName} (Unique). 잠시 후 목록에 표시됨");
    }

    // ── 이벤트 발동 ──
    EmployeeData FindByEvent(string eventType)
    {
        var em = EmployeeManager.Instance;
        foreach (var e in em.ownedEmployees)
            if (!e.isCEO && e.grade >= EmployeeGrade.Unique && CharacterTraitApplier.ResolveEventType(e) == eventType)
                return e;
        return null;
    }

    void ForceEvent(string eventType)
    {
        var emp = FindByEvent(eventType);
        if (emp == null) { Set($"{eventType} 보유 유니크 직원 없음 — 먼저 채용"); return; }
        emp.lastUniqueEventYear = -1;        // 연 1회 가드 우회(반복 테스트)
        CharacterUniqueEvents.Trigger(emp);
        Set($"{eventType} 발동: {emp.employeeName}");
    }

    // 4개 스탯(기획/개발/아트/창의성) 버프/디버프 반영 과정을 항목별로 콘솔에 찍는다 — "왜 한 스탯만 색이 안 바뀌지" 디버깅용.
    void LogStatBreakdown(EmployeeData e)
    {
        float satMult    = e.GetSatisfactionMultiplier();
        float debuffPct  = e.GetStatDebuffPercent();
        float buffPct    = e.GetStatBuffPercent();
        float romancePct = e.GetRomanceBuffPercent();
        float godPct     = e.GetGodBlessingBuffPercent();
        float otakuPct   = e.GetOtakuBuffPercent();
        float buffDebuffPct = e.GetTotalStatBuffDebuffPercent();
        float cosmicMul     = e.GetCosmicMultiplier();
        float grandTotalPct = e.GetTotalStatPercent();

        // 콘솔 리더가 멀티라인 로그를 첫 줄만 보여주는 경우가 있어 한 줄씩 따로 찍는다.
        Debug.Log($"[StatBreakdown] {e.employeeName} (role={e.role}, satisfaction={e.satisfaction})");
        Debug.Log($"[StatBreakdown] satMult={satMult:0.00}  debuff={debuffPct}%  buff=+{buffPct}%  romance=+{romancePct}%  godBlessing=+{godPct}%  otaku=+{otakuPct}%  buffDebuffSubtotal={buffDebuffPct}%  cosmicMult={cosmicMul:0.00}  grandTotal={grandTotalPct}%");
        Debug.Log($"[StatBreakdown] 기획: raw={e.planningSkill} -> effective={e.EffectivePlanningSkill} (diff={e.EffectivePlanningSkill - e.planningSkill})");
        Debug.Log($"[StatBreakdown] 개발: raw={e.developSkill} -> effective={e.EffectiveDevelopSkill} (diff={e.EffectiveDevelopSkill - e.developSkill})");
        Debug.Log($"[StatBreakdown] 아트: raw={e.artSkill} -> effective={e.EffectiveArtSkill} (diff={e.EffectiveArtSkill - e.artSkill})");
        Debug.Log($"[StatBreakdown] 창의: raw={e.creativitySkill} -> effective={e.EffectiveCreativitySkill} (diff={e.EffectiveCreativitySkill - e.creativitySkill})");
        Debug.Log($"[StatBreakdown] statBuffStacks={e.statBuffStacks.Count} statDebuffStacks={e.statDebuffStacks.Count} romanceWeeksLeft={e.romanceBuffWeeksLeft} godBlessingPercent={e.godBlessingStatPercent} otakuFixedGenre={e.otakuFixedGenre} selectedGenre={ProjectSetupUI.SelectedGenre}");
        Set($"{e.employeeName} 스탯 breakdown 콘솔에 출력함");
    }

    void ForceKim()
    {
        var emp = FindByEvent("KimUnique");
        if (emp == null) { Set("김아무개(KimUnique) 없음 — 먼저 채용"); return; }
        emp.satisfaction = 50;               // 회복 조건(80↓) 충족
        emp.lastUniqueEventYear = -1;
        CharacterUniqueEvents.Trigger(emp);  // 만족도 100 회복
        Set($"유리멘탈 회복: {emp.employeeName} (만족도 50→100)");
    }

    void ForceGodBlessing(int dice)
    {
        var emp = FindByEvent("UgiUnique");
        if (emp == null) { Set("우기(UgiUnique) 없음 — 먼저 채용"); return; }
        CharacterUniqueEvents.DebugForcedDice = dice; // 0=랜덤, 1~6=강제
        emp.lastUniqueEventYear = -1;
        CharacterUniqueEvents.Trigger(emp);
        Set($"신의 축복 발동 (주사위 {(dice == 0 ? "랜덤" : dice.ToString())}): {emp.employeeName}");
    }

    void AllSatisfaction(int amount)
    {
        var em = EmployeeManager.Instance;
        foreach (var e in em.ownedEmployees) if (!e.isCEO) e.ChangeSatisfaction(amount);
        Set($"전 직원 만족도 {(amount >= 0 ? "+" : "")}{amount}");
    }

    void FireUgi()
    {
        var emp = FindByEvent("UgiUnique");
        if (emp == null) { Set("우기 없음"); return; }
        EmployeeManager.Instance.FireEmployee(emp, countAsExit: false);
        Set($"우기 해고: {emp.employeeName} → 신의축복 지속효과 정리됨 (HasUniqueUgi={CharacterUniqueEvents.HasUniqueUgi()})");
    }

    // ── 엘리베이터 테스트 (반대편 셀로 patrol 이동 — (3,-6,0)을 "지나가야" 워프가 발동함) ──
    GameObject _elevatorTestPoint;

    void SendToElevatorEntry()
    {
        if (GridManager.Instance == null) { Set("GridManager 없음 (인게임에서 실행하세요)"); return; }

        // 목적지를 링크 건너편(11,2,0)으로 잡아야 경로가 (3,-6,0)을 "통과"하면서 워프가 발동한다.
        // 목적지를 (3,-6,0) 자체로 잡으면 거기 도착하는 순간 경로가 끝나 워프가 시도조차 안 됨.
        Vector3Int cell = new Vector3Int(11, 2, 0);
        Vector3 world = GridManager.Instance.CellToWorld(cell);

        if (_elevatorTestPoint == null)
        {
            _elevatorTestPoint = new GameObject("[Test]ElevatorExitPoint");
            _elevatorTestPoint.AddComponent<PatrolPoint>().pointId = "elevator_test_exit";
        }
        _elevatorTestPoint.transform.position = world;
        OfficeManager.Instance?.RefreshPatrolPoints(); // 새로 만든 포인트를 캐시에 반영

        var em = EmployeeManager.Instance;
        EmployeeData target = null;
        foreach (var e in em.ownedEmployees)
        {
            if (e.isCEO) continue;
            target = e;
            break;
        }
        if (target == null) { Set("보낼 수 있는 직원 없음"); return; }

        OfficeManager.Instance?.ForceCharacterToPatrolPoint(target.id, "elevator_test_exit", 9999f);
        Set($"{target.employeeName} → 셀 (11,2,0) [world {world}] 이동 시작 (중간에 (3,-6,0) 경유 워프 예상)");
    }

    // ── 강화 (테스트: 확률 무시하고 강제 성공으로 목표 레벨까지) ──
    void EnhanceAll(int target)
    {
        var em = EmployeeManager.Instance;
        int count = 0;
        foreach (var e in em.ownedEmployees)
        {
            if (e.isCEO) continue;
            int goal = Mathf.Min(target, EmployeeEnhancement.GetMaxLevel(e.grade));
            while (e.enhancementLevel < goal)
            {
                e.enhancementLevel++;
                em.ApplyEnhancement(e); // EnhanceOnce 성공 경로와 동일 — 주스탯/부스탯/연봉 반영
            }
            em.UpdateEmployee(e);
            count++;
        }
        em.SaveAllEmployees();
        HUDUI.Instance?.RefreshAll();
        Set($"{count}명 강화 완료 (목표 +{target}강, 등급별 최대치 제한 적용)");
    }
}
