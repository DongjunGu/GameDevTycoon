using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LeaderScoreUI : MonoBehaviour
{
    public static LeaderScoreUI Instance { get; private set; }

    [Header("직원 정보")]
    public GameObject leaderscorePanel;
    public TextMeshProUGUI leaderNameText;
    public TextMeshProUGUI leaderRoleText;
    public TextMeshProUGUI leaderGradeText;
    public TextMeshProUGUI leaderPotentialText;
    public Image categoryIcon;                // 현재 진행 중인 카테고리 (기획/개발/아트) — 텍스트 대신 아이콘
    public RoleIconSet categoryIconSet;        // 공용 역할 아이콘 세트

    [Header("회차 결과 (1~4회차 순서대로, 길이 4)")]
    public TextMeshProUGUI[] roundScoreTexts; // 각 회차 점수
    public TextMeshProUGUI dsText;            // 누적 ds (스트레스)
    public Slider dsSlider;                   // 누적 ds (0~100)
    public TextMeshProUGUI totalText;         // 팀장 점수 총합
    public Button confirmButton;

    [Header("dsSlider Fill Area/Fill — 스트레스 구간별 스프라이트 (인스펙터에서 직접 할당)")]
    public Image fillImage;
    public Sprite fillSpriteLow;  // ~69
    public Sprite fillSpriteMid;  // 70~89
    public Sprite fillSpriteHigh; // 90 이상

    [Header("회차 강조 (ScorePanel/Score1~4, 1~4회차 순서대로) — 진행 중인 회차만 높이/글씨 확대, 아니면 원상복구")]
    public RectTransform[] scoreRows;
    public float scoreRowActiveFontSize = 40f;
    public float scoreRowActiveHeightGrow = 25f;

    private readonly Dictionary<TextMeshProUGUI, float> _scoreRowBaseFontSize = new();
    private readonly Dictionary<RectTransform, Vector2> _scoreRowBaseSizeDelta = new();

    [Header("스트레스 슬라이더 눈금 (90/95/99% 보너스 임계선 — 위치는 직접 배치, 평소엔 비활성 상태로 둘 것)")]
    public RectTransform tick90Mark;
    public RectTransform tick95Mark;
    public RectTransform tick99Mark;
    [Tooltip("각 nImage 자식의 보너스 점수 텍스트 (비워두면 자동으로 자식 TextMeshProUGUI를 찾아서 씀)")]
    public TextMeshProUGUI tick90BonusText;
    public TextMeshProUGUI tick95BonusText;
    public TextMeshProUGUI tick99BonusText;
    [Tooltip("각 nBG의 roleIcon — TopIcon/RoleIcon과 동일하게 InitPanel에서 도전 파트 아이콘으로 갱신")]
    public Image tick90RoleIcon;
    public Image tick95RoleIcon;
    public Image tick99RoleIcon;
    [Tooltip("각 nBG의 dimImage — 해당 임계선을 실제로 달성하면 활성화(정산 시점, RefreshBonusActualTexts)")]
    public GameObject tick90DimImage;
    public GameObject tick95DimImage;
    public GameObject tick99DimImage;
    [Tooltip("\"스트레스 폭발까지 n\" — 3회차 끝난 시점 누적ds 기준으로 갱신")]
    public TextMeshProUGUI burstText;
    [Tooltip("burstText 커졌다 작아졌다 반복(4회차 대기 시작~4회차 연출 종료까지)")]
    public float burstPulseScale = 1.15f;
    public float burstPulseDuration = 0.4f;
    Tween _burstPulseTween;
    [Tooltip("StressPanel/BackgroundFrame — burst(오버플로) 시 sprite를 crackedSprite로 교체, 새 세션 시작 시 원래 sprite로 복원")]
    public Image backgroundFrame;
    public Sprite backgroundFrameCrackedSprite;
    Sprite _backgroundFrameNormalSprite;

    [Header("버스트 연출 (스트레스 100 오버플로 시)")]
    [Tooltip("MenuCanvas 직속 풀스크린 — burst 순간 화면을 가리는 플래시. alpha 0→255로 빠르게 튀었다가 다시 0으로 사라짐")]
    public Image burstFlashImage;
    public float burstFlashInDuration = 0.08f;  // 0→255 (빠르게 터지는 느낌)
    public float burstFlashOutDuration = 0.35f; // 255→0 (서서히 사라짐)
    [Tooltip("DeskPanel/AfterBurstImage — burst 이후 계속 표시되는 붕괴 연출(자식 FailImage/SmokeImage 포함). 새 세션 시작 시 다시 비활성화")]
    public GameObject afterBurstImage;
    [Tooltip("AfterBurstImage 자식 FailImage들 — burst 시작과 함께 계속 Z축 회전")]
    public RectTransform[] failImages;
    public float failSpinPeriod = 2f; // 1바퀴 도는 데 걸리는 시간(초)
    [Tooltip("AfterBurstImage 자식 SmokeImage들 — 프레임 1장짜리라 스케일업+페이드로 연기 터지는 느낌을 낸다")]
    public Image[] smokeImages;
    public float smokeStartScale = 0.6f;
    public float smokeEndScale = 1.4f;
    public float smokeDuration = 0.9f;
    public float smokeRiseDistance = 40f; // 위로 살짝 떠오르는 거리
    public float smokeRestAlpha = 0.4f;   // 다 퍼진 뒤 남아서 유지되는 alpha(잔여 연기)
    private readonly Dictionary<Image, Vector2> _smokeBasePos = new();

    [Header("스트레스 경고 (LeaderScoreEmergencyImage — MenuCanvas 직속 풀스크린, SafeArea 밖)")]
    public Image stressWarningImage;          // LeaderScoreEmergencyImage의 Image — 기본 비활성+alpha 0, InitPanel~OnClickConfirm 구간에만 활성
    public float stressBlinkInterval = 0.3f;  // sin파 반 주기(초) — 값↔값 왕복 한 방향 시간
    private float _currentDs = 0f;

    [Tooltip("LeaderScoreLastSpurtImage — MenuCanvas 직속 풀스크린(블러 배경용). 3회차 연출 끝난 직후 켰다가 " +
             "fly-in/hold/fly-out 연출이 다 끝나면 끄고, 그 다음 aimButtonsRoot(조준 버튼)가 뜬다.")]
    public GameObject lastSpurtImage;
    [Tooltip("lastSpurtImage 자식(Text (TMP)) — 화면 왼쪽 밖에서 빠르게 날아들어와 중앙에서 대기했다가 오른쪽 밖으로 빠르게 날아나감")]
    public RectTransform lastSpurtContent;
    public float lastSpurtFlyDuration = 0.25f; // 좌→중앙 / 중앙→우 각 구간 소요 시간(빠르게)
    public float lastSpurtHoldDuration = 1f;   // 중앙에 머무는 시간
    public float lastSpurtFlyDistance = 2200f; // 화면 밖으로 확실히 벗어나는 X 오프셋(중앙 기준)

    [Header("좌우 커튼 (팀장점수 진행 중 alpha 왕복)")]
    public Image leftCurtain;
    public Image rightCurtain;
    public float curtainBlinkPeriod = 0.3f;   // 206→255→206 한 바퀴 도는 데 걸리는 시간(초)
    private bool _curtainActive = false;

    [Header("연출")]
    public float rollDuration = 1f;   // 회차 점수/ds 상승 시간
    public float roundGap = 1f;       // 회차 간 간격

    [Header("회차 점수 팝콘 연출")]
    public GameObject iconPrefab;     // 회차 점수만큼 개수가 터지는 아이콘
    public Transform popcornPoint;    // 아이콘들이 터져나오는 지점
    public float popDuration = 1.5f;    // 터진 뒤 다음 단계(빨려들어가기) 전까지 총 대기 시간(= 착지 후 바닥에 머무는 시간 포함)
    public float popFlightDuration = 0.3f; // 그 중 실제로 포물선 그리며 날아가 바닥에 착지하기까지 걸리는 시간(빠르게 터져나감)
    public float popScatterRangeX = 400f; // 터질 때 X축으로 최대 ± 얼마나 흩어질지
    public float popFloorY = -140f;   // 떨어져서 착지하는 바닥 Y 좌표(로컬)
    public float popArcHeight = 220f; // 포물선 정점 높이감
    public float suckDuration = 0.15f; // 아이콘 1개가 categoryIcon으로 빨려들어가는 데 걸리는 시간(개별 기준)
    [Tooltip("첫 아이콘의 흡입 진행률(0~1)이 이 값에 도달하면 총점 커짐 펄스 발동. 0=흡수 시작 즉시, 1=RoleIcon에 도착하는 순간")]
    [Range(0f, 1f)] public float growTriggerRatio = 0.8f;
    // 아이콘들이 순차적으로 빨려들어가기 "시작"하는 전체 목표 시간(초). 개수가 몇 개든 이 시간 안에 전부
    // 시작되도록 간격을 자동 계산(간격 = 이 값 / 개수)해서, 개수가 많아져도 총 재생시간이 무한정 늘어나지 않는다.
    // 0이면 기존처럼 일괄 동시 시작.
    public float suckStaggerWindow = 1f;
    public RectTransform topIcon;      // TopIcon(프레임) — 빨려들어가는 동안 총점 텍스트와 함께 살짝 커졌다 원상복구
    [Tooltip("TopPanel/UpImage — 점수가 흡수되는 동안(PopAndFlyCoroutine) 활성화, 끝나면 비활성화")]
    public GameObject upImage;
    [Tooltip("TopPanel/DownImage — burst로 전 회차 점수가 깎이는 동안(ApplyCutCoroutine) 활성화, 끝나면 비활성화. 활성화되어 있는 동안 alpha로 downBlinkCount번 깜빡인다.")]
    public GameObject downImage;
    public int downBlinkCount = 2; // downImage가 켜져 있는 동안(ApplyCutCoroutine 지속시간 기준) 깜빡이는 횟수
    // topIcon/totalText 흡입 펄스 — 아이콘이 하나씩 도착할 때마다 punchStrength를 그 그룹의 아이콘
    // 개수로 나눈 만큼씩 조금씩 누적해서 커진다(PulseSession 참고). punchStrength는 그 그룹의 아이콘이
    // 전부 도착했을 때 도달하는 최종 배율(1+punchStrength)이고, punchDuration은 각 단계 트윈 시간.
    // private int _pulseActiveCount; // [기존 방식 전용 필드 — 주석 처리]
    public float punchStrength = 0.35f;
    public float punchDuration = 0.35f;
    public float burstSpitDuration = 0.4f; // 스트레스 100 오버플로 시 총점 위치에서 아이콘을 역방향으로 뱉어내는 시간

    [Header("보너스 아이콘 흡수 (4회차 끝나고 90/95/99BG의 roleIcon → TopPanel RoleIcon, 3점당 1개)")]
    [Tooltip("BonusIconPrefab — 순수 Image 1장짜리 전용 프리팹(회차 팝콘용 iconPrefab과 별개). 비워두면 iconPrefab으로 대체.")]
    public GameObject bonusIconPrefab;
    [Tooltip("LeaderScoreEntirePanel — ModalLayer가 여기에 overrideSorting Canvas를 붙여서 패널 전체를 최상단으로 올린다. " +
             "보너스 아이콘을 이 안쪽에 붙여야 같은 정렬을 상속해 패널 뒤로 그려지지 않는다(이 컴포넌트 자신의 transform은 " +
             "이 패널의 형제라 정렬 대상 밖이라 뒤에 그려짐). 비워두면 그냥 이 컴포넌트의 transform으로 대체.")]
    public Transform flyingIconLayer;
    public float bonusDipRadius = 260f;       // roleIcon에서 아래쪽으로 퍼지는 최대 거리
    public float bonusDipArcDegrees = 150f;   // 아래(-90°)를 중심으로 부채꼴로 퍼지는 각도 폭 — 클수록 옆으로도 넓게 퍼짐
    public float bonusDipDuration = 0.2f;    // ① 아래쪽으로 여유있게 미끄러지며 퍼지는 데 걸리는 시간(전부 동시 진행) — 끝나면 멈추지 않고 바로 ②로 이어진다
    [Tooltip("bonusDipDuration(초반 퍼짐)이 끝난 뒤, 아직 차례가 안 된 아이콘이 계속 같은 방향으로 흩어지는 속도를 " +
             "초반 평균 속도(bonusDipRadius/bonusDipDuration) 대비 몇 배로 늦출지. 1이면 초반과 동일 속도로 계속, " +
             "0에 가까울수록 초반 퍼짐이 끝난 뒤엔 거의 멈춘 것처럼 아주 천천히 흩어진다.")]
    [Range(0f, 1f)] public float bonusDriftSpeedFactor = 0.15f;
    public float bonusFlyDuration = 0.2f;    // ② 아이콘 1개가 흩어진 위치에서 categoryIcon까지 빨려들어가는 시간
    // 아이콘이 몇 개든 ③단계 첫 아이콘 시작부터 마지막 1개가 흡수되기까지 항상 이 시간 안에 끝나도록,
    // 아이콘 사이 시작 간격(stagger)을 개수에 반비례해서 자동으로 줄인다 — round 아이콘의
    // suckStaggerWindow와 동일한 방식. bonusFlyDuration이 이 값보다 크면 stagger가 0이 되어(전부 동시
    // 시작) 그만큼은 걸린다.
    public float bonusTotalAbsorbDuration = 0.5f;

    [Header("팀장 캐릭터 프리뷰 (working 애니메이션)")]
    public Image characterImage;              // Resources/Characters/{portraitId} 프리팹의 working 스프라이트를 그대로 미러링
    public string previewLayerName = "PreviewLayer"; // 어떤 카메라에도 안 잡히게 격리할 레이어
    public Animator fireAnimator;             // FireAnim의 Animator — 수치 다 오르면 캐릭터 애니와 함께 정지

    private GameObject _previewInstance;
    private SpriteRenderer _previewSpriteRenderer;
    private Animator _previewAnimator;

    private System.Action _onComplete;

    // confirm 시 DevelopmentPanelUI 에 한 번에 적용할 팀장점수 (애니 종료 시 산출)
    private float _applyPlanning, _applyDevelop, _applyArt;

    // 1~3회차 재생 후 4회차 대기 상태를 이어가기 위한 값 (ShowPendingRound4 ~ PlayRound4AndFinish 사이 보관)
    private float _pendingDisplayedTotal;
    private float _pendingPrevCumDs;
    private LeaderType _pendingUiType;

    // 4회차까지 반영된 총점(보너스 제외) — 보너스 아이콘이 흡입될 때마다 여기서부터 더해진다.
    // 90/95/99 각각의 BonusFlyCoroutine이 동시에 돌 수 있어(모두 지급됐을 때) _bonusDisplayedTotal은
    // 공유 누산값으로 두고 각자 자기 몫만 더하는 방식으로 레이스를 피한다.
    private float _bonusBaselineTotal;
    private float _bonusDisplayedTotal;

    // 회차 연출 코루틴이 "화면상 재생"을 끝낸 시점(confirmButton이 눌릴 수 있게 된 순간)마다 호출됨 —
    // 정상 4회차 완료든 오버플로(burst)로 조기 종료든 동일하게 발생. 온보딩 튜토리얼(7-6)이 burst
    // 연출이 다 끝난 뒤에 대사를 띄우기 위해 구독. _onComplete(게임 진행 재개)와는 별개 — 그건 유저가
    // confirmButton을 눌러야만 발동.
    public System.Action OnRoundsVisualComplete;

    // confirmButton을 눌러 패널이 실제로 닫힌 직후(_onComplete 호출 다음) 발동 — 온보딩 튜토리얼 8-1이
    // "패널이 닫히고 나서" SupriseQuestUI를 강조하기 위해 구독.
    public System.Action OnConfirmClosed;

    // 1~3회차 "연출"까지 다 재생되고 4회차 조준 선택을 기다리는 중인지 — 계산 완료(DevelopmentManager.IsPendingRound4Aim)와
    // 달리 코루틴 애니메이션이 실제로 끝난 시점에만 true. 버튼 UI는 이 값을 폴링해야 함.
    public bool IsWaitingForRound4Aim { get; private set; }

    // 테스트 진입(DevelopmentManager.TestLeaderScore)이면 true — 확정해도 DevelopmentPanelUI에 스탯 반영 안 함.
    private bool _testMode;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        _burstPulseTween?.Kill();
    }

    // burstText(및 보너스 미리보기 텍스트)를 켠다 — 3회차 연출이 끝나 4회차 대기가 시작될 때 1회 호출.
    // tick90/95/99Mark 자체는 InitPanel에서 이미 1회차부터 켜져 있음. 이 시점엔 아직 4회차가 안
    // 굴려졌으므로, 실제 획득액이 아니라 "이 임계선까지 도달하면 받을 수 있는" 잠재 범위(중첩 누적)를
    // 미리보기로 보여준다.
    void ShowThresholdTicks()
    {
        if (burstText != null) burstText.gameObject.SetActive(true);
        RefreshBonusPotentialTexts();
        UpdateBurstText();
        StartBurstPulse();
    }

    // burstText 커졌다(1.15배)~작아졌다(1배)를 4회차 대기~연출 종료까지 무한 반복.
    void StartBurstPulse()
    {
        if (burstText == null) return;
        _burstPulseTween?.Kill();
        burstText.transform.localScale = Vector3.one;
        _burstPulseTween = burstText.transform.DOScale(burstPulseScale, burstPulseDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true);
    }

    // 4회차 연출이 끝나거나(정상/오버플로 모두) 새 세션이 시작되면 반복을 멈추고 크기를 원상복구.
    void StopBurstPulse()
    {
        _burstPulseTween?.Kill();
        _burstPulseTween = null;
        if (burstText != null) burstText.transform.localScale = Vector3.one;
    }

    // "스트레스 폭발까지 n" — 3회차 끝난 시점 누적ds(_pendingPrevCumDs) 기준으로 100까지 남은 값.
    void UpdateBurstText()
    {
        if (burstText == null) return;
        int remain = Mathf.RoundToInt(100f - _pendingPrevCumDs);
        burstText.text = $"스트레스 폭발까지 <color=#E63356>{remain}</color>";
    }

    // tick90/95/99Mark 자체는 1회차부터 계속 보이므로(InitPanel에서 직접 활성화) 여기선 안 건드림 —
    // burstText(3회차 끝나야 의미있는 값)와 dimImage(달성 표시)만 새 세션 시작 시 초기화.
    void HideThresholdTicks()
    {
        StopBurstPulse();
        if (burstText != null) burstText.gameObject.SetActive(false);
        if (tick90DimImage != null) tick90DimImage.SetActive(false);
        if (tick95DimImage != null) tick95DimImage.SetActive(false);
        if (tick99DimImage != null) tick99DimImage.SetActive(false);
    }

    // 3회차 끝난 시점 미리보기 — DevelopmentManager.GetLeaderBonusPotentialAmount가 이 시점에 임계선별
    // 금액을 확정(단 1회 굴림)해두므로, 4회차 끝나고 RefreshBonusActualTexts가 표시하는 실제 지급액과
    // 항상 동일한 값이 나온다(재추첨 없음, 유저 확정 사양).
    void RefreshBonusPotentialTexts()
    {
        SetBonusPotentialText(tick90Mark, ref tick90BonusText, 0);
        SetBonusPotentialText(tick95Mark, ref tick95BonusText, 1);
        SetBonusPotentialText(tick99Mark, ref tick99BonusText, 2);
    }

    void SetBonusPotentialText(RectTransform tick, ref TextMeshProUGUI text, int thresholdIndex)
    {
        if (text == null && tick != null) text = tick.GetComponentInChildren<TextMeshProUGUI>();
        if (text == null || DevelopmentManager.Instance == null) return;

        float amount = DevelopmentManager.Instance.GetLeaderBonusPotentialAmount(thresholdIndex);
        text.text = $"+{Mathf.RoundToInt(amount)}";
    }

    // 4회차까지 끝난 뒤(정산 시점) — 실제로 획득한 보너스 금액으로 교체. DevelopmentManager.GetLeaderBonusAmount가
    // 단일 소스. 90/95/99 중 실제로 흡입 아이콘이 뜬 것들의 Coroutine을 모아서 반환 — 호출자가 이걸 전부
    // yield해서 "보너스 흡입이 다 끝날 때까지" 기다릴 수 있게(confirmBtn을 그 전에 누르면 안 되므로).
    List<Coroutine> RefreshBonusActualTexts()
    {
        var coroutines = new List<Coroutine>(3);
        void Add(Coroutine c) { if (c != null) coroutines.Add(c); }
        Add(SetBonusActualText(tick90Mark, ref tick90BonusText, tick90DimImage, tick90RoleIcon, 0));
        Add(SetBonusActualText(tick95Mark, ref tick95BonusText, tick95DimImage, tick95RoleIcon, 1));
        Add(SetBonusActualText(tick99Mark, ref tick99BonusText, tick99DimImage, tick99RoleIcon, 2));
        return coroutines;
    }

    Coroutine SetBonusActualText(RectTransform tick, ref TextMeshProUGUI text, GameObject dimImage, Image roleIcon, int thresholdIndex)
    {
        if (text == null && tick != null) text = tick.GetComponentInChildren<TextMeshProUGUI>();
        bool granted = DevelopmentManager.Instance != null && DevelopmentManager.Instance.IsLeaderBonusGranted(thresholdIndex);
        if (dimImage != null) dimImage.SetActive(granted);
        if (text == null || DevelopmentManager.Instance == null) return null;

        // 이 임계선을 실제로 못 넘었으면(granted=false) 라벨을 절대 건드리지 않는다 — 안 그러면
        // GetLeaderBonusAmount(누적 합산)가 하위 임계선까지만 지급된 값으로 떨어져서, 3회차 끝난 시점
        // 미리보기(예: 99 라벨의 "+80" — 90+95+99 다 받았다고 가정한 낙관적 금액)가 4회차 결과가 나오는
        // 순간 엉뚱하게 더 작은 값(예: 90만 실제로 받았다면 "+4")으로 바뀌어 보이는 버그가 있었다.
        // dimImage가 이미 "달성 못함"을 시각적으로 알려주므로 숫자는 미리보기 그대로 둔다.
        if (!granted) return null;

        // 라벨("+n")은 예전처럼 이 임계선까지 중첩 누적된 금액을 보여준다(90+95+99 다 받았으면 99 라벨엔
        // 셋 다 합산해서 표시).
        float cumulativeAmount = DevelopmentManager.Instance.GetLeaderBonusAmount(thresholdIndex);
        text.text = $"+{Mathf.RoundToInt(cumulativeAmount)}";

        // 흡입 아이콘/총점 가산은 반드시 "이 임계선 하나만의" 개별 금액을 써야 한다 — 누적값을 쓰면
        // 90/95/99가 다 지급됐을 때 하위 임계선 금액이 여러 번 겹쳐서 더해진다.
        float individualAmount = DevelopmentManager.Instance.GetLeaderBonusAmountIndividual(thresholdIndex);
        if (individualAmount > 0f)
        {
            // 3점당 아이콘 1개, 단 최소 1개는 반드시 나오게 한다 — floor만 쓰면 소액 보너스일 때
            // 아이콘이 하나도 안 생겨 "빨려들어가는 연출 자체가 없다"고 보임.
            int iconCount = Mathf.Max(1, Mathf.FloorToInt(individualAmount / 3f));
            return StartCoroutine(BonusFlyCoroutine(roleIcon, iconCount, individualAmount));
        }
        return null;
    }

    // count개의 bonusIconPrefab(전용 Image 프리팹, 없으면 iconPrefab으로 대체)을 roleIcon(90/95/99BG 자식)
    // 위치에서 생성 → 각자 배정된 방향(아래쪽 부채꼴)으로 "차례가 될 때까지" 멈추지 않고 계속 흩어지다가
    // → 자기 차례(stagger로 순서가 매겨짐)가 되면 그 순간 있던 자리에서 categoryIcon(TopPanel RoleIcon)
    // 으로 빨려들어가며 축소되어 사라진다. 아이콘이 몇 개든 뒤 순번일수록 그만큼 더 오래, 더 멀리
    // 흩어지다가 흡수된다(멈춰서 대기하는 구간 없음). 아이콘 하나가 도착할 때마다 totalAmount/count
    // (이 임계선 개별 금액을 아이콘 수만큼 등분)씩 총점(totalText)을 밀어올린다 — _bonusDisplayedTotal은
    // 90/95/99 코루틴이 동시에 돌아도 서로 자기 몫만 더하는 공유 누산값.
    IEnumerator BonusFlyCoroutine(Image roleIcon, int count, float totalAmount)
    {
        GameObject prefab = bonusIconPrefab != null ? bonusIconPrefab : iconPrefab;
        if (count <= 0 || prefab == null || roleIcon == null || categoryIcon == null) yield break;

        Vector3 originPos = roleIcon.transform.position;
        Vector3 targetPos = categoryIcon.transform.position;
        // popcornPoint는 MiddlePanel(RectMask2D 있음) 안에 있어서, 그 밖(스트레스 게이지 쪽 90/95/99
        // roleIcon)에서 시작하는 보너스 아이콘을 여기 붙이면 마스크에 잘려 아예 안 보인다. 그렇다고
        // 이 컴포넌트 자신의 transform(LeaderSelectUI)을 쓰면, ModalLayer가 LeaderScoreEntirePanel에
        // 붙이는 overrideSorting Canvas의 "형제"가 되어 정렬 대상 밖으로 빠져 패널 뒤에 그려진다 —
        // flyingIconLayer(LeaderScoreEntirePanel 안쪽)를 부모로 써야 같은 정렬을 상속해 항상 위에 뜬다.
        Transform parent = flyingIconLayer != null ? flyingIconLayer : transform;

        var icons = new System.Collections.Generic.List<RectTransform>(count);
        var scatterOffsets = BuildDownwardFanScatterOffsets(count, bonusDipRadius, bonusDipArcDegrees); // 아래쪽으로 부채꼴로 퍼짐

        // scatterOffsets는 픽셀(캔버스 로컬) 단위인데 originPos는 월드 좌표(.position)라 캔버스 스케일
        // (lossyScale)을 곱해서 변환해야 한다 — 안 곱하면 화면 크기 몇 배에 달하는 거리로 튕겨나가
        // 엉뚱한 곳에 나타난다. 방향(directions)은 크기와 무관한 단위벡터만 뽑아서, 아래에서 "차례가 될
        // 때까지" 그 방향으로 계속 더 멀리 흩어지는 데 쓴다(scatterOffsets 자체는 더 이상 목표 지점이 아님).
        float worldScale = roleIcon.transform.lossyScale.x;
        var directions = new System.Collections.Generic.List<Vector2>(count);

        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(prefab, parent);
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) { Destroy(go); continue; }
            rt.position = originPos;
            rt.localScale = Vector3.zero; // 0에서 시작 — 커지면서 미끄러져 나옴

            // iconPrefab으로 대체됐을 땐 그 안의 "iconImage" 자식이 아이콘이므로 먼저 찾고, bonusIconPrefab
            // (루트 자신이 순수 Image 1장)처럼 그 자식이 없으면 루트의 Image를 그대로 쓴다.
            var iconImage = go.transform.Find("iconImage");
            var iconImg = iconImage != null ? iconImage.GetComponent<Image>() : go.GetComponent<Image>();
            if (iconImg != null) iconImg.sprite = roleIcon.sprite;

            icons.Add(rt);
            var dir = scatterOffsets[i];
            directions.Add(dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.down);
        }

        float dipDur = Mathf.Max(0.01f, bonusDipDuration);          // 흩어지기 시작 → 최초 bonusDipRadius 지점까지 도달하는 데 걸리는 시간
        float baseDist = bonusDipRadius * worldScale;
        float continuousSpeed = (baseDist / dipDur) * bonusDriftSpeedFactor; // dipDur 이후엔 훨씬 느린 속도로 계속 더 멀리 흩어짐(대기 없음)

        // stagger(아이콘 사이 차례 간격)는 개수에 반비례해서 자동으로 줄어든다 — 흡수 구간 자체의 길이가
        // 개수와 무관하게 대략 bonusTotalAbsorbDuration을 넘지 않도록 보장.
        float perIconDur = Mathf.Max(0.01f, bonusFlyDuration);
        float staggerWindow = Mathf.Max(0f, bonusTotalAbsorbDuration - perIconDur);
        float stagger = icons.Count > 0 ? staggerWindow / icons.Count : 0f;

        // 아이콘 i의 "차례"(흡수 시작 시각) — 전부 최소 dipDur만큼은 흩어진 뒤(0번 포함), 뒤 순번일수록
        // stagger만큼씩 더 늦게(=그만큼 더 오래, 더 멀리 흩어지다가) 차례가 온다.
        var absorbStart = new float[icons.Count];
        for (int i = 0; i < icons.Count; i++) absorbStart[i] = dipDur + i * stagger;
        var absorbStartPos = new Vector3[icons.Count];
        var absorbStartCaptured = new bool[icons.Count];

        float totalDur = icons.Count > 0 ? absorbStart[icons.Count - 1] + perIconDur : 0f;
        float amountPerIcon = totalAmount / Mathf.Max(1, icons.Count);
        var iconArrived = new bool[icons.Count];
        // bool grown = false; // [기존, 주석 처리] — 아래 PulseSession으로 교체
        var pulseSession = icons.Count > 0 ? BeginPulseSession() : null;
        float pulseStepAmount = punchStrength / Mathf.Max(1, icons.Count);

        float elapsed = 0f;
        while (elapsed < totalDur)
        {
            elapsed += Time.deltaTime;
            for (int i = 0; i < icons.Count; i++)
            {
                if (icons[i] == null) continue;

                if (elapsed < absorbStart[i])
                {
                    // 아직 차례가 안 됨 — 배정된 방향으로 계속 흩어지는 중(멈추지 않음).
                    float dist;
                    if (elapsed <= dipDur)
                    {
                        float ratio = elapsed / dipDur;
                        dist = baseDist * (1f - Mathf.Pow(1f - ratio, 2f)); // 완만한 ease-out으로 여유있게 미끄러지며 펼쳐짐
                    }
                    else
                    {
                        dist = baseDist + (elapsed - dipDur) * continuousSpeed; // 그 이후로도 같은 속도로 계속 더 멀리
                    }
                    icons[i].position = originPos + (Vector3)(directions[i] * dist);

                    float scaleRatio = Mathf.Clamp01(elapsed / dipDur);
                    icons[i].localScale = Vector3.one * (scaleRatio < 0.6f
                        ? Mathf.Lerp(0f, 1.15f, scaleRatio / 0.6f)
                        : Mathf.Lerp(1.15f, 1f, (scaleRatio - 0.6f) / 0.4f));
                }
                else
                {
                    // 차례가 되어 categoryIcon으로 빨려들어가는 중 — 흩어지던 그 자리에서 시작.
                    if (!absorbStartCaptured[i])
                    {
                        absorbStartCaptured[i] = true;
                        absorbStartPos[i] = icons[i].position;
                    }
                    float t = Mathf.Clamp01((elapsed - absorbStart[i]) / perIconDur);
                    float eased = t * t * t * t; // 강한 ease-in — 빨려들어가는 느낌
                    icons[i].position = Vector3.Lerp(absorbStartPos[i], targetPos, eased);
                    icons[i].localScale = Vector3.one * (1f - eased * 0.7f);

                    if (!iconArrived[i] && t >= 1f)
                    {
                        iconArrived[i] = true;
                        _bonusDisplayedTotal += amountPerIcon;
                        if (totalText) totalText.text = Mathf.RoundToInt(_bonusBaselineTotal + _bonusDisplayedTotal).ToString();
                        // if (!grown) { grown = true; GrowPulseIfNeeded(); } // [기존, 주석 처리]
                        StepPulseSession(pulseSession, pulseStepAmount); // 아이콘 하나 도착 — punchStrength/개수만큼 조금씩 커짐
                    }
                }
            }
            yield return null;
        }

        // if (grown) ShrinkPulseIfDone(); // [기존, 주석 처리]
        EndPulseSession(pulseSession); // 이 그룹(이 임계선) 아이콘 전부 흡수 완료 — 다른 그룹도 없으면 원상복구

        foreach (var rt in icons)
            if (rt != null) Destroy(rt.gameObject);
    }

    // fullRoundScores: 차감 전 회차 점수 / roundScores: 차감 반영 최종 회차 점수
    // cumDsAfter: 회차 종료 시점 누적 ds / overflowRound: 누적 ds 100 초과 회차(없으면 -1)
    // 4회차까지 이미 다 정해진 데이터를 처음부터 끝까지 재생 (저장값 재생 / 1~3회차 중 오버플로로 이미 끝난 경우).
    public void Show(EmployeeData employee, LeaderType type,
                     float[] fullRoundScores, float[] roundScores, float[] cumDsAfter,
                     float total, int overflowRound, float cutFactor,
                     System.Action onComplete,
                     bool testMode = false)
    {
        InitPanel(employee, type, testMode);
        _onComplete = onComplete;

        StartCoroutine(PlayRoundsCoroutine(type, fullRoundScores, roundScores, cumDsAfter,
                                     total, overflowRound, cutFactor, 0, 4));
    }

    // 1~3회차만 재생하고 4회차 조준 선택을 기다린다 (정산/커튼오프/확정버튼 활성화 없음).
    // 4회차 값은 아직 안 정해졌으므로 total/overflow 등은 넘기지 않는다.
    public void ShowPendingRound4(EmployeeData employee, LeaderType type,
                     float[] fullRoundScores, float[] roundScores, float[] cumDsAfter,
                     bool testMode = false)
    {
        InitPanel(employee, type, testMode);

        StartCoroutine(PlayRoundsCoroutine(type, fullRoundScores, roundScores, cumDsAfter,
                                     0f, -1, 0f, 0, 3));
    }

    // 온보딩 튜토리얼(7-1/7-2) 전용 — 1회차만 재생하고 멈춘다. 정상 흐름의 "4회차 대기"
    // 눈금/버튼(90/95/99 임계선, aimButtonsRoot)은 아직 3회차가 안 끝났으므로 절대 안 뜨게
    // announceRound4Wait=false로 호출(끝나도 IsWaitingForRound4Aim 안 세움).
    public void ShowRound1Then(EmployeeData employee, LeaderType type,
                     float[] fullRoundScores, float[] roundScores, float[] cumDsAfter,
                     System.Action onRound1Done, bool testMode = false)
    {
        InitPanel(employee, type, testMode);
        StartCoroutine(PlayRound1ThenCoroutine(type, fullRoundScores, roundScores, cumDsAfter, onRound1Done));
    }

    IEnumerator PlayRound1ThenCoroutine(LeaderType type,
                     float[] fullRoundScores, float[] roundScores, float[] cumDsAfter,
                     System.Action onRound1Done)
    {
        yield return StartCoroutine(PlayRoundsCoroutine(type, fullRoundScores, roundScores, cumDsAfter,
                                     0f, -1, 0f, 0, 1, announceRound4Wait: false));
        onRound1Done?.Invoke();
    }

    // 온보딩 튜토리얼 전용 — ShowRound1Then으로 1회차만 재생해둔 상태에서 2~3회차를 이어서 재생.
    // InitPanel을 다시 부르지 않음(이미 초기화됨, _pendingDisplayedTotal/_pendingPrevCumDs가 1회차
    // 결과를 들고 있어 PlayRoundsCoroutine 내부에서 자연히 이어받음). 끝나면 기존 ShowPendingRound4와
    // 동일하게 4회차 대기 상태(눈금/aimButtonsRoot 노출)로 전환된다.
    public void ContinueRounds2And3(LeaderType type,
                     float[] fullRoundScores, float[] roundScores, float[] cumDsAfter)
    {
        StartCoroutine(PlayRoundsCoroutine(type, fullRoundScores, roundScores, cumDsAfter,
                                     0f, -1, 0f, 1, 3));
    }

    // 유저가 조준(약/중/강)을 선택해 4회차가 계산된 뒤 호출 — 4회차만 재생하고 최종 정산까지 이어간다.
    public void PlayRound4AndFinish(float[] fullRoundScores, float[] roundScores, float[] cumDsAfter,
                     float total, int overflowRound, float cutFactor,
                     System.Action onComplete)
    {
        Debug.Log("[LeaderScoreUI] PlayRound4AndFinish 호출됨 — 4회차 연출 시작");
        _onComplete = onComplete;
        IsWaitingForRound4Aim = false;

        StartCoroutine(PlayRoundsCoroutine(_pendingUiType, fullRoundScores, roundScores, cumDsAfter,
                                     total, overflowRound, cutFactor, 3, 4));
    }

    // 패널/커튼/캐릭터 프리뷰 등 연출 시작 시 1회 초기화 — Show/ShowPendingRound4 공용.
    void InitPanel(EmployeeData employee, LeaderType type, bool testMode = false)
    {
        _pendingUiType = type;
        _testMode = testMode;
        IsWaitingForRound4Aim = false;

        if (leaderNameText)      leaderNameText.text      = employee.employeeName;
        if (leaderRoleText)      leaderRoleText.text      = employee.RoleToString();
        if (leaderGradeText)     leaderGradeText.text     = employee.GradeToString();
        if (leaderPotentialText) leaderPotentialText.text = employee.PotentialToString();
        EmployeeRole categoryRole = type switch
        {
            LeaderType.Programmer => EmployeeRole.Programmer,
            LeaderType.Artist     => EmployeeRole.Artist,
            _                     => EmployeeRole.Planner
        };
        RoleIconSet.Apply(categoryIcon, categoryIconSet, categoryRole);
        RoleIconSet.Apply(tick90RoleIcon, categoryIconSet, categoryRole);
        RoleIconSet.Apply(tick95RoleIcon, categoryIconSet, categoryRole);
        RoleIconSet.Apply(tick99RoleIcon, categoryIconSet, categoryRole);

        // 90/95/99Image는 1회차부터 계속 보인다 — 보너스 미리보기 금액도 이제 3회차 끝날 때까지
        // 기다리지 않고 그 즉시("+0" 대신) 같이 표시한다. K/enhancementLevel은 회차 진행과 무관하게
        // 세션 시작 시 이미 확정되므로 DevelopmentManager가 1회차 시점에도 정확한 값을 돌려준다.
        if (tick90Mark != null) tick90Mark.gameObject.SetActive(true);
        if (tick95Mark != null) tick95Mark.gameObject.SetActive(true);
        if (tick99Mark != null) tick99Mark.gameObject.SetActive(true);
        RefreshBonusPotentialTexts();

        // 초기화
        if (roundScoreTexts != null)
            foreach (var t in roundScoreTexts) if (t) t.text = "0";
        if (dsText) dsText.text = "0";
        if (dsSlider) { dsSlider.minValue = 0f; dsSlider.maxValue = 100f; }
        SetDsSlider(0f);
        if (totalText) totalText.text = "0";
        _currentDs = 0f;
        _pendingDisplayedTotal = 0f;
        _pendingPrevCumDs = 0f;
        _bonusBaselineTotal = 0f;
        _bonusDisplayedTotal = 0f;
        // 이전 세션이 코루틴 중간에 끊긴 경우를 대비해 UpImage/DownImage도 새 세션 시작 시 확실히 꺼둔다.
        if (upImage != null) upImage.SetActive(false);
        if (downImage != null) downImage.SetActive(false);
        // BackgroundFrame — burst로 갈라진 상태였다면 새 세션 시작 시 원래 sprite로 복원.
        if (backgroundFrame != null)
        {
            if (_backgroundFrameNormalSprite == null) _backgroundFrameNormalSprite = backgroundFrame.sprite;
            backgroundFrame.sprite = _backgroundFrameNormalSprite;
        }
        ResetBurstVisuals(); // burst 플래시/애프터버스트/FailImage 회전/연기 — 새 세션 시작 시 전부 초기화
        HideThresholdTicks(); // 90/95/99 눈금은 3회차 끝날 때 다시 켬 (ShowThresholdTicks)
        SetActiveScoreRow(-1);
        if (stressWarningImage != null)
        {
            // LeaderScorePanel 밖(MenuCanvas 직속 풀스크린)에 있는 오브젝트라 leaderscorePanel처럼 부모를 따라
            // 자동으로 켜지지 않음 — 팀장점수 연출 동안에만 직접 켜고, 확정 시(OnClickConfirm) 직접 끈다.
            stressWarningImage.gameObject.SetActive(true);
            var swc = stressWarningImage.color;
            swc.a = 0f;
            stressWarningImage.color = swc;
        }
        if (lastSpurtImage != null) lastSpurtImage.SetActive(false);
        if (lastSpurtContent != null) lastSpurtContent.anchoredPosition3D = Vector3.zero;

        // confirmBtn은 화면 전체를 덮는 투명 레이캐스트 버튼(anchor 풀스트레치) — interactable=false만으로는
        // 레이캐스트 차단이 안 풀려서(Button.interactable은 클릭 반응만 막지 raycastTarget은 그대로) 4회차
        // 결과가 나오기 전까지는 아예 SetActive(false)로 꺼야 뒤에 있는 다른 버튼(예: 튜토리얼 7-x 강조
        // 대상)이 클릭을 받을 수 있다.
        if (confirmButton) { confirmButton.interactable = false; confirmButton.gameObject.SetActive(false); }
        leaderscorePanel.SetActive(true);
        GameTimeManager.Instance?.StopTime(); // 팀장 점수 연출 동안 시간 정지
        ModalGate.I.Register(this); // 점수 표시 중 다른 모달(상인 Alert 등) 차단
        SpawnCharacterPreview(employee);
        _curtainActive = true;
    }

    // fromRound~toRoundExclusive 구간만 재생. toRoundExclusive<4 면 4회차 대기 상태로 남고
    // (정산/커튼오프 없이 종료), toRoundExclusive>=4 면(또는 도중 오버플로 발생 시) 최종 정산까지 진행한다.
    IEnumerator PlayRoundsCoroutine(LeaderType type,
                              float[] fullRoundScores, float[] roundScores, float[] cumDsAfter,
                              float total, int overflowRound, float cutFactor,
                              int fromRound, int toRoundExclusive, bool announceRound4Wait = true)
    {
        if (fromRound == 0) yield return new WaitForSeconds(0.5f);

        float dur = Mathf.Max(0.01f, rollDuration);
        float displayedTotal = _pendingDisplayedTotal;
        float prevCumDs = _pendingPrevCumDs;
        bool reachedEnd = toRoundExclusive >= 4;

        for (int r = fromRound; r < toRoundExclusive; r++)
        {
            SetActiveScoreRow(r);
            bool isOverflow = (overflowRound == r);
            float targetDs    = cumDsAfter[r];
            float targetRound = isOverflow ? 0f : fullRoundScores[r]; // 오버플로 회차는 0점

            float startTotal = displayedTotal;

            // Phase A — ds(스트레스)만 상승, 회차 점수는 아직 0으로 숨겨둠. 4회차(r==3) 한정 — ds가 90
            // 이상인 구간에 들어서면 상승 속도가 0.5배로 느려진다(슬라이더/dsText 둘 다 같은 값을 쓰므로 자동으로 함께 느려짐).
            SetRoundText(r, 0f);
            bool round4SlowZone = (r == 3);
            float baseRate = (targetDs - prevCumDs) / dur; // 등속 기준 ds/초
            float ds = prevCumDs;
            while (ds < targetDs)
            {
                float rate = (round4SlowZone && ds >= 90f) ? baseRate * 0.5f : baseRate;
                ds = Mathf.Min(targetDs, ds + rate * Time.deltaTime);
                if (dsText) dsText.text = Mathf.RoundToInt(ds).ToString();
                SetDsSlider(ds);
                _currentDs = ds;
                yield return null;
            }
            if (dsText) dsText.text = Mathf.RoundToInt(targetDs).ToString();
            SetDsSlider(targetDs);
            _currentDs = targetDs;
            prevCumDs = targetDs;

            if (isOverflow)
            {
                // 누적 ds 100 초과(burst): 총점 위치(categoryIcon)에서 빨아들였던 것과 반대로 아이콘을
                // 뱉어내는 연출(SpitBurstCoroutine)과 애프터버스트(FailImage 회전/SmokeImage 연기, 화면
                // 플래시 포함)는 burst 즉시 동시에 시작하되, 점수 차감(ApplyCutCoroutine)만큼은 burst
                // 시점으로부터 0.5초 뒤에 시작한다(요청 사양).
                float lost = 0f;
                for (int k = 0; k < r; k++) lost += fullRoundScores[k] - roundScores[k];
                StartCoroutine(SpitBurstCoroutine(Mathf.Max(0, Mathf.RoundToInt(lost))));
                TriggerBurstVisuals();
                yield return new WaitForSeconds(0.5f);

                yield return StartCoroutine(ApplyCutCoroutine(fullRoundScores, roundScores, r));
                reachedEnd = true; // 오버플로는 회차와 무관하게 항상 그 자리에서 종료
                break;
            }

            // 보너스(90/95/99 임계선)는 여기서 같이 안 얹는다 — 4회차까지는 4회차 점수만 올라가고,
            // 보너스는 이후 RefreshBonusActualTexts → BonusFlyCoroutine이 각 임계선 아이콘을 흡수하는
            // 타이밍에 맞춰서 별도로 총점에 더해진다(유저 확정 사양).
            //
            // Phase B+C — 회차 점수만큼 아이콘이 팝콘처럼 펑 터진 뒤 categoryIcon으로 빨려들어가는 동안
            // 총점 + 회차 점수가 동시에 상승.
            yield return StartCoroutine(PopAndFlyCoroutine(Mathf.Max(0, Mathf.RoundToInt(targetRound)), r, startTotal, targetRound));
            displayedTotal = startTotal + targetRound;

            if (r < toRoundExclusive - 1)
                yield return new WaitForSeconds(roundGap);
        }

        _pendingDisplayedTotal = displayedTotal;
        _pendingPrevCumDs = prevCumDs;
        SetActiveScoreRow(-1); // 회차 연출 종료 — 다음 대기든 최종 정산이든 더 이상 "차례"인 회차 없음

        if (!reachedEnd)
        {
            // 1~3회차 연출까지 다 끝남 — 이제부터 4회차 조준 선택 대기 (커튼/스트레스 경고는 계속 돌아감).
            // announceRound4Wait=false(튜토리얼 1회차 단독 재생)면 아직 3회차 전이므로 이 상태로 안 넘어간다.
            if (announceRound4Wait)
            {
                // 라스트 스퍼트 연출(좌→중앙 fly-in, 대기, 중앙→우 fly-out) — 이 동안은 IsWaitingForRound4Aim
                // 이 아직 false라 LeaderScoreAimButtons(폴링)가 aimButtonsRoot 를 켜지 않는다. 끝나야 조준 버튼 등장.
                if (lastSpurtImage != null)
                {
                    lastSpurtImage.SetActive(true);
                    yield return StartCoroutine(PlayLastSpurtCoroutine());
                    lastSpurtImage.SetActive(false);
                }
                IsWaitingForRound4Aim = true;
                ShowThresholdTicks();
            }
            yield break;
        }

        // 90/95/99 눈금은 ShowThresholdTicks 로 한 번 뜨면(3회차 끝) 그 뒤로는 절대 안 숨긴다 —
        // 4회차 진행/오버플로(burst) 등 무엇이 일어나도 이번 팀장점수 연출이 끝날 때까지 계속 유지.

        // 총점(보너스 제외, 4회차까지만 — 오버플로면 차감 후 값)을 baseline으로 고정. 여기서부터는
        // 보너스가 아이콘 흡입 타이밍에 맞춰 별도로 더해진다(요청: "4회차까지는 4회차 점수만, 보너스는
        // 흡입되면서"). total 파라미터가 이미 (전 회차 점수 합 + 보너스 총합)이므로 역산으로 구한다.
        float bonusTotalNow = DevelopmentManager.Instance != null ? DevelopmentManager.Instance.GetLeaderBonusTotal() : 0f;
        _bonusBaselineTotal = total - bonusTotalNow;
        _bonusDisplayedTotal = 0f;
        if (totalText) totalText.text = Mathf.RoundToInt(_bonusBaselineTotal).ToString();

        // 4회차까지 끝났으면 미리보기(범위) 대신 실제 획득액으로 교체 — 동시에 보너스 아이콘이 각 임계선
        // 위치에서 TopIcon으로 흡입되며 총점을 baseline에서부터 하나씩 밀어올린다(BonusFlyCoroutine).
        // (훈수쟁이 보너스는 여기서 처리하지 않음 — 팀장점수 연출/확정과 분리해 ContinueAfterLeaderScore 에서
        //  AlertUI3 로 별도 안내 후 적용된다.)
        // 이 흡입이 전부 끝날 때까지 기다린 뒤에야 아래로 진행 — 안 그러면 총점이 한창 올라가는 도중에
        // confirmBtn이 눌려서 결과가 다 안 보인 채로 패널이 넘어가버린다.
        var bonusCoroutines = RefreshBonusActualTexts();
        foreach (var co in bonusCoroutines) yield return co;

        float pl = type == LeaderType.Planner   ? total : 0f;
        float dv = type == LeaderType.Programmer ? total : 0f;
        float ar = type == LeaderType.Artist     ? total : 0f;
        // 패널 반영은 confirm 시점에 한 번에 (OnClickConfirm 에서 AddValuesInstant)
        _applyPlanning = pl;
        _applyDevelop  = dv;
        _applyArt      = ar;

        _curtainActive = false;
        ResetCurtainAlpha();
        StopWorkingAnimations();
        StopBurstPulse(); // 4회차(정상/오버플로 모두) 연출 종료 — burstText 반복 정지

        yield return new WaitForSeconds(0.5f);
        // 4회차(또는 오버플로) 결과가 화면에 다 나온 지금에야 confirmBtn을 켠다 — 위 InitPanel 주석 참고.
        if (confirmButton) { confirmButton.gameObject.SetActive(true); confirmButton.interactable = true; }
        if (burstText != null) burstText.gameObject.SetActive(false); // confirmBtn과 함께 꺼짐 — dimImage 등 나머지 결과 표시는 유지
        OnRoundsVisualComplete?.Invoke();
    }

    // 누적 ds 100 초과 시: 전 회차(0..overflowRound-1) 점수를 full → cut 값으로 내림
    IEnumerator ApplyCutCoroutine(float[] fullRoundScores, float[] roundScores, int overflowRound)
    {
        if (overflowRound <= 0) yield break;

        Image downImg = downImage != null ? downImage.GetComponent<Image>() : null;
        if (downImage != null) downImage.SetActive(true); // burst로 전 회차 점수가 깎이는 동안만 활성화
        Color downBaseColor = downImg != null ? downImg.color : Color.white;
        if (backgroundFrame != null && backgroundFrameCrackedSprite != null) backgroundFrame.sprite = backgroundFrameCrackedSprite;

        float dur = 0.5f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float sum = 0f;
            for (int k = 0; k < overflowRound; k++)
            {
                float v = Mathf.Lerp(fullRoundScores[k], roundScores[k], t);
                SetRoundText(k, v);
                sum += v;
            }
            if (totalText) totalText.text = Mathf.RoundToInt(sum).ToString();

            // downImage가 켜져 있는 이 구간(dur) 동안 alpha로 downBlinkCount번 깜빡인다.
            if (downImg != null)
            {
                float wave = (1f - Mathf.Cos(t * downBlinkCount * 2f * Mathf.PI)) * 0.5f; // 0→1→0을 downBlinkCount번 반복
                var c = downBaseColor;
                c.a = downBaseColor.a * wave;
                downImg.color = c;
            }

            yield return null;
        }

        for (int k = 0; k < overflowRound; k++)
            SetRoundText(k, roundScores[k]);
        if (downImg != null) downImg.color = downBaseColor;
        if (downImage != null) downImage.SetActive(false);
    }

    // burst(스트레스 100 오버플로) 순간 1회 호출 — 화면 플래시 + 애프터버스트(FailImage 회전 시작 +
    // SmokeImage 연기 퍼짐)를 동시에 재생한다. AfterBurstImage는 이후 새 세션이 시작될 때까지 계속 표시.
    // 반환하는 Coroutine은 화면 플래시(LeaderScoreBurstImage)가 완전히 꺼지는 시점 — 호출자가 이걸
    // yield해서 "플래시가 다 꺼진 뒤에" 점수 차감(ApplyCutCoroutine)을 시작하도록 순서를 맞춘다.
    Coroutine TriggerBurstVisuals()
    {
        Coroutine flash = StartCoroutine(BurstFlashCoroutine());

        if (afterBurstImage != null) afterBurstImage.SetActive(true);
        StartFailSpin();

        if (smokeImages != null)
            foreach (var img in smokeImages)
                if (img != null) StartCoroutine(SmokePuffCoroutine(img));

        return flash;
    }

    // LeaderScoreBurstImage(풀스크린) alpha를 0→255로 빠르게 튀웠다가 다시 0으로 서서히 되돌리며 사라짐.
    IEnumerator BurstFlashCoroutine()
    {
        if (burstFlashImage == null) yield break;

        // 안전망 — lastSpurtImage는 1~3회차와 4회차 사이(PlayLastSpurtCoroutine)에만 잠깐 켜졌다 꺼져야
        // 하는데, 그 코루틴이 중간에 끊기면(재접속 등) 꺼지지 않은 채 남을 수 있다. 이 시점(burst 발동)엔
        // 이미 4회차까지 진행된 뒤라 lastSpurtImage가 켜져있을 이유가 없으므로 여기서 확실히 꺼둔다.
        if (lastSpurtImage != null) lastSpurtImage.SetActive(false);

        burstFlashImage.gameObject.SetActive(true);
        var c = burstFlashImage.color;

        float inDur = Mathf.Max(0.01f, burstFlashInDuration);
        float elapsed = 0f;
        while (elapsed < inDur)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Clamp01(elapsed / inDur);
            burstFlashImage.color = c;
            yield return null;
        }
        c.a = 1f;
        burstFlashImage.color = c;

        float outDur = Mathf.Max(0.01f, burstFlashOutDuration);
        elapsed = 0f;
        while (elapsed < outDur)
        {
            elapsed += Time.deltaTime;
            c.a = 1f - Mathf.Clamp01(elapsed / outDur);
            burstFlashImage.color = c;
            yield return null;
        }
        c.a = 0f;
        burstFlashImage.color = c;
        burstFlashImage.gameObject.SetActive(false);
    }

    // failImages 전부를 failSpinPeriod초에 한 바퀴씩 무한 반복 회전시킨다(DOTween 루프 — 코루틴/Update 불필요).
    void StartFailSpin()
    {
        if (failImages == null) return;
        float period = Mathf.Max(0.01f, failSpinPeriod);
        foreach (var rt in failImages)
        {
            if (rt == null) continue;
            rt.DOKill();
            rt.DORotate(new Vector3(0f, 0f, -360f), period, RotateMode.FastBeyond360)
                .SetLoops(-1, LoopType.Restart)
                .SetEase(Ease.Linear)
                .SetUpdate(true);
        }
    }

    void StopFailSpin()
    {
        if (failImages == null) return;
        foreach (var rt in failImages)
            if (rt != null) rt.DOKill();
    }

    // 프레임이 1장뿐인 SmokeImage를 스케일업+페이드로 "연기가 확 퍼졌다가 흐릿하게 가라앉는" 것처럼
    // 흉내낸다 — 초반 30%는 알파가 0→1로 확 밝아지며 커지고, 나머지 70%는 계속 커지면서 알파가
    // smokeRestAlpha까지 가라앉아 옅은 잔여 연기로 남는다. 위로 smokeRiseDistance만큼 서서히 떠오른다.
    IEnumerator SmokePuffCoroutine(Image img)
    {
        RectTransform rt = img.rectTransform;

        if (!_smokeBasePos.TryGetValue(img, out Vector2 basePos))
        {
            basePos = rt.anchoredPosition;
            _smokeBasePos[img] = basePos;
        }

        rt.DOKill();
        rt.localScale = Vector3.one * smokeStartScale;
        rt.anchoredPosition = basePos;
        Color c = img.color;
        c.a = 0f;
        img.color = c;

        float dur = Mathf.Max(0.01f, smokeDuration);
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float scaleT = 1f - (1f - t) * (1f - t); // ease-out

            rt.localScale = Vector3.one * Mathf.Lerp(smokeStartScale, smokeEndScale, scaleT);
            rt.anchoredPosition = basePos + Vector2.up * (smokeRiseDistance * t);

            c.a = t < 0.3f ? Mathf.Lerp(0f, 1f, t / 0.3f) : Mathf.Lerp(1f, smokeRestAlpha, (t - 0.3f) / 0.7f);
            img.color = c;

            yield return null;
        }

        rt.localScale = Vector3.one * smokeEndScale;
        rt.anchoredPosition = basePos + Vector2.up * smokeRiseDistance;
        c.a = smokeRestAlpha;
        img.color = c;
    }

    // 새 세션 시작(InitPanel) 시 burst 관련 연출을 전부 원상복구.
    void ResetBurstVisuals()
    {
        if (burstFlashImage != null)
        {
            var c = burstFlashImage.color;
            c.a = 0f;
            burstFlashImage.color = c;
            burstFlashImage.gameObject.SetActive(false);
        }

        if (afterBurstImage != null) afterBurstImage.SetActive(false);
        StopFailSpin();

        if (smokeImages != null)
        {
            foreach (var img in smokeImages)
            {
                if (img == null) continue;
                img.rectTransform.DOKill();
                img.rectTransform.localScale = Vector3.one;
                if (_smokeBasePos.TryGetValue(img, out Vector2 basePos))
                    img.rectTransform.anchoredPosition = basePos;
                var c = img.color;
                c.a = 0f;
                img.color = c;
            }
        }
    }

    // dsSlider(0~100)를 실제 채움 비율로 반영한다. UnityEngine.UI.Slider의 기본 채움은 값에 선형 비례하지만,
    // 90/95/99 눈금(tick90~99Mark)이 균등 간격이 아니라 위로 갈수록 확 벌어지는 계단식 배치라서(위치조정
    // 참고) 슬라이더가 값을 세팅하며 자동으로 덮어쓴 fillRect.anchorMax.y를 MapDsToFillFraction 결과로
    // 다시 덮어써야 눈금과 실제 채워지는 높이가 일치한다.
    void SetDsSlider(float ds)
    {
        if (dsSlider == null) return;
        ds = Mathf.Clamp(ds, 0f, 100f);
        dsSlider.value = ds;
        if (dsSlider.fillRect != null)
        {
            var anchorMax = dsSlider.fillRect.anchorMax;
            anchorMax.y = MapDsToFillFraction(ds);
            dsSlider.fillRect.anchorMax = anchorMax;
        }
        UpdateFillSprite(ds);
    }

    static readonly Color DsTextColorLow = new Color32(0x2C, 0xE2, 0x93, 0xFF);
    static readonly Color DsTextColorMid = new Color32(0xFF, 0xB9, 0x5E, 0xFF);
    static readonly Color DsTextColorHigh = new Color32(0xE6, 0x33, 0x56, 0xFF);

    // Fill 스프라이트를 스트레스 구간(~69/70~89/90~)에 맞게 교체. 스프라이트 3종은 인스펙터에서 직접 할당.
    // dsText 색상도 같은 구간 기준으로 함께 바뀐다(low #2CE293 / mid #FFB95E / high #E63356).
    void UpdateFillSprite(float ds)
    {
        Sprite target = ds >= 90f ? fillSpriteHigh : (ds >= 70f ? fillSpriteMid : fillSpriteLow);
        Color textColor = ds >= 90f ? DsTextColorHigh : (ds >= 70f ? DsTextColorMid : DsTextColorLow);

        if (fillImage != null && target != null && fillImage.sprite != target) fillImage.sprite = target;
        if (dsText != null) dsText.color = textColor;
    }

    // ds(0~100) → 채움 비율(0~1). dsSlider 자체 높이 기준 432 안에서 0→0, 90→269, 95→336, 99→403, 100→432
    // 위치에 해당하는 4구간 꺾은선 — 90 밑에서는 완만하게, 90 이상부터는 구간별로 기울기가 점점 가팔라지며
    // 급격히 오른다. 90/95/99Image의 localPosition.y(offset -216, dsSlider 자체 프레임 기준)와 반드시
    // 같은 기준값(432)을 공유해야 눈금과 실제 채움 위치가 어긋나지 않는다.
    static float MapDsToFillFraction(float ds)
    {
        const float total = 432f;
        float h;
        if (ds <= 90f)      h = Mathf.Lerp(0f,   269f, ds / 90f);
        else if (ds <= 95f) h = Mathf.Lerp(269f, 336f, (ds - 90f) / 5f);
        else if (ds <= 99f) h = Mathf.Lerp(336f, 403f, (ds - 95f) / 4f);
        else                h = Mathf.Lerp(403f, 432f, (ds - 99f) / 1f);
        return h / total;
    }

    void SetRoundText(int index, float value)
    {
        if (roundScoreTexts != null && index >= 0 && index < roundScoreTexts.Length && roundScoreTexts[index])
            roundScoreTexts[index].text = Mathf.RoundToInt(value).ToString();
    }

    // scoreRows[i]의 원래 높이/폰트크기를 최초 1회 캐싱해두고, activeIndex번째 행만 높이가
    // scoreRowActiveHeightGrow만큼 커지고 자식 텍스트(numText/scoreText) 폰트크기를 키운다.
    // (Y축 이동 방식은 ScorePanel의 HorizontalLayoutGroup이 매 리빌드마다 위치를 되돌려버려서
    // 높이 확대 방식으로 교체함 — sizeDelta는 childControlHeight=false라 레이아웃이 안 건드림.)
    // 나머지 행(및 activeIndex=-1이면 전부)은 캐싱된 원래 값으로 되돌린다.
    void SetActiveScoreRow(int activeIndex)
    {
        if (scoreRows == null) return;
        for (int i = 0; i < scoreRows.Length; i++)
        {
            var row = scoreRows[i];
            if (row == null) continue;

            bool active = i == activeIndex;

            if (!_scoreRowBaseSizeDelta.TryGetValue(row, out Vector2 baseSizeDelta))
            {
                baseSizeDelta = row.sizeDelta;
                _scoreRowBaseSizeDelta[row] = baseSizeDelta;
            }
            row.sizeDelta = new Vector2(baseSizeDelta.x, baseSizeDelta.y + (active ? scoreRowActiveHeightGrow : 0f));

            foreach (var t in row.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (!_scoreRowBaseFontSize.TryGetValue(t, out float baseSize))
                {
                    baseSize = t.fontSize;
                    _scoreRowBaseFontSize[t] = baseSize;
                }
                t.fontSize = active ? scoreRowActiveFontSize : baseSize;
            }
        }
    }

    // lastSpurtContent를 화면 왼쪽 밖에서 중앙(원래 위치)까지 빠르게 날아들어오게(ease-out) 한 뒤,
    // lastSpurtHoldDuration만큼 대기했다가 다시 오른쪽 밖으로 빠르게 날아나가게(ease-in) 한다.
    IEnumerator PlayLastSpurtCoroutine()
    {
        if (lastSpurtContent == null) yield break;

        Vector3 restPos  = lastSpurtContent.anchoredPosition3D;
        Vector3 fromLeft = restPos + new Vector3(-lastSpurtFlyDistance, 0f, 0f);
        Vector3 toRight  = restPos + new Vector3(lastSpurtFlyDistance, 0f, 0f);
        float dur = Mathf.Max(0.01f, lastSpurtFlyDuration);

        // 좌 → 중앙 (ease-out: 빠르게 튀어나왔다가 감속하며 안착)
        lastSpurtContent.anchoredPosition3D = fromLeft;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            lastSpurtContent.anchoredPosition3D = Vector3.LerpUnclamped(fromLeft, restPos, eased);
            yield return null;
        }
        lastSpurtContent.anchoredPosition3D = restPos;

        yield return new WaitForSeconds(lastSpurtHoldDuration);

        // 중앙 → 우 (ease-in: 느리게 시작해 빠르게 화면 밖으로 빠져나감)
        elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float eased = t * t * t;
            lastSpurtContent.anchoredPosition3D = Vector3.LerpUnclamped(restPos, toRight, eased);
            yield return null;
        }
        lastSpurtContent.anchoredPosition3D = toRight;
    }

    // count개의 iconPrefab을 popcornPoint에서 팝콘처럼 펑 터뜨린 뒤, categoryIcon 위치로 빨려들어가게 하면서
    // 그와 동시에 총점(startTotal→startTotal+targetRound)과 해당 회차 점수(0→targetRound)를 함께 올린다.
    // 보너스(90/95/99)는 여기서 같이 안 올린다 — RefreshBonusActualTexts → BonusFlyCoroutine이 각 임계선
    // 아이콘 흡입 타이밍에 맞춰 별도로 더한다.
    IEnumerator PopAndFlyCoroutine(int count, int roundIndex, float startTotal, float targetRound)
    {
        if (count <= 0 || iconPrefab == null || popcornPoint == null)
        {
            // 연출 리소스가 없으면 텍스트만 즉시 반영
            SetRoundText(roundIndex, targetRound);
            if (totalText) totalText.text = Mathf.RoundToInt(startTotal + targetRound).ToString();
            yield break;
        }

        var icons = new System.Collections.Generic.List<RectTransform>(count);
        var scatterOffsets = BuildStratifiedScatterOffsets(count, popScatterRangeX);

        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(iconPrefab, popcornPoint);
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) continue;
            rt.position = popcornPoint.position;
            rt.localScale = Vector3.zero;

            // 자식 iconImage를 현재 진행 중인 직군(categoryIcon)과 같은 아이콘으로 교체
            var iconImage = go.transform.Find("iconImage");
            var iconImg = iconImage != null ? iconImage.GetComponent<Image>() : null;
            if (iconImg != null && categoryIcon != null) iconImg.sprite = categoryIcon.sprite;

            icons.Add(rt);
            StartCoroutine(PopPunch(rt, rt.localPosition, scatterOffsets[i]));
        }

        yield return new WaitForSeconds(popDuration + 0.05f); // 다 터질 때까지 대기

        var startPositions = new System.Collections.Generic.List<Vector3>(icons.Count);
        foreach (var rt in icons) startPositions.Add(rt != null ? rt.position : Vector3.zero);

        Vector3 targetPos = categoryIcon != null ? categoryIcon.transform.position : popcornPoint.position;

        // 아이콘 1개당 흡입 시간(perIconDur)은 그대로 두고, 각 아이콘의 시작 시점을 순차적으로 늦춰서
        // "한 번에 훅" 대신 "톡톡톡" 순차적으로 빨려들어가게 한다. 간격을 고정값이 아니라
        // suckStaggerWindow(목표 총 시간) / 개수로 계산해서, 개수가 많아져도 전체 재생 시간이
        // perIconDur + suckStaggerWindow 를 거의 넘지 않도록(개수→∞일수록 그 값에 근접) 제한한다.
        float perIconDur = Mathf.Max(0.01f, suckDuration);
        float staggerWindow = Mathf.Max(0f, suckStaggerWindow);
        float stagger = icons.Count > 0 ? staggerWindow / icons.Count : 0f;
        float totalDur = perIconDur + stagger * Mathf.Max(0, icons.Count - 1);

        if (upImage != null) upImage.SetActive(true); // 점수가 흡수되는(빨려들어가는) 동안만 활성화

        // topIcon/totalText 흡입 펄스 — [기존] 첫 아이콘이 growTriggerRatio에 도달하는 순간 GrowPulseIfNeeded()
        // 1회 호출해 한번에 확 키웠던 방식은 주석 처리. 지금은 아이콘 각자가 자기 진행률이 growTriggerRatio
        // (기본 0.8 — RoleIcon 도착 직전)에 도달할 때마다 punchStrength/아이콘개수 만큼씩 조금씩 누적해서 커진다.
        // bool grown = false;
        // bool growTriggered = false;
        var pulseSession = icons.Count > 0 ? BeginPulseSession() : null;
        float pulseStepAmount = punchStrength / Mathf.Max(1, icons.Count);
        var iconStepDone = new bool[icons.Count];

        float elapsed = 0f;
        while (elapsed < totalDur)
        {
            elapsed += Time.deltaTime;

            for (int i = 0; i < icons.Count; i++)
            {
                if (icons[i] == null) continue;
                float localElapsed = Mathf.Clamp(elapsed - i * stagger, 0f, perIconDur);
                float it = localElapsed / perIconDur;
                float eased = it * it * it * it; // 강한 ease-in — 초반엔 거의 안 움직이다 순식간에 확 빨려들어감
                icons[i].position = Vector3.Lerp(startPositions[i], targetPos, eased);
                icons[i].localScale = Vector3.one * (1f - eased * 0.7f);

                // if (!growTriggered && i == 0 && it >= growTriggerRatio) { growTriggered = true; grown = true; GrowPulseIfNeeded(); } // [기존, 주석 처리]
                if (!iconStepDone[i] && it >= growTriggerRatio)
                {
                    iconStepDone[i] = true;
                    StepPulseSession(pulseSession, pulseStepAmount);
                }
            }

            float t = Mathf.Clamp01(elapsed / totalDur);
            float rs = Mathf.Lerp(0f, targetRound, t);
            SetRoundText(roundIndex, rs);
            if (totalText) totalText.text = Mathf.RoundToInt(startTotal + rs).ToString();

            yield return null;
        }

        // if (grown) ShrinkPulseIfDone(); // [기존, 주석 처리]
        EndPulseSession(pulseSession);

        SetRoundText(roundIndex, targetRound);
        if (totalText) totalText.text = Mathf.RoundToInt(startTotal + targetRound).ToString();
        if (upImage != null) upImage.SetActive(false);

        foreach (var rt in icons)
            if (rt != null) Destroy(rt.gameObject);
    }

    // ════════ [기존 방식 — 주석 처리] 흡입 시작/도착 시점에 1→(1+punchStrength)로 한번에 확 커졌다가,
    // 그 그룹의 아이콘이 전부 끝나야만(_pulseActiveCount가 0으로 돌아와야만) 원래 크기로 줄어들었다.
    // 아이콘 개수가 많을수록 "커진 채로 오래 유지"되는 느낌이라, 아래 incremental 방식(아이콘이 하나씩
    // 도착할 때마다 punchStrength/개수만큼 조금씩 누적해서 커짐)으로 교체.
    // void GrowPulseIfNeeded()
    // {
    //     if (_pulseActiveCount == 0)
    //     {
    //         GrowHold(topIcon);
    //         GrowHold(totalText != null ? totalText.transform as RectTransform : null);
    //     }
    //     _pulseActiveCount++;
    // }
    //
    // void ShrinkPulseIfDone()
    // {
    //     _pulseActiveCount = Mathf.Max(0, _pulseActiveCount - 1);
    //     if (_pulseActiveCount == 0)
    //     {
    //         ShrinkHold(topIcon);
    //         ShrinkHold(totalText != null ? totalText.transform as RectTransform : null);
    //     }
    // }
    //
    // void GrowHold(RectTransform rt)
    // {
    //     if (rt == null) return;
    //     rt.DOKill();
    //     rt.DOScale(Vector3.one * (1f + punchStrength), punchDuration).SetEase(Ease.OutBack).SetUpdate(true);
    // }
    //
    // void ShrinkHold(RectTransform rt)
    // {
    //     if (rt == null) return;
    //     rt.DOKill();
    //     rt.DOScale(Vector3.one, punchDuration).SetEase(Ease.OutQuad).SetUpdate(true);
    // }

    // ════════ 새 방식: 아이콘이 하나씩 도착할 때마다 punchStrength를 그 그룹의 아이콘 개수로 나눈 만큼씩
    // 조금씩 누적해서 커진다(한번에 확 커지지 않음). 회차 팝콘(PopAndFlyCoroutine)과 보너스 흡입
    // (BonusFlyCoroutine, 90/95/99 동시 진행 가능)이 동시에 돌 수 있어, 그룹마다 PulseSession을 하나씩
    // 발급받아 자기 누적치를 따로 들고 있고, 실제 적용 스케일은 모든 세션 중 가장 큰 누적치(MAX) 기준 —
    // 여러 그룹이 겹쳐도 값이 서로 더해져서 과하게 커지지 않는다. 세션이 하나도 안 남으면 1로 복귀.
    class PulseSession { public float accum; }
    readonly System.Collections.Generic.List<PulseSession> _pulseSessions = new();

    PulseSession BeginPulseSession()
    {
        var s = new PulseSession();
        _pulseSessions.Add(s);
        return s;
    }

    void StepPulseSession(PulseSession session, float stepAmount)
    {
        if (session == null) return;
        session.accum += stepAmount;
        ApplyPulseScale();
    }

    void EndPulseSession(PulseSession session)
    {
        if (session == null) return;
        _pulseSessions.Remove(session);
        ApplyPulseScale();
    }

    void ApplyPulseScale()
    {
        float maxAccum = 0f;
        foreach (var s in _pulseSessions)
            if (s.accum > maxAccum) maxAccum = s.accum;
        float target = 1f + maxAccum;
        AnimatePulseScale(topIcon, target);
        AnimatePulseScale(totalText != null ? totalText.transform as RectTransform : null, target);
    }

    void AnimatePulseScale(RectTransform rt, float target)
    {
        if (rt == null) return;
        rt.DOKill();
        rt.DOScale(Vector3.one * target, punchDuration).SetEase(Ease.OutBack).SetUpdate(true);
    }

    // count개를 popScatterRangeX 범위 안에서 "골고루" 흩어지도록 미리 배정하는 오프셋 목록.
    // 완전 랜덤(Random.Range)이면 운 나쁘면 10개가 전부 한쪽으로 쏠릴 수 있어서, 전체 범위를
    // count개 구간으로 균등 분할(예: 10개면 절반은 왼쪽 절반은 오른쪽)해 구간마다 하나씩 배정하고,
    // 구간 내부에서만 랜덤(자연스러운 흔들림)+구간↔아이콘 매칭은 셔플(항상 왼쪽부터 순서대로 터지는
    // 부자연스러움 방지)한다.
    static System.Collections.Generic.List<float> BuildStratifiedScatterOffsets(int count, float range)
    {
        var offsets = new System.Collections.Generic.List<float>(count);
        if (count <= 0) return offsets;

        float binWidth = (range * 2f) / count;
        for (int i = 0; i < count; i++)
        {
            float binStart = -range + i * binWidth;
            offsets.Add(binStart + Random.Range(0f, binWidth));
        }

        // Fisher-Yates 셔플 — 구간 순서와 생성 순서(아이콘 index)를 무작위로 매칭
        for (int i = offsets.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (offsets[i], offsets[j]) = (offsets[j], offsets[i]);
        }
        return offsets;
    }

    // count개를 아래(-90°) 방향을 중심으로 arcDegrees 폭의 부채꼴 안에서 골고루 흩어지도록 배정하는
    // 2D 오프셋 목록(보너스 아이콘이 roleIcon 아래로 확 퍼지듯 미끄러지는 연출용). BuildStratifiedScatterOffsets
    // (X축 1차원)와 같은 원리 — arcDegrees를 count-1개 구간으로 균등 분할해 구간마다 하나씩 각도를
    // 배정하고, 구간 내부 각도와 반지름만 랜덤하게 흔든다. count가 1이면 아래 방향 기준으로 살짝만 흔든다.
    static System.Collections.Generic.List<Vector2> BuildDownwardFanScatterOffsets(int count, float radius, float arcDegrees)
    {
        var offsets = new System.Collections.Generic.List<Vector2>(count);
        if (count <= 0) return offsets;

        const float DownAngle = -90f; // Unity 좌표계 기준 정확히 아래 방향(0=오른쪽, 90=위쪽)

        if (count == 1)
        {
            float angle1 = DownAngle + Random.Range(-arcDegrees * 0.5f, arcDegrees * 0.5f);
            float rad1 = angle1 * Mathf.Deg2Rad;
            offsets.Add(new Vector2(Mathf.Cos(rad1), Mathf.Sin(rad1)) * radius * Random.Range(0.6f, 1f));
            return offsets;
        }

        float startAngle = DownAngle - arcDegrees * 0.5f;
        float angleStep = arcDegrees / (count - 1);
        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + angleStep * i + Random.Range(-angleStep * 0.25f, angleStep * 0.25f);
            float r = radius * Random.Range(0.7f, 1f);
            float rad = angle * Mathf.Deg2Rad;
            offsets.Add(new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * r);
        }

        // Fisher-Yates 셔플 — 각도 구간 순서와 생성 순서(아이콘 index)를 무작위로 매칭
        for (int i = offsets.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (offsets[i], offsets[j]) = (offsets[j], offsets[i]);
        }
        return offsets;
    }

    // 아이콘 하나가 popFlightDuration 동안 포물선을 그리며 (X는 scatterOffset 만큼 이동한 착지 지점으로,
    // Y는 위로 튀었다가 내려오는 곡선으로) 빠르게 터져나가 popFloorY 바닥에 착지한 뒤,
    // 남은 시간(popDuration - popFlightDuration)은 그 자리에 가만히 있는다. 스케일은 0 → overshoot → 1.
    IEnumerator PopPunch(RectTransform rt, Vector3 startLocalPos, float scatterOffset)
    {
        float targetX = startLocalPos.x + scatterOffset;
        Vector3 targetLocalPos = new Vector3(targetX, popFloorY, startLocalPos.z);

        float flightDur = Mathf.Clamp(popFlightDuration, 0.01f, Mathf.Max(0.01f, popDuration));
        float elapsed = 0f;
        while (elapsed < flightDur && rt != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flightDur);
            float scaleT = t < 0.6f ? Mathf.Lerp(0f, 1.15f, t / 0.6f) : Mathf.Lerp(1.15f, 1f, (t - 0.6f) / 0.4f);
            rt.localScale = Vector3.one * scaleT;

            float x = Mathf.Lerp(startLocalPos.x, targetLocalPos.x, t);
            float yLinear = Mathf.Lerp(startLocalPos.y, targetLocalPos.y, t);
            float y = yLinear + popArcHeight * 4f * t * (1f - t); // 포물선 — 튀어올랐다가 바닥에 착지
            rt.localPosition = new Vector3(x, y, targetLocalPos.z);

            yield return null;
        }

        if (rt != null)
        {
            rt.localScale = Vector3.one;
            rt.localPosition = targetLocalPos; // 착지 후 남은 시간은 그대로 바닥에 위치
        }
    }

    // 스트레스 100 오버플로(burst) 시: PopAndFlyCoroutine의 "빨려들어가기(총점 위치로 ease-in)"를 정반대로
    // 재생한다 — count개의 아이콘이 총점 위치(categoryIcon)에서 시작해 팝콘 착지 구역 쪽으로 ease-out
    // 흩어지며 날아가다 축소되어 사라짐. 잃는 점수만큼(count) 뱉어내는 것으로 보이게 하는 연출.
    IEnumerator SpitBurstCoroutine(int count)
    {
        if (count <= 0 || iconPrefab == null || categoryIcon == null) yield break;

        Vector3 originPos = categoryIcon.transform.position;
        Vector3 basePos = popcornPoint != null ? popcornPoint.position : originPos;
        float worldScale = categoryIcon.transform.lossyScale.x; // 픽셀 단위 오프셋 → 월드 좌표 변환용 캔버스 배율

        var icons   = new System.Collections.Generic.List<RectTransform>(count);
        var targets = new System.Collections.Generic.List<Vector3>(count);
        var scatterOffsets = BuildStratifiedScatterOffsets(count, popScatterRangeX);

        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(iconPrefab, popcornPoint != null ? popcornPoint : transform);
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) { Destroy(go); continue; }
            rt.position = originPos;
            rt.localScale = Vector3.one;

            var iconImage = go.transform.Find("iconImage");
            var iconImg = iconImage != null ? iconImage.GetComponent<Image>() : null;
            if (iconImg != null) iconImg.sprite = categoryIcon.sprite;

            icons.Add(rt);
            float oy = Random.Range(0f, popArcHeight);
            targets.Add(basePos + new Vector3(scatterOffsets[i], oy, 0f) * worldScale);
        }

        float dur = Mathf.Max(0.01f, burstSpitDuration);
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float eased = 1f - Mathf.Pow(1f - t, 4f); // 강한 ease-out — 빨려들어가기(ease-in)의 반대: 터지듯 빠르게 뱉어나갔다 서서히 멈춤

            for (int i = 0; i < icons.Count; i++)
            {
                if (icons[i] == null) continue;
                icons[i].position    = Vector3.Lerp(originPos, targets[i], eased);
                icons[i].localScale  = Vector3.one * (1f - eased); // 날아가며 축소, 도착 시점 소멸
            }
            yield return null;
        }

        foreach (var rt in icons)
            if (rt != null) Destroy(rt.gameObject);
    }

    void LateUpdate()
    {
        // 실제 캐릭터 프리팹(SpriteRenderer)이 Animator로 바꾸는 스프라이트를 그대로 UI Image에 미러링.
        if (_previewSpriteRenderer != null && characterImage != null)
            characterImage.sprite = _previewSpriteRenderer.sprite;

        UpdateStressWarning();
        UpdateCurtainBlink();
    }

    // 팀장점수 연출이 진행되는 동안(Show ~ 점수 확정) LeftCurtain/RightCurtain의 alpha를
    // 206~255 사이에서 sin파로 부드럽게 왕복시킨다. curtainBlinkPeriod초에 한 바퀴(206→255→206).
    void UpdateCurtainBlink()
    {
        if (!_curtainActive) return;

        float period = Mathf.Max(0.01f, curtainBlinkPeriod);
        float wave = (Mathf.Sin(Time.time * (2f * Mathf.PI / period)) + 1f) * 0.5f; // 0~1
        float a = Mathf.Lerp(206f / 255f, 1f, wave);

        SetCurtainAlpha(leftCurtain, a);
        SetCurtainAlpha(rightCurtain, a);
    }

    void ResetCurtainAlpha()
    {
        SetCurtainAlpha(leftCurtain, 1f);
        SetCurtainAlpha(rightCurtain, 1f);
    }

    void SetCurtainAlpha(Image img, float a)
    {
        if (img == null) return;
        var c = img.color;
        c.a = a;
        img.color = c;
    }

    // ds < 90: 완전히 안 보임(alpha 0) / 90~99: alpha 50~70 사이를 sin파로 스무스하게 오감(경고) /
    // 100: alpha 100~120 사이를 sin파로 스무스하게 오감(위험). 계속 반복, 안 멈춤.
    // 100 구간은 회차 연출이 끝나(StopWorkingAnimations 이후)도 멈추지 않음 — LateUpdate가 _currentDs만 보고
    // 계속 도는 구조라 코루틴 종료와 무관하게 계속 오간다.
    void UpdateStressWarning()
    {
        if (stressWarningImage == null) return;

        float period = Mathf.Max(0.01f, stressBlinkInterval) * 2f;
        float wave = (Mathf.Sin(Time.time * (2f * Mathf.PI / period)) + 1f) * 0.5f; // 0~1

        float targetA;
        if (_currentDs >= 100f)
            targetA = Mathf.Lerp(80f / 255f, 120f / 255f, wave);
        else if (_currentDs >= 90f)
            targetA = Mathf.Lerp(50f / 255f, 70f / 255f, wave);
        else
            targetA = 0f;

        var c = stressWarningImage.color;
        c.a = targetA;
        stressWarningImage.color = c;
    }

    // 선택된 팀장의 캐릭터 프리팹을 화면 밖에 인스턴스화해 working 애니메이션을 실제로 재생시키고,
    // 그 SpriteRenderer.sprite를 매 프레임 characterImage로 미러링한다 (LateUpdate).
    void SpawnCharacterPreview(EmployeeData employee)
    {
        ClearCharacterPreview();
        if (characterImage == null || employee == null) return;

        // CEO는 일반 직원과 달리 Resources/Characters/{portraitId}에 프리팹이 없고,
        // OfficeManager.SpawnCEO()와 동일하게 CEOManager.ceoPrefab(→ 없으면 OfficeManager.fallbackPrefab)을 사용한다.
        GameObject prefab;
        if (employee.isCEO)
        {
            prefab = CEOManager.Instance != null ? CEOManager.Instance.ceoPrefab : null;
            if (prefab == null) prefab = OfficeManager.Instance != null ? OfficeManager.Instance.fallbackPrefab : null;
        }
        else
        {
            if (string.IsNullOrEmpty(employee.portraitId)) return;
            prefab = Resources.Load<GameObject>($"Characters/{employee.portraitId}");
        }
        if (prefab == null) return;

        _previewInstance = Instantiate(prefab);
        _previewInstance.transform.position = new Vector3(9999f, 9999f, 0f); // 화면 밖 — 어떤 카메라에도 안 잡히게

        int layer = LayerMask.NameToLayer(previewLayerName);
        if (layer >= 0)
            foreach (var t in _previewInstance.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;

        // 게임플레이 로직(이동/파견/클릭 등) 제거 — 애니메이션만 필요
        foreach (var c in _previewInstance.GetComponentsInChildren<OfficeCharacter>())     { c.enabled = false; Destroy(c); }
        foreach (var c in _previewInstance.GetComponentsInChildren<CharacterMover>())      { c.enabled = false; Destroy(c); }
        foreach (var c in _previewInstance.GetComponentsInChildren<IsometricSorter>())     { c.enabled = false; Destroy(c); }
        foreach (var c in _previewInstance.GetComponentsInChildren<CharacterController>()) { c.enabled = false; Destroy(c); }
        foreach (var c in _previewInstance.GetComponentsInChildren<Rigidbody2D>())  Destroy(c);
        foreach (var c in _previewInstance.GetComponentsInChildren<Collider2D>())   Destroy(c);

        var charAnimator = _previewInstance.GetComponentInChildren<CharacterAnimator>();
        var rawAnimator  = _previewInstance.GetComponentInChildren<Animator>();
        if (charAnimator != null)
        {
            charAnimator.SetWorking(true);
            // CharacterAnimator.Update()는 GameTimeManager.IsRunning이 false면 animator.speed를 0으로 묶어버림 —
            // 팀장점수 연출은 시간을 멈춘 채 진행되므로, 여기서 꺼서 그 게이팅을 우회하고 아래서 speed를 직접 고정한다.
            charAnimator.enabled = false;
        }
        if (rawAnimator != null) rawAnimator.speed = 1f;
        _previewAnimator = rawAnimator;

        _previewSpriteRenderer = _previewInstance.GetComponentInChildren<SpriteRenderer>();
        characterImage.enabled = true;

        if (fireAnimator != null)
        {
            fireAnimator.gameObject.SetActive(true); // 새 팀장 프리뷰 시작 시 fire도 다시 활성화+재생
            fireAnimator.speed = 1f;
        }
    }

    // 회차 점수가 다 오르고 나면(연출 완료) 캐릭터 working 애니 정지 + FireAnim 오브젝트 비활성화.
    void StopWorkingAnimations()
    {
        if (_previewAnimator != null) _previewAnimator.speed = 0f;
        if (fireAnimator != null)     fireAnimator.gameObject.SetActive(false);
    }

    void ClearCharacterPreview()
    {
        if (_previewInstance != null) Destroy(_previewInstance);
        _previewInstance = null;
        _previewSpriteRenderer = null;
        _previewAnimator = null;
    }

    public void OnClickConfirm()
    {
        // 팀장점수를 DevelopmentPanelUI 에 한 번에 반영 (실제값+표시값 즉시 동기화 → 점프 업).
        // _onComplete(→StartDeveloping) 의 SaveProject 전에 적용돼야 accum 에 저장됨.
        // 테스트 모드(DevelopmentManager.TestLeaderScore)는 프로젝트 상태에 영향을 주면 안 되므로 스킵.
        if (!_testMode)
            DevelopmentPanelUI.Instance.AddValuesInstant(_applyPlanning, _applyDevelop, _applyArt, 0f, 0f);
        _applyPlanning = _applyDevelop = _applyArt = 0f;

        ClearCharacterPreview();
        leaderscorePanel.SetActive(false);
        LeaderSelectUI.Instance.entireLeaderPanel.gameObject.SetActive(false);
        if (stressWarningImage != null) stressWarningImage.gameObject.SetActive(false);
        GameTimeManager.Instance?.StartTime();
        ModalGate.I.Unregister(this);
        _onComplete?.Invoke();
        OnConfirmClosed?.Invoke();
    }
}
