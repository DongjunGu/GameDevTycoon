using System.Collections;
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

    [Header("스트레스 슬라이더 눈금 (90/95/99% 보너스 임계선 — 위치는 직접 배치, 평소엔 비활성 상태로 둘 것)")]
    public RectTransform tick90Mark;
    public RectTransform tick95Mark;
    public RectTransform tick99Mark;
    [Tooltip("각 nImage 자식의 보너스 점수 텍스트 (비워두면 자동으로 자식 TextMeshProUGUI를 찾아서 씀)")]
    public TextMeshProUGUI tick90BonusText;
    public TextMeshProUGUI tick95BonusText;
    public TextMeshProUGUI tick99BonusText;
    [Tooltip("\"스트레스 폭발까지 n\" — 3회차 끝난 시점 누적ds 기준으로 갱신")]
    public TextMeshProUGUI burstText;

    [Header("스트레스 경고 (LeaderScoreEntirePanel)")]
    public Image stressWarningImage;          // LeaderScoreEntirePanel의 Image — 기본 alpha 0
    public float stressBlinkInterval = 0.3f;  // sin파 반 주기(초) — 값↔값 왕복 한 방향 시간
    private float _currentDs = 0f;

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
    // 아이콘들이 순차적으로 빨려들어가기 "시작"하는 전체 목표 시간(초). 개수가 몇 개든 이 시간 안에 전부
    // 시작되도록 간격을 자동 계산(간격 = 이 값 / 개수)해서, 개수가 많아져도 총 재생시간이 무한정 늘어나지 않는다.
    // 0이면 기존처럼 일괄 동시 시작.
    public float suckStaggerWindow = 1f;
    public RectTransform topIcon;      // TopIcon(프레임) — 빨려들어가는 동안 총점 텍스트와 함께 살짝 커졌다 원상복구
    public float suckPulseScale = 1.2f; // topIcon/totalText 가 흡입 도중 커지는 배율
    public float suckPulseDelay = 0.08f; // 흡입 시작(t=0) 직후엔 ease-in 특성상 실제로는 거의 안 움직이는 "텀"이 있음 — 그 텀만큼 지난 뒤에야 펄스 스케일을 시작
    public float textPulseDelay = 0.3f; // topIcon 펄스가 시작(suckPulseDelay 시점)하고 나서 totalText 펄스가 시작하기까지 추가 지연시간(초)
    public float burstSpitDuration = 0.4f; // 스트레스 100 오버플로 시 총점 위치에서 아이콘을 역방향으로 뱉어내는 시간

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

    // 90/95/99 눈금(및 보너스 텍스트)을 켠다 — 3회차 연출이 끝나 4회차 대기가 시작될 때 1회 호출.
    // 이 시점엔 아직 4회차가 안 굴려졌으므로, 실제 획득액이 아니라 "이 임계선까지 도달하면 받을 수
    // 있는" 잠재 범위(중첩 누적)를 미리보기로 보여준다.
    void ShowThresholdTicks()
    {
        if (tick90Mark != null) tick90Mark.gameObject.SetActive(true);
        if (tick95Mark != null) tick95Mark.gameObject.SetActive(true);
        if (tick99Mark != null) tick99Mark.gameObject.SetActive(true);
        if (burstText != null) burstText.gameObject.SetActive(true);
        RefreshBonusPotentialTexts();
        UpdateBurstText();
    }

    // "스트레스 폭발까지 n" — 3회차 끝난 시점 누적ds(_pendingPrevCumDs) 기준으로 100까지 남은 값.
    void UpdateBurstText()
    {
        if (burstText == null) return;
        int remain = Mathf.RoundToInt(100f - _pendingPrevCumDs);
        burstText.text = $"스트레스 폭발까지 {remain}";
    }

    void HideThresholdTicks()
    {
        if (tick90Mark != null) tick90Mark.gameObject.SetActive(false);
        if (tick95Mark != null) tick95Mark.gameObject.SetActive(false);
        if (tick99Mark != null) tick99Mark.gameObject.SetActive(false);
        if (burstText != null) burstText.gameObject.SetActive(false);
    }

    // 3회차 끝난 시점 미리보기 — "+min~max" (중첩 포함, DevelopmentManager.GetLeaderBonusPotentialRange)
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

        var (min, max) = DevelopmentManager.Instance.GetLeaderBonusPotentialRange(thresholdIndex);
        float sample = Random.Range(min, max); // 범위 텍스트 대신 그 범위 안에서 임의로 하나 찍어 정수로 표시
        text.text = $"+{Mathf.RoundToInt(sample)}";
    }

    // 4회차까지 끝난 뒤(정산 시점) — 실제로 획득한 보너스 금액으로 교체. DevelopmentManager.GetLeaderBonusAmount가 단일 소스.
    void RefreshBonusActualTexts()
    {
        SetBonusActualText(tick90Mark, ref tick90BonusText, 0);
        SetBonusActualText(tick95Mark, ref tick95BonusText, 1);
        SetBonusActualText(tick99Mark, ref tick99BonusText, 2);
    }

    void SetBonusActualText(RectTransform tick, ref TextMeshProUGUI text, int thresholdIndex)
    {
        if (text == null && tick != null) text = tick.GetComponentInChildren<TextMeshProUGUI>();
        if (text == null || DevelopmentManager.Instance == null) return;

        float amount = DevelopmentManager.Instance.GetLeaderBonusAmount(thresholdIndex);
        text.text = $"+{Mathf.RoundToInt(amount)}";
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

        // 초기화
        if (roundScoreTexts != null)
            foreach (var t in roundScoreTexts) if (t) t.text = "0";
        if (dsText) dsText.text = "0";
        if (dsSlider) { dsSlider.minValue = 0f; dsSlider.maxValue = 100f; dsSlider.value = 0f; }
        if (totalText) totalText.text = "0";
        _currentDs = 0f;
        _pendingDisplayedTotal = 0f;
        _pendingPrevCumDs = 0f;
        HideThresholdTicks(); // 90/95/99 눈금은 3회차 끝날 때 다시 켬 (ShowThresholdTicks)
        if (stressWarningImage != null)
        {
            var swc = stressWarningImage.color;
            swc.a = 0f;
            stressWarningImage.color = swc;
        }

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
            bool isOverflow = (overflowRound == r);
            float targetDs    = cumDsAfter[r];
            float targetRound = isOverflow ? 0f : fullRoundScores[r]; // 오버플로 회차는 0점

            float startTotal = displayedTotal;

            // Phase A — ds(스트레스)만 상승, 회차 점수는 아직 0으로 숨겨둠
            SetRoundText(r, 0f);
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float ds = Mathf.Lerp(prevCumDs, targetDs, t);
                if (dsText) dsText.text = Mathf.RoundToInt(ds).ToString();
                if (dsSlider) dsSlider.value = Mathf.Clamp(ds, 0f, 100f);
                _currentDs = ds;
                yield return null;
            }
            if (dsText) dsText.text = Mathf.RoundToInt(targetDs).ToString();
            if (dsSlider) dsSlider.value = Mathf.Clamp(targetDs, 0f, 100f);
            _currentDs = targetDs;
            prevCumDs = targetDs;

            // 4회차 결과가 확정되는 시점(성공/오버플로 무관) — 3회차 끝나고 떠있던 미리보기(눈금/BurstText)는
            // 이제 실제 결과가 나오는 참이니 걷어낸다.
            if (r == 3) HideThresholdTicks();

            if (isOverflow)
            {
                // 누적 ds 100 초과(burst): 총점 위치(categoryIcon)에서 빨아들였던 것과 반대로 아이콘을
                // 뱉어내는 연출을 동시에 재생하면서, 전 회차 점수 일괄 차감 연출 후 종료.
                float lost = 0f;
                for (int k = 0; k < r; k++) lost += fullRoundScores[k] - roundScores[k];
                StartCoroutine(SpitBurstCoroutine(Mathf.Max(0, Mathf.RoundToInt(lost))));

                yield return StartCoroutine(ApplyCutCoroutine(fullRoundScores, roundScores, r));
                reachedEnd = true; // 오버플로는 회차와 무관하게 항상 그 자리에서 종료
                break;
            }

            // 4회차는 보너스 점수(90/95/99 임계선 누적)도 같이 팝콘으로 뿌려서 총점에 흡수시킨다.
            float bonusExtra = (r == 3 && DevelopmentManager.Instance != null) ? DevelopmentManager.Instance.GetLeaderBonusTotal() : 0f;

            // Phase B+C — 회차 점수(+4회차면 보너스까지)만큼 아이콘이 팝콘처럼 펑 터진 뒤 categoryIcon으로
            // 빨려들어가는 동안 총점 + 회차 점수가 동시에 상승.
            yield return StartCoroutine(PopAndFlyCoroutine(Mathf.Max(0, Mathf.RoundToInt(targetRound + bonusExtra)), r, startTotal, targetRound, bonusExtra));
            displayedTotal = startTotal + targetRound + bonusExtra;

            if (r < toRoundExclusive - 1)
                yield return new WaitForSeconds(roundGap);
        }

        _pendingDisplayedTotal = displayedTotal;
        _pendingPrevCumDs = prevCumDs;

        if (!reachedEnd)
        {
            // 1~3회차 연출까지 다 끝남 — 이제부터 4회차 조준 선택 대기 (커튼/스트레스 경고는 계속 돌아감).
            // announceRound4Wait=false(튜토리얼 1회차 단독 재생)면 아직 3회차 전이므로 이 상태로 안 넘어간다.
            if (announceRound4Wait)
            {
                IsWaitingForRound4Aim = true;
                ShowThresholdTicks();
            }
            yield break;
        }

        // 4회차까지 끝났으면 미리보기(범위) 대신 실제 획득액으로 교체
        RefreshBonusActualTexts();

        // 총합 최종 확정 + 역할 누적스탯 반영
        // (훈수쟁이 보너스는 여기서 처리하지 않음 — 팀장점수 연출/확정과 분리해 ContinueAfterLeaderScore 에서
        //  AlertUI3 로 별도 안내 후 적용된다.)
        if (totalText) totalText.text = Mathf.RoundToInt(total).ToString();

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

        yield return new WaitForSeconds(0.5f);
        // 4회차(또는 오버플로) 결과가 화면에 다 나온 지금에야 confirmBtn을 켠다 — 위 InitPanel 주석 참고.
        if (confirmButton) { confirmButton.gameObject.SetActive(true); confirmButton.interactable = true; }
        OnRoundsVisualComplete?.Invoke();
    }

    // 누적 ds 100 초과 시: 전 회차(0..overflowRound-1) 점수를 full → cut 값으로 내림
    IEnumerator ApplyCutCoroutine(float[] fullRoundScores, float[] roundScores, int overflowRound)
    {
        if (overflowRound <= 0) yield break;

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
            yield return null;
        }

        for (int k = 0; k < overflowRound; k++)
            SetRoundText(k, roundScores[k]);
    }

    void SetRoundText(int index, float value)
    {
        if (roundScoreTexts != null && index >= 0 && index < roundScoreTexts.Length && roundScoreTexts[index])
            roundScoreTexts[index].text = Mathf.RoundToInt(value).ToString();
    }

    // count개의 iconPrefab을 popcornPoint에서 팝콘처럼 펑 터뜨린 뒤, categoryIcon 위치로 빨려들어가게 하면서
    // 그와 동시에 총점(startTotal→startTotal+targetRound)과 해당 회차 점수(0→targetRound)를 함께 올린다.
    // bonusExtra: 4회차에서 90/95/99 임계선 보너스가 있으면 회차점수와 함께 팝콘으로 뿌려서 총점에 얹는다.
    // 회차 점수 텍스트(roundScoreTexts)는 targetRound까지만 오르고, 총점만 그만큼 더 올라간다.
    IEnumerator PopAndFlyCoroutine(int count, int roundIndex, float startTotal, float targetRound, float bonusExtra = 0f)
    {
        if (count <= 0 || iconPrefab == null || popcornPoint == null)
        {
            // 연출 리소스가 없으면 텍스트만 즉시 반영
            SetRoundText(roundIndex, targetRound);
            if (totalText) totalText.text = Mathf.RoundToInt(startTotal + targetRound + bonusExtra).ToString();
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

        Vector3 topIconBaseScale = topIcon != null ? topIcon.localScale : Vector3.one;
        Vector3 totalTextBaseScale = totalText != null ? totalText.transform.localScale : Vector3.one;

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
            }

            float t = Mathf.Clamp01(elapsed / totalDur);
            float rs = Mathf.Lerp(0f, targetRound, t);
            SetRoundText(roundIndex, rs);
            float totalRise = Mathf.Lerp(0f, targetRound + bonusExtra, t);
            if (totalText) totalText.text = Mathf.RoundToInt(startTotal + totalRise).ToString();

            // topIcon을 흡입 도중 살짝 부풀렸다가 끝나면 원래 크기로. 단, 흡입 시작 직후(ease-in 특성상
            // 실제로 거의 안 움직이는 "텀")엔 아직 스케일업하지 않고, suckPulseDelay 만큼 지난 뒤부터 남은 시간에
            // sin 곡선(양 끝 0=원래 크기, 중앙에서 suckPulseScale 배 피크)을 압축해 재생한다.
            // totalText는 topIcon이 커지기 시작한 뒤 textPulseDelay 만큼 더 지나서야 같은 방식으로 펄스를 시작한다.
            float iconScaleMul = ComputeSuckPulseScale(elapsed, suckPulseDelay, totalDur, suckPulseScale);
            if (topIcon != null) topIcon.localScale = topIconBaseScale * iconScaleMul;

            float textScaleMul = ComputeSuckPulseScale(elapsed, suckPulseDelay + textPulseDelay, totalDur, suckPulseScale);
            if (totalText != null) totalText.transform.localScale = totalTextBaseScale * textScaleMul;

            yield return null;
        }

        SetRoundText(roundIndex, targetRound);
        if (totalText) totalText.text = Mathf.RoundToInt(startTotal + targetRound + bonusExtra).ToString();
        if (topIcon != null) topIcon.localScale = topIconBaseScale;
        if (totalText != null) totalText.transform.localScale = totalTextBaseScale;

        foreach (var rt in icons)
            if (rt != null) Destroy(rt.gameObject);
    }

    // startDelay 이전엔 1배(펄스 시작 전), 이후 (totalDur-startDelay) 구간에 sin 곡선(양 끝 0=원래크기,
    // 중앙에서 peakScale배 피크)을 압축 재생. topIcon/totalText가 서로 다른 startDelay로 이 함수를 호출해
    // "아이콘 먼저, 텍스트는 그 뒤"처럼 시작 시점만 어긋나게 만든다.
    static float ComputeSuckPulseScale(float elapsed, float startDelay, float totalDur, float peakScale)
    {
        float pulseElapsed = Mathf.Max(0f, elapsed - startDelay);
        float pulseDur = Mathf.Max(0.01f, totalDur - startDelay);
        float pulseT = Mathf.Clamp01(pulseElapsed / pulseDur);
        float pulse = Mathf.Sin(pulseT * Mathf.PI);
        return Mathf.Lerp(1f, peakScale, pulse);
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
            targets.Add(basePos + new Vector3(scatterOffsets[i], oy, 0f));
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
        GameTimeManager.Instance?.StartTime();
        ModalGate.I.Unregister(this);
        _onComplete?.Invoke();
        OnConfirmClosed?.Invoke();
    }
}
