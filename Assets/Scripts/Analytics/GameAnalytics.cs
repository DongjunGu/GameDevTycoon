using System.Collections.Generic;
using UnityEngine;
#if FIREBASE_ANALYTICS
using Firebase;
using Firebase.Analytics;
#endif

// 게임 → 분석 도구로 나가는 유일한 창구.
//
// Firebase SDK 유무와 무관하게 컴파일된다. Firebase 를 임포트한 뒤 Player Settings 의
// Scripting Define Symbols 에 FIREBASE_ANALYTICS 를 추가하면 실제 전송이 켜지고,
// 그 전까지는 콘솔 로그만 남는다(배선을 미리 다 해두고 SDK 는 나중에 붙일 수 있게).
//
// 사용:
//   GameAnalytics.TutorialStep(key);                 // OnboardingState.MarkDone 이 자동 호출
//   GameAnalytics.LogEvent("game_released", ...);    // 그 외 진척도 이벤트
public static class GameAnalytics
{
    // GA4 이벤트/파라미터 이름 규칙: 소문자 snake_case, 이벤트 40자·파라미터 40자·문자열 값 100자 이내.
    public const string EV_TUTORIAL_BEGIN = "tutorial_begin";     // GA4 권장 이벤트명
    public const string EV_TUTORIAL_STEP = "tutorial_step";
    public const string EV_TUTORIAL_COMPLETE = "tutorial_complete"; // GA4 권장 이벤트명

    // 사용자 속성 — 이름 24자, 값 36자 제한.
    // "이 유저가 튜토리얼 어디까지 갔나"를 유저 단위로 보려면 이벤트가 아니라 사용자 속성이어야 한다.
    // (이벤트는 발생 시점 기록이라, 이탈한 유저의 "최종 도달 지점"으로 세그먼트를 만들 수 없다)
    public const string UP_TUTORIAL_PROGRESS = "tutorial_progress";
    public const string UP_TUTORIAL_INDEX = "tutorial_index";

    // ── 마일스톤 ────────────────────────────────────────────────────
    // GA4 유입경로(퍼널) 탐색은 최대 10단계라 33개 세부 단계를 그대로 못 넣는다.
    // 그래서 "여기서 막히면 이탈한다" 싶은 관문 10개만 따로 굵직하게 쏜다.
    // 세부 이탈 지점은 tutorial_progress(33단계)로, 구간별 통과율은 이 이벤트로 본다.
    public const string EV_TUTORIAL_MILESTONE = "tutorial_milestone";
    public const string UP_TUTORIAL_MILESTONE = "tutorial_milestone";

    // 이름 앞의 번호는 GA4 표에서 알파벳 정렬이 곧 진행 순서가 되도록 붙인 것.
    // 키는 OnboardingState.StepIdFromKey() 결과(= "tutorial13_4" 형태).
    static readonly Dictionary<string, string> Milestones = new Dictionary<string, string>
    {
        { "intro",        "01_intro" },          // 컷씬 + 첫 런 진입
        { "tutorial3",    "02_hire" },           // 첫 채용 확정
        { "tutorial5",    "03_project_start" },  // 첫 프로젝트 개발 시작
        { "tutorial7",    "04_leader_score" },   // 기획팀장 점수(고스탑) 첫 완주
        { "tutorial13_4", "05_creativity" },     // 창의성 미니게임
        { "tutorial14_1", "06_game_complete" },  // 첫 게임 완성
        { "tutorial16_1", "07_first_sales" },    // 첫 매출 발생
        { "tutorial17_7", "08_shop_item" },      // 상점 구매 + 아이템 사용
        { "tutorial19",   "09_handoff" },        // "이제 혼자 해보라" — 튜토리얼 본편 종료
        { "tutorial24",   "10_complete" },       // 파산 엔딩까지 완주
    };

    public static bool IsReady { get; private set; }

    // 초기화 전에 발생한 이벤트를 담아뒀다가 준비되면 흘려보낸다.
    // (앱 시작 직후 컷씬 단계에서 이미 이벤트가 발생하는데, Firebase 초기화는 비동기라 늦다)
    struct Pending
    {
        public string name;
        public Dictionary<string, object> parameters;
    }
    static readonly List<Pending> _queue = new List<Pending>();
    const int QUEUE_MAX = 64;

