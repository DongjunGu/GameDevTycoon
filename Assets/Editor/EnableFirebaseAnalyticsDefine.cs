using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// 일회성 유틸 — GameAnalytics 의 Firebase 전송 경로를 켜는 정의 심볼을 추가한다.
// 심볼이 들어가고 나면 이 파일은 지워도 된다.
public static class EnableFirebaseAnalyticsDefine
{
    const string SYMBOL = "FIREBASE_ANALYTICS";

    [MenuItem("Tools/Analytics/Enable FIREBASE_ANALYTICS")]
    public static void Enable()
    {
        var targets = new[]
        {
            NamedBuildTarget.Android,
            NamedBuildTarget.iOS,
            NamedBuildTarget.Standalone,
        };

        foreach (var t in targets)
        {
            string current = PlayerSettings.GetScriptingDefineSymbols(t);
            if (current.Split(';').Select(s => s.Trim()).Contains(SYMBOL))
            {
                Debug.Log($"[Define] {t.TargetName}: 이미 있음");
                continue;
            }

            string next = string.IsNullOrEmpty(current) ? SYMBOL : current + ";" + SYMBOL;
            PlayerSettings.SetScriptingDefineSymbols(t, next);
            Debug.Log($"[Define] {t.TargetName} → {next}");
        }

        AssetDatabase.SaveAssets();
    }

    // 심볼이 실제로 "지금 컴파일에" 먹었는지 확인용 — 에디터 스크립트는 활성 빌드 타겟의 심볼로 컴파일된다.
    [MenuItem("Tools/Analytics/Check FIREBASE_ANALYTICS")]
    public static void Check()
    {
#if FIREBASE_ANALYTICS
        // Firebase 어셈블리까지 실제로 해석되는지 같이 검증한다.
        string t = typeof(Firebase.Analytics.FirebaseAnalytics).FullName;
        Debug.Log($"[Define] 활성 / target={EditorUserBuildSettings.activeBuildTarget} / {t}");
#else
        Debug.LogWarning($"[Define] 비활성 / target={EditorUserBuildSettings.activeBuildTarget}");
#endif
    }
}
