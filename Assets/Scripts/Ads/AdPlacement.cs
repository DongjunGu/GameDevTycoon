using System.Collections.Generic;
using UnityEngine;

// 광고를 띄우는 "위치". AdMob 광고 단위와 1:1로 대응시킨다.
// 위치별로 단위를 나눠야 콘솔에서 노출수/시청완료율/eCPM 을 위치별로 볼 수 있다.
// (한 단위를 여러 곳에서 재사용하면 통계가 뭉쳐서 어디를 밀고 뺄지 판단이 안 된다)
public enum AdPlacement
{
    Test,            // 관리 서브메뉴 테스트 버튼
    DevResultDouble, // 개발 결과 발표 후 보상 2배
    BankruptRevive,  // 파산 시 1회 부활
    HiringReroll,    // 채용 후보 리롤
    MerchantReroll,  // 상인 상품 리롤
}

// 위치 → 광고 단위 ID 표.
// AdMob 콘솔에서 단위를 새로 만들면 여기 한 줄만 채우면 된다.
public static class AdUnits
{
    public struct Entry
    {
        public string androidId;
        public string iosId;
        // true 면 앱 시작 직후 미리 로드해둔다. 자주 쓰는 위치만 켤 것 —
        // 전부 켜면 앱 시작할 때 광고 요청이 위치 수만큼 동시에 나간다.
        public bool preload;
    }

    // 구글 공식 테스트 단위 — 실제 ID 가 아직 없는 위치의 폴백.
    public const string REWARDED_TEST_ANDROID = "ca-app-pub-3940256099942544/5224354917";
    public const string REWARDED_TEST_IOS = "ca-app-pub-3940256099942544/1712485313";

    // ⚠️ 릴리즈 빌드에서도 테스트 광고를 강제하는 스위치.
    //
    // 내부 테스트(플레이 콘솔) 배포로 흐름만 확인할 때 true 로 두면 실제 광고가 안 나가서
    // 무효 트래픽/계정 정지 위험이 없고, 노필도 없어 항상 100% 로드된다.
    // 실제 수익이 발생하는 배포(프로덕션 출시) 전에는 반드시 false 로 되돌릴 것.
    //
    // const 가 아니라 static readonly 인 이유: const 면 아래 if 문이 도달 불가 코드로 경고가 난다.
    public static readonly bool FORCE_TEST_ADS = true;

    static readonly Dictionary<AdPlacement, Entry> Table = new Dictionary<AdPlacement, Entry>
    {
        { AdPlacement.Test, new Entry {
            androidId = "ca-app-pub-4551340166428636/9791424179",
            iosId     = "", // TODO: iOS 앱(~5301514821)에 보상형 단위 발급 후 입력
            preload   = true } },

        { AdPlacement.DevResultDouble, new Entry {
            androidId = "", // TODO: AdMob 콘솔에서 발급
            iosId     = "",
            preload   = true } },

        { AdPlacement.BankruptRevive, new Entry {
            androidId = "", // TODO
            iosId     = "",
            preload   = false } }, // 파산은 드물다 — 필요할 때 Prepare()

        { AdPlacement.HiringReroll, new Entry {
            androidId = "", // TODO
            iosId     = "",
            preload   = false } },

        { AdPlacement.MerchantReroll, new Entry {
            androidId = "", // TODO
            iosId     = "",
            preload   = false } },
    };

    static readonly HashSet<AdPlacement> _fallbackWarned = new HashSet<AdPlacement>();

    // 에디터/개발빌드는 무조건 테스트 단위. 실제 단위를 개발 중에 클릭하면 무효 트래픽으로 계정이 정지된다.
    public static string GetUnitId(AdPlacement placement)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return TestUnitId;
#else
        if (FORCE_TEST_ADS)
        {
            if (_fallbackWarned.Add(placement))
                Debug.LogWarning($"[Ads] FORCE_TEST_ADS 켜짐 — {placement} 테스트 광고 사용(수익 없음)");
            return TestUnitId;
        }

        if (!Table.TryGetValue(placement, out var e)) return TestUnitId;

#if UNITY_IPHONE
        string id = e.iosId;
#else
        string id = e.androidId;
#endif
        if (string.IsNullOrEmpty(id))
        {
            // 아직 단위를 안 판 위치 — 빈 ID 로 요청하면 예외가 나므로 테스트 단위로 폴백.
            if (_fallbackWarned.Add(placement))
                Debug.LogWarning($"[Ads] {placement} 광고 단위 미설정 — 테스트 단위로 폴백(수익 없음)");
            return TestUnitId;
        }
        return id;
#endif
    }

    public static bool ShouldPreload(AdPlacement placement)
    {
        return Table.TryGetValue(placement, out var e) && e.preload;
    }

    public static IEnumerable<AdPlacement> All => Table.Keys;

    static string TestUnitId
    {
        get
        {
#if UNITY_IPHONE
            return REWARDED_TEST_IOS;
#else
            return REWARDED_TEST_ANDROID;
#endif
        }
    }
}