    // 온보딩 진입 시점 — LogoScenario 가 컷씬을 재생하기 "직전"에 호출한다.
    //
    // ⚠️ 첫 단계 완료(intro)로 이걸 대신하면 안 된다. intro 플래그는 "컷씬을 끝까지 본" 시점이라
    // 컷씬 도중 이탈한 유저가 통계에서 통째로 사라진다 — 초반 이탈이 가장 몰리는 구간인데
    // 그 구간이 측정 사각지대가 되어버린다. 그래서 진입과 완료를 분리해서 쏜다.
    //
    // 온보딩이 끝나기 전엔 앱을 켤 때마다 이 분기를 타므로 이벤트가 여러 번 나갈 수 있다.
    // 비율은 GA4 '총 사용자'(중복 제거) 기준으로 보면 정확하고, 이벤트 수는 재시도 횟수로 읽으면 된다.
    public static void TutorialBegin()
    {
        LogEvent(EV_TUTORIAL_BEGIN);
        SetUserProperty(UP_TUTORIAL_MILESTONE, "00_begin");
    }

    // ── 튜토리얼 진척도 ─────────────────────────────────────────────
    // OnboardingState.MarkDone() 이 단계 최초 완료 시 1회 호출한다.
    public static void TutorialStep(string onboardingKey)
    {
        string stepId = OnboardingState.StepIdFromKey(onboardingKey);
        int index = OnboardingState.StepIndexOf(onboardingKey);

        // 순서 배열에 없는 키(보조 플래그 등)는 퍼널을 오염시키므로 보내지 않는다.
        if (index < 0)
        {
            Debug.Log($"[Analytics] 순서 미등록 단계 무시: {stepId}");
            return;
        }

        LogEvent(EV_TUTORIAL_STEP,
            "step_id", stepId,
            "step_index", index,
            "step_total", OnboardingState.TotalStepCount);

        // 최종 도달 지점 갱신 — 유저 단위 세그먼트/이탈 분석의 핵심.
        SetUserProperty(UP_TUTORIAL_PROGRESS, stepId);
        SetUserProperty(UP_TUTORIAL_INDEX, index.ToString());

        // 관문 단계면 굵직한 이벤트를 하나 더 — 퍼널 보고서용.
        if (Milestones.TryGetValue(stepId, out string milestone))
        {
            LogEvent(EV_TUTORIAL_MILESTONE,
                "milestone", milestone,
                "step_id", stepId);
            SetUserProperty(UP_TUTORIAL_MILESTONE, milestone);
        }

        if (index == OnboardingState.TotalStepCount - 1)
            LogEvent(EV_TUTORIAL_COMPLETE);
    }

    // 앱 시작 시 1회 — 이번 세션에 새 단계를 안 밟아도 "어디까지 간 유저"인지 항상 최신으로 유지한다.
    static void SyncTutorialProgressProperty()
    {
        int index = OnboardingState.FurthestDoneIndex();
        if (index < 0)
        {
            SetUserProperty(UP_TUTORIAL_PROGRESS, "none");
            SetUserProperty(UP_TUTORIAL_INDEX, "-1");
            SetUserProperty(UP_TUTORIAL_MILESTONE, "00_none");
            return;
        }

        SetUserProperty(UP_TUTORIAL_PROGRESS, OnboardingState.StepIdFromKey(OnboardingState.OrderedStepKeys[index]));
        SetUserProperty(UP_TUTORIAL_INDEX, index.ToString());

        // 도달한 단계 이하에서 가장 마지막으로 통과한 관문.
        string milestone = "00_none";
        for (int i = 0; i <= index; i++)
        {
            string id = OnboardingState.StepIdFromKey(OnboardingState.OrderedStepKeys[i]);
            if (Milestones.TryGetValue(id, out string m)) milestone = m;
        }
        SetUserProperty(UP_TUTORIAL_MILESTONE, milestone);
    }

    // ── 범용 API ────────────────────────────────────────────────────
    // 파라미터는 (이름, 값) 쌍을 번갈아 넘긴다:
    //   GameAnalytics.LogEvent("game_released", "genre", "RPG", "score", 87);
    public static void LogEvent(string eventName, params object[] keyValuePairs)
    {
        var dict = ToDictionary(eventName, keyValuePairs);

        if (!IsReady)
        {
            if (_queue.Count < QUEUE_MAX)
                _queue.Add(new Pending { name = eventName, parameters = dict });
            return;
        }

        Send(eventName, dict);
    }

