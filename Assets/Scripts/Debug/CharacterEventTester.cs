using System.Collections.Generic;
using UnityEngine;

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

    void Update()
    {
        // New Input System (activeInputHandler=1) — UnityEngine.Input 은 예외 발생하므로 Keyboard.current 사용.
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.f9Key.wasPressedThisFrame) _open = !_open;
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
        GUILayout.Label("<b>━━ 유틸 ━━</b>", Rich());
        if (GUILayout.Button("전 직원 만족도 -30 (유리멘탈 조건/효과 확인)")) AllSatisfaction(-30);
        if (GUILayout.Button("전 직원 만족도 +30")) AllSatisfaction(+30);
        if (GUILayout.Button("우기 해고 (신의축복 지속효과 즉시중단 확인)")) FireUgi();
        if (GUILayout.Button($"매출축복 활성? {CharacterUniqueEvents.GetGodBlessingSalesBonus():0.00}")) { }
        GUILayout.Label($"우기 보유: {(CharacterUniqueEvents.HasUniqueUgi() ? "O" : "X")}");

        GUILayout.Space(8);
        GUILayout.Label("<b>━━ 모달 차단 테스트 ━━</b>", Rich());
        if (GUILayout.Button("AlertUI 띄우기 (뒤 클릭 차단 확인)")) AlertUI.Instance?.Show("테스트 알림 — 이 뒤의 버튼/메뉴가 안 눌려야 정상");
        if (GUILayout.Button("ConfirmUI 띄우기")) ConfirmUI.Instance?.Show("테스트 확인", () => Set("확인됨"), () => Set("취소됨"));
        if (GUILayout.Button("LoanUI 띄우기 (MenuCanvas 크로스 차단)")) LoanUI.Instance?.Open();

        GUILayout.Space(8);
        GUILayout.Label("<b>━━ 온보딩 ━━</b>", Rich());
        GUILayout.Label($"IntroDone: {OnboardingState.IntroDone} / TutorialDone: {OnboardingState.TutorialDone}");
        if (GUILayout.Button("온보딩 리셋 (컷씬/튜토리얼 재노출)")) { OnboardingState.ResetAll(); Set("온보딩 리셋 — LoadingScene부터 다시 실행"); }

        GUILayout.Space(6);
        GUILayout.Label("<b>상태:</b> " + _status, Rich());

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    GUIStyle Rich() { var s = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true }; return s; }
    void Set(string msg) { _status = msg; Debug.Log("[EventTester] " + msg); }

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
}
