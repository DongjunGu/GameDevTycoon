using System;
using System.Collections.Generic;
using UnityEngine;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;

// AdMob 전역 매니저 — 보상형(Rewarded) 광고 담당.
// 흐름: UMP 동의 수집 → MobileAds.Initialize → preload 위치 미리 로드 → Show → 닫히면 그 위치만 재로드.
// 씬 배치 불필요 — RuntimeInitializeOnLoadMethod 로 스스로 생성하고 DontDestroyOnLoad 로 유지한다.
//
// 위치(AdPlacement)별로 광고 단위와 로드 상태를 따로 들고 있다. RewardedAd 인스턴스는 1회용이라
// 한 번 보여주면 폐기하고 새로 로드해야 하고, 재시도 백오프도 위치마다 독립적으로 돌아야 한다.
//
// 사용:
//   AdsManager.Instance.ShowRewarded(AdPlacement.DevResultDouble,
//       onRewarded:    () => { 보상지급(); },
//       onUnavailable: () => { AlertUI 로 "광고 준비중" 안내; });
//
//   // 드물게 쓰는 위치는 띄우기 전에 미리 요청해두면 성공률이 올라간다
//   AdsManager.Instance.Prepare(AdPlacement.BankruptRevive);
public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance { get; private set; }

    // 실제 단위로 실기기 테스트해야 할 때만 자기 기기 ID 를 넣는다.
    // ID 는 첫 광고 요청 시 logcat 에 "Use RequestConfiguration.Builder.setTestDeviceIds" 로 찍힌다.
    static readonly List<string> TestDeviceIds = new List<string>();

    public static bool IsInitialized { get; private set; }

    // 위치 하나당 광고 인스턴스 + 로드 상태 한 벌
    class Slot
    {
        public AdPlacement placement;
        public RewardedAd ad;
        public bool loading;
        public int retryCount;
        public float nextRetryTime = -1f;

        public bool IsReady => ad != null && ad.CanShowAd();
    }

    readonly Dictionary<AdPlacement, Slot> _slots = new Dictionary<AdPlacement, Slot>();

    // 전면 광고는 동시에 하나만 뜬다 — 지금 표시 중인 위치
    Slot _showing;
    Action _pendingReward;
    Action _pendingUnavailable;

    // 광고가 떠 있는 동안 게임 시간을 멈췄는지 — 중복 StartTime 방지용
    bool _timeStoppedByAd;

    // 동의 워치독
    bool _watchdogFired;
    float _consentDeadline;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("AdsManager");
        go.AddComponent<AdsManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 콜백을 유니티 메인 스레드로 강제 — 안 켜면 광고 콜백 안에서 UI/매니저 접근 시 터진다.
        MobileAds.RaiseAdEventsOnUnityMainThread = true;

        _consentDeadline = Time.realtimeSinceStartup + 10f;
        RequestConsent();
    }

    void Update()
    {
        // 동의 콜백이 끝내 안 오는 경우(플랫폼별 구현 차이)를 대비한 1회성 워치독.
        if (!IsInitialized && !_watchdogFired && Time.realtimeSinceStartup >= _consentDeadline)
        {
            _watchdogFired = true;
            Debug.LogWarning("[Ads] 동의 응답 지연 — 초기화 강제 시도");
            InitializeAds();
        }

        if (!IsInitialized) return;

        // 위치별 재시도 (지수 백오프). 코루틴 대신 타임스탬프 — 시간정지/씬전환에 영향 안 받게.
        foreach (var slot in _slots.Values)
        {
            if (slot.nextRetryTime > 0f && Time.realtimeSinceStartup >= slot.nextRetryTime)
            {
                slot.nextRetryTime = -1f;
                Load(slot);
            }
        }
    }

    // ── 1) UMP 동의 ─────────────────────────────────────────────────
    // EEA/영국/스위스 사용자는 동의를 받기 전에 광고를 요청하면 정책 위반.
    // 그 외 지역은 폼이 아예 안 뜨고 즉시 콜백만 돌아온다.
    void RequestConsent()
    {
#if UNITY_EDITOR
        // 에디터의 UMP 는 플레이스홀더 구현이라, 동의 폼이 필요 없는 상태면
        // LoadAndShowConsentFormIfRequired 의 콜백을 아예 호출하지 않는다(= 흐름이 여기서 끊김).
        // 에디터에서는 동의 단계를 건너뛰고 바로 초기화한다. 실기기는 아래 정상 경로를 탄다.
        InitializeAds();
        return;
#else
        var parameters = new ConsentRequestParameters
        {
            TagForUnderAgeOfConsent = false,
            // EEA 동의창을 한국에서 테스트하려면 아래 주석을 풀고 기기 해시 ID 를 넣는다.
            // ConsentDebugSettings = new ConsentDebugSettings
            // {
            //     DebugGeography = DebugGeography.EEA,
            //     TestDeviceHashedIds = new List<string> { "여기에 기기 해시 ID" },
            // },
        };

        ConsentInformation.Update(parameters, updateError =>
        {
            if (updateError != null)
            {
                Debug.LogWarning("[Ads] 동의정보 갱신 실패: " + updateError.Message);
                InitializeAds(); // 동의 조회가 실패해도 비개인화 광고는 나가야 하므로 초기화는 진행
                return;
            }

            // 이미 광고 요청이 가능한 상태(동의 불필요 지역/이전에 동의 완료)면 폼을 기다리지 않는다.
            // 폼이 필요 없을 때 콜백이 안 오는 구현이 있어서, 여기서 먼저 끊고 들어가는 게 안전하다.
            if (ConsentInformation.CanRequestAds())
            {
                InitializeAds();
                return;
            }

            ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
            {
                if (formError != null)
                    Debug.LogWarning("[Ads] 동의 폼 실패: " + formError.Message);

                InitializeAds();
            });
        });
