using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using BackEnd;

// 유저 플레이 중 발생하는 에러를 자동으로 뒤끝 GameLog로 전송 — 코드 전체에 try-catch를 깔 필요 없이
// Application.logMessageReceived 하나로 Debug.LogError/LogException/Assert(잡히지 않은 예외 포함)를
// 전역으로 수집한다. 뒤끝 콘솔 "뒤끝베이스 > 로그 관리"에서 logType="ClientError"로 조회.
// BackendManager.Start()에서 인스턴스 생성, LoadAllAndEnterGame() 진입 시 ReadyToSend=true로 전송 시작
// (로그인 완료 전 전송 시도 방지).
public class ErrorReporter : MonoBehaviour
{
    public static ErrorReporter Instance { get; private set; }

    public static bool ReadyToSend = false;

    // 세션당 같은 메시지는 최대 이만큼만 전송 — 같은 에러가 매 프레임(Update 등)에서 반복돼도 폭탄전송 방지.
    const int MaxSendsPerMessage = 2;
    readonly Dictionary<string, int> _sentCounts = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Application.logMessageReceived += OnLogMessageReceived;
    }

    void OnDestroy()
    {
        Application.logMessageReceived -= OnLogMessageReceived;
    }

    void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (!ReadyToSend) return;
        if (string.IsNullOrEmpty(condition)) return;

        int count = _sentCounts.TryGetValue(condition, out var c) ? c : 0;
        if (count >= MaxSendsPerMessage) return;
        _sentCounts[condition] = count + 1;

        var param = new Param();
        param.Add("logType", type.ToString());
        param.Add("message", Truncate(condition, 500));
        param.Add("stackTrace", Truncate(stackTrace, 1500));
        param.Add("scene", SceneManager.GetActiveScene().name);
        param.Add("platform", Application.platform.ToString());
        param.Add("appVersion", Application.version);

        // ⚠️ 이 콜백 안에서는 Debug.Log 계열을 쓰지 않는다 — 또 다른 로그를 유발해 재귀될 수 있음.
        // InsertLogV2 비동기 버전 사용 — 메인 스레드 블로킹 없이 전송(동기 버전은 네트워크 응답까지 대기).
        try
        {
            Backend.GameLog.InsertLogV2("ClientError", param, bro => { });
        }
        catch
        {
            // 로그 전송 자체의 실패는 게임 진행에 영향 주면 안 되므로 조용히 무시.
        }
    }

    static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= max ? s : s.Substring(0, max);
    }
}
