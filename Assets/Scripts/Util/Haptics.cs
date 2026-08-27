using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

// 가벼운 햅틱(약한 진동) 유틸. 실기기 전용 — 에디터/데스크톱은 무동작.
//  - Android: 시스템 Vibrator 로 아주 짧고 약한 one-shot (API 26+ 는 진폭 지정, 그 이하는 짧은 vibrate)
//  - iOS: UIImpactFeedbackGenerator(.light) — Assets/Plugins/iOS/Haptics.mm
// 연속 호출(예: 보너스 점수 아이콘이 우르르 흡수될 때) 대비 짧은 디바운스 포함.
public static class Haptics
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void _Haptic_LightImpact();
#endif

    // 너무 촘촘하면 "지지직" 연속음처럼 느껴지므로 최소 간격을 둔다.
    const float MinIntervalSec = 0.02f;
    static float _lastTime = -999f;

#if UNITY_ANDROID && !UNITY_EDITOR
    static AndroidJavaObject _vibrator;
    static int _sdkInt = -1;
    static bool _initTried;

    static void EnsureAndroid()
    {
        if (_initTried) return;
        _initTried = true;
        try
        {
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                _sdkInt = version.GetStatic<int>("SDK_INT");
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
        }
        catch (System.Exception e) { Debug.LogWarning("[Haptics] Android init 실패: " + e.Message); }
    }
#endif

    // 아이콘 1개 흡수 = 이걸 1회. 약하고 아주 짧게.
    public static void LightTick()
    {
        float now = Time.unscaledTime;
        if (now - _lastTime < MinIntervalSec) return;
        _lastTime = now;

#if UNITY_IOS && !UNITY_EDITOR
        try { _Haptic_LightImpact(); } catch { }
#elif UNITY_ANDROID && !UNITY_EDITOR
        EnsureAndroid();
        if (_vibrator == null) return;
        try
        {
            if (_sdkInt >= 26)
            {
                // createOneShot(ms, amplitude) — amplitude 1~255. 40 정도면 톡 하는 약한 느낌.
                using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", 18L, 40))
                    _vibrator.Call("vibrate", effect);
            }
            else
            {
                _vibrator.Call("vibrate", 12L); // 구버전 — 진폭 지정 불가, 최대한 짧게
            }
        }
        catch { }
#else
        // 에디터/데스크톱 — 무동작
#endif
    }
}