#endif
    }

    // ── 2) SDK 초기화 ───────────────────────────────────────────────
    void InitializeAds()
    {
        if (IsInitialized) return;

#if !UNITY_EDITOR
        // 동의를 아직 못 받은 상태면 광고 요청 자체를 하지 않는다.
        if (!ConsentInformation.CanRequestAds())
        {
            Debug.Log("[Ads] 동의 미완료 — 광고 요청 보류");
            return;
        }
#endif

        IsInitialized = true;

        if (TestDeviceIds.Count > 0)
        {
            MobileAds.SetRequestConfiguration(new RequestConfiguration
            {
                TestDeviceIds = TestDeviceIds,
            });
        }

        MobileAds.Initialize(_ =>
        {
            Debug.Log("[Ads] MobileAds 초기화 완료");

            // preload 로 표시된 위치만 미리 로드. 나머지는 Prepare()/Show() 시점에 로드된다.
            foreach (var placement in AdUnits.All)
                if (AdUnits.ShouldPreload(placement))
                    Load(GetSlot(placement));
        });
    }

    // ── 3) 로드 ─────────────────────────────────────────────────────
    Slot GetSlot(AdPlacement placement)
    {
        if (!_slots.TryGetValue(placement, out var slot))
        {
            slot = new Slot { placement = placement };
            _slots[placement] = slot;
        }
        return slot;
    }

    // 드물게 쓰는 위치를 띄우기 직전에 미리 요청해둘 때 사용 (예: 파산 화면 진입 시점)
    public void Prepare(AdPlacement placement)
    {
        if (!IsInitialized) return;
        Load(GetSlot(placement));
    }

    void Load(Slot slot)
    {
        if (!IsInitialized || slot.loading || slot.IsReady) return;

        slot.loading = true;
        string unitId = AdUnits.GetUnitId(slot.placement);

        RewardedAd.Load(unitId, new AdRequest(), (ad, error) =>
        {
            slot.loading = false;

            if (error != null || ad == null)
            {
                // 노필(no fill)은 정상 상황이라 LogError 로 올리지 않는다.
                // (ErrorReporter 는 Error/Exception 만 서버로 보내므로 경고로 두면 리포트가 안 쌓임)
                Debug.LogWarning($"[Ads] {slot.placement} 로드 실패: " +
                                 (error != null ? error.GetMessage() : "ad is null"));
                ScheduleRetry(slot);
                return;
            }

            slot.retryCount = 0;
            slot.ad = ad;
            HookEvents(slot, ad);
        });
    }

    void ScheduleRetry(Slot slot)
    {
        slot.retryCount = Mathf.Min(slot.retryCount + 1, 6);
        float delay = Mathf.Pow(2f, slot.retryCount); // 2,4,8,...,64초
        slot.nextRetryTime = Time.realtimeSinceStartup + delay;
    }

    void HookEvents(Slot slot, RewardedAd ad)
    {
        // ILRD(Impression-Level Revenue Data) — 노출 1건이 실제로 확정된 시점에만 온다.
        // AdMob-Firebase 콘솔 연동은 네트워크 집계치라 하루 지연되므로, ARPDAU를 정밀 계산하려면
        // 이 콜백으로 노출 단위 값을 직접 GA4에 심어야 한다.
        ad.OnAdPaid += adValue =>
        {
            double revenue = adValue.Value / 1_000_000.0; // micros → 통화 단위
            GameAnalytics.LogEvent("ad_impression",
                "ad_platform", "AdMob",
                "ad_source", ad.GetResponseInfo()?.GetMediationAdapterClassName() ?? "unknown",
                "ad_format", "Rewarded",
                "ad_unit_name", slot.placement.ToString(),
                "value", revenue,
                "currency", adValue.CurrencyCode);
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            ResumeGameTime();
            if (_showing == slot) _showing = null;
            _pendingReward = null;
            _pendingUnavailable = null;

            DestroyAd(slot);
            Load(slot); // 같은 위치의 다음 광고 미리 확보
        };

        ad.OnAdFullScreenContentFailed += adError =>
        {
            Debug.LogWarning($"[Ads] {slot.placement} 표시 실패: " + adError.GetMessage());

            ResumeGameTime();
            if (_showing == slot) _showing = null;

            var cb = _pendingUnavailable;
            _pendingReward = null;
            _pendingUnavailable = null;

            DestroyAd(slot);
            Load(slot);

            if (cb != null) cb.Invoke();
        };
    }

    void DestroyAd(Slot slot)
    {
        if (slot.ad == null) return;
        slot.ad.Destroy();
        slot.ad = null;
    }

    // ── 4) 표시 ─────────────────────────────────────────────────────
    public bool IsReady(AdPlacement placement)
    {
        return _slots.TryGetValue(placement, out var slot) && slot.IsReady;
    }

    // onRewarded   : 시청 완료 시 1회 호출 (보상 지급)
    // onUnavailable: 광고가 없거나 표시 실패 — 호출부가 안내 UI 를 띄운다. 보상은 주지 않는다.
    public void ShowRewarded(AdPlacement placement, Action onRewarded, Action onUnavailable = null)
    {
        if (_showing != null)
        {
            if (onUnavailable != null) onUnavailable.Invoke();
            return;
        }

        var slot = GetSlot(placement);

        if (!slot.IsReady)
        {
            Debug.Log($"[Ads] {placement} 미준비 — 로드 시작");
            Load(slot);
            if (onUnavailable != null) onUnavailable.Invoke();
            return;
        }

        _showing = slot;
        _pendingReward = onRewarded;
        _pendingUnavailable = onUnavailable;

        StopGameTime();

        slot.ad.Show(_ =>
        {
            // 보상 콜백은 광고가 닫히기 전에 온다. 지급은 여기서 1회만.
            // AdMob 콘솔의 "보상 항목" 값은 쓰지 않는다 — 액수는 게임 코드가 정한다.
            var cb = _pendingReward;
            _pendingReward = null;
            _pendingUnavailable = null;
            if (cb != null) cb.Invoke();
        });
    }

    // ── 게임 시간 연동 ──────────────────────────────────────────────
    // 광고가 떠 있는 동안 앱이 백그라운드로 가므로 게임 시간이 흐르면 안 된다.
    // GameScene 밖(로딩/아웃게임)에는 GameTimeManager 가 없으므로 명시적 null 비교로 건너뛴다.
    void StopGameTime()
    {
        if (_timeStoppedByAd) return;
        if (GameTimeManager.Instance == null) return;

        GameTimeManager.Instance.StopTime();
        _timeStoppedByAd = true;
    }

    void ResumeGameTime()
    {
        if (!_timeStoppedByAd) return;
        _timeStoppedByAd = false;

        if (GameTimeManager.Instance == null) return;
        GameTimeManager.Instance.StartTime();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