    public static void SetUserProperty(string name, string value)
    {
#if FIREBASE_ANALYTICS
        if (!IsReady) { _pendingUserProps[name] = value; return; }
        Echo("user_property", new Dictionary<string, object> { { name, value } });
        FirebaseAnalytics.SetUserProperty(name, value);
#else
        Debug.Log($"[Analytics] (미전송) 사용자속성 {name}={value}");
#endif
    }

    // 전송 내용을 콘솔에도 남긴다 — 에디터/데스크톱의 Firebase Analytics 는 스텁이라
    // 실제로 보내지지 않고 아무 로그도 안 남는다. 실기기 DebugView 없이 배선을 확인하려면 이게 필요하다.
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    static void Echo(string eventName, Dictionary<string, object> dict)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("[Analytics] ").Append(eventName);
        if (dict != null)
            foreach (var kv in dict) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
        Debug.Log(sb.ToString());
    }

    static Dictionary<string, object> ToDictionary(string eventName, object[] kv)
    {
        var dict = new Dictionary<string, object>();
        if (kv == null) return dict;

        if (kv.Length % 2 != 0)
        {
            Debug.LogWarning($"[Analytics] {eventName}: 파라미터가 (이름,값) 쌍이 아님 — 무시");
            return dict;
        }

        for (int i = 0; i < kv.Length; i += 2)
        {
            string key = kv[i] as string;
            if (string.IsNullOrEmpty(key)) continue;
            dict[key] = kv[i + 1];
        }
        return dict;
    }

#if FIREBASE_ANALYTICS
    static readonly Dictionary<string, string> _pendingUserProps = new Dictionary<string, string>();

    static void Send(string eventName, Dictionary<string, object> dict)
    {
        Echo(eventName, dict);

        if (dict.Count == 0)
        {
            FirebaseAnalytics.LogEvent(eventName);
            return;
        }

        var list = new List<Parameter>(dict.Count);
        foreach (var kv in dict)
        {
            switch (kv.Value)
            {
                case null: break;
                case int i: list.Add(new Parameter(kv.Key, i)); break;
                case long l: list.Add(new Parameter(kv.Key, l)); break;
                case float f: list.Add(new Parameter(kv.Key, f)); break;
                case double d: list.Add(new Parameter(kv.Key, d)); break;
                case bool b: list.Add(new Parameter(kv.Key, b ? 1 : 0)); break;
                default: list.Add(new Parameter(kv.Key, kv.Value.ToString())); break;
            }
        }
        FirebaseAnalytics.LogEvent(eventName, list.ToArray());
    }

    // 자동 초기화 — 씬 배치 불필요. 뒤끝 초기화와 순서를 엮지 않는다(서로 독립).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoInit()
    {
        var go = new GameObject("FirebaseAnalytics");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<AnalyticsRunner>();
    }

    // 의존성 확인 콜백이 메인 스레드가 아닐 수 있어서, 플래그만 세우고 실제 처리는 Update 에서 한다.
    class AnalyticsRunner : MonoBehaviour
    {
        bool _depsOk;
        bool _depsFailed;

        void Awake()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
            {
                if (task.IsFaulted || task.Result != DependencyStatus.Available)
                    _depsFailed = true;
                else
                    _depsOk = true;
            });
        }

        void Update()
        {
            if (_depsFailed)
            {
                _depsFailed = false;
                Debug.LogWarning("[Analytics] Firebase 의존성 실패 — 이번 세션 전송 비활성");
                _queue.Clear();
                enabled = false;
                return;
            }

            if (!_depsOk || IsReady) return;

            FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
            IsReady = true;
            Debug.Log("[Analytics] Firebase Analytics 준비 완료");

            foreach (var kv in _pendingUserProps)
                FirebaseAnalytics.SetUserProperty(kv.Key, kv.Value);
            _pendingUserProps.Clear();

            SyncTutorialProgressProperty();

            foreach (var p in _queue) Send(p.name, p.parameters);
            _queue.Clear();

            enabled = false;
        }
    }
#else
    // SDK 미도입 상태 — 전송 없이 콘솔로만 확인한다. 배선/이벤트 설계를 먼저 검증하는 용도.
    static void Send(string eventName, Dictionary<string, object> dict)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("[Analytics] (미전송) ").Append(eventName);
        foreach (var kv in dict) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
        Debug.Log(sb.ToString());
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoInit()
    {
        IsReady = true; // 큐에 쌓지 않고 바로 콘솔로 흘린다
        SyncTutorialProgressProperty();
    }
#endif
}
