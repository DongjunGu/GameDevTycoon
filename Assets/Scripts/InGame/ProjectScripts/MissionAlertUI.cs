using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

// HUDCanvas/MissionAlertUI — SupriseQuestUI-QuestPanel 또는 LeaderSelectUI-MissionPanel 클릭 시 열리는
// 도전과제 상세 팝업. mainPanel(MissionMainPanel, ModalLayer 부착)을 SetActive로 토글해 최상단에 띄운다
// (ModalLayer는 OnEnable/OnDisable로 동작하므로 SetActive 토글이 정석 — CanvasGroup 방식 아님).
public class MissionAlertUI : MonoBehaviour
{
    public static MissionAlertUI Instance { get; private set; }

    public GameObject mainPanel;      // MissionMainPanel
    public Image missionIcon;         // MissionPanel/MissionIcon — 도전 파트(ChallengePart) 아이콘
    public TextMeshProUGUI missionText;
    public Image rewardIcon;
    public TextMeshProUGUI rewardText;
    public Button confirmButton;
    public Button receiveRewardButton; // 보상 미수령 성공 상태일 때 confirmButton 대신 노출 — 눌러야 보상 지급
    [Tooltip("BottomPanel — 이미 달성+수령까지 끝난 뒤 다시 열람할 때(needsClaim도 아니고 진행중도 아닌 상태) confirmButton 대신 노출")]
    public Button confirmButton2;
    public GameObject successPanel;   // MissionSuccessPanel

    [Header("현재 파트별 점수 (CurrentScorePanel — 도전 파트 무관 3파트 항상 표시)")]
    [Tooltip("QuestPanel 클릭으로 열 때는 미리보기 성격이라 숨기고, MissionPanel(팀장점수 쪽) 클릭/자동 알림 때는 보여준다 — Show()의 showCurrentScorePanel 인자로 토글")]
    public GameObject currentScorePanel;
    public TextMeshProUGUI planningScoreText;
    public TextMeshProUGUI developScoreText;
    public TextMeshProUGUI artScoreText;
    public Image planningScoreRoleIcon;
    public Image developScoreRoleIcon;
    public Image artScoreRoleIcon;

    [Header("보상 파트 아이콘 L")]
    public Sprite planIconL;
    public Sprite devIconL;
    public Sprite artIconL;

    [Header("보상 획득 연출 — RewardIcon에서 미끄러져 나와 CurrentScorePanel의 해당 RoleIcon으로 흡수")]
    [Tooltip("LeaderScoreUI.BonusFlyCoroutine과 동일 패턴(아이콘 1개 버전) — 아래쪽 부채꼴로 여유있게 펼쳐졌다 잠깐 대기 후 목표로 빨려들어감")]
    public float rewardDipRadius = 150f;
    public float rewardDipArcDegrees = 150f;
    public float rewardDipDuration = 0.45f;
    public float rewardDipHoldDuration = 0.1f;
    public float rewardFlyDuration = 0.35f;
    public float rewardPunchStrength = 0.35f;
    public float rewardPunchDuration = 0.35f;
    public int rewardPunchVibrato = 8;
    [Range(0f, 1f)] public float rewardPunchElasticity = 0.7f;

    void Awake()
    {
        Instance = this;
        if (confirmButton != null) confirmButton.onClick.AddListener(Hide);
        if (confirmButton2 != null) confirmButton2.onClick.AddListener(Hide);
        if (receiveRewardButton != null) receiveRewardButton.onClick.AddListener(OnClickReceiveReward);
        if (mainPanel != null) mainPanel.SetActive(false);
    }

    System.Action _onClosed; // Show(onClosed) 호출 시 저장 — 패널이 (어느 버튼으로든) 닫힐 때 1회 호출
    bool _timeStopped; // 보상 미수령 성공(needsClaim) 상태로 열려있는 동안만 true — GameTimeManager.StopTime() 카운터와 1:1로 짝지어 관리

    // onClosed: 패널이 닫히는 순간(ConfirmBtn/ReceiveRewardBtn 무관) 1회 호출되는 콜백 — 개발 완료 흐름처럼
    // "보상 확인 후 다음 단계로 진행"해야 하는 호출자가 사용. 그냥 열람용(QuestPanel 클릭 등)은 생략.
    // showCurrentScorePanel: LeaderScoreUI-MissionPanel 클릭/자동 알림은 true(그대로 표시), QuestPanel
    // 클릭(도전과제 미리보기)은 OpenMissionAlertOnClick에서 false로 넘겨 CurrentScorePanel을 숨긴다.
    public void Show(System.Action onClosed = null, bool showCurrentScorePanel = true)
    {
        _onClosed = onClosed;
        var dm = DevelopmentManager.Instance;
        var c = dm != null ? dm.Challenge : null;

        // 파트총점(PartTotal) 도전과제는 CurrentScorePanel(팀장점수 리더존 3파트 비교용)을 안 보여주기로 함 —
        // 직접 클릭해서 열든, 목표 달성으로 자동으로 뜨든, 보상 수령까지 이 패널이 열려있는 내내(RewardFlyCoroutine이
        // 따로 안 건드리므로) 계속 꺼진 상태로 유지된다.
        bool isPartTotal = c != null && c.IsActive && c.Kind == ChallengeKind.PartTotal;
        if (currentScorePanel != null) currentScorePanel.SetActive(showCurrentScorePanel && !isPartTotal);

        if (c == null || !c.IsActive)
        {
            if (missionText != null) missionText.text = "없음";
            if (missionIcon != null) missionIcon.enabled = false;
            if (rewardIcon != null) rewardIcon.enabled = false;
            if (rewardText != null) rewardText.text = "";
            if (successPanel != null) successPanel.SetActive(false);
            SetButtonState(false, false);
            if (planningScoreText != null) planningScoreText.text = "0";
            if (developScoreText != null) developScoreText.text = "0";
            if (artScoreText != null) artScoreText.text = "0";
            if (mainPanel != null) mainPanel.SetActive(true);
            return;
        }

        // 95존/99존은 내부 명칭을 그대로 노출하지 않고 "{파트}팀장 N점 이상 달성"으로 통일 표시(파트총점만 별도 문구).
        // "[파트]" 같은 대괄호 접두 형식은 안 붙인다. 파트명({기획/개발/아트}) 부분만 파트별 색상으로 강조.
        if (missionText != null)
        {
            string coloredChallengePart = ColorizePartName(c.ChallengePart);
            missionText.text = c.Kind == ChallengeKind.PartTotal
                ? $"{coloredChallengePart} 총점 <color=#E63356>{Mathf.RoundToInt(c.TargetValue)}</color>점 이상 달성"
                : $"{coloredChallengePart}팀장 <color=#E63356>{Mathf.RoundToInt(c.TargetValue)}</color>점 이상 달성";
        }
        if (missionIcon != null)
        {
            var sprite = PartIconSprite(c.ChallengePart);
            missionIcon.sprite = sprite;
            missionIcon.enabled = sprite != null;
        }

        if (rewardIcon != null)
        {
            var sprite = PartIconSprite(c.RewardPart);
            rewardIcon.sprite = sprite;
            rewardIcon.enabled = sprite != null;
        }
        // "{기획/개발/아트} 점수 +N" — 파트명은 파트별 색상, 점수는 목표 점수와 동일한 강조색(E63356).
        if (rewardText != null)
            rewardText.text = $"{ColorizePartName(c.RewardPart)} 점수 <color=#E63356>+{Mathf.RoundToInt(c.RewardScore)}</color>";

        // 도전 파트와 무관하게 3파트 현재 실제값을 항상 나란히 표시 — 같은 기준(c.Kind)으로 조회.
        if (planningScoreText != null) planningScoreText.text = Mathf.RoundToInt(c.GetCurrentValueForPart(LeaderType.Planner)).ToString();
        if (developScoreText != null) developScoreText.text = Mathf.RoundToInt(c.GetCurrentValueForPart(LeaderType.Programmer)).ToString();
        if (artScoreText != null) artScoreText.text = Mathf.RoundToInt(c.GetCurrentValueForPart(LeaderType.Artist)).ToString();

        bool succeeded = c.Resolved && c.Succeeded;
        if (successPanel != null) successPanel.SetActive(succeeded);
        SetButtonState(succeeded && !c.RewardApplied, succeeded && c.RewardApplied);
        if (mainPanel != null) mainPanel.SetActive(true);
    }

    // 파트별 아이콘(L 사이즈) — missionIcon(도전 파트)/rewardIcon(보상 파트) 공용.
    Sprite PartIconSprite(LeaderType part) => part switch
    {
        LeaderType.Planner => planIconL,
        LeaderType.Programmer => devIconL,
        LeaderType.Artist => artIconL,
        _ => null
    };

    // 파트명({기획/개발/아트})을 파트별 고정 색상으로 감싼 리치텍스트 — missionText/rewardText 공용.
    static string ColorizePartName(LeaderType part) => $"<color=#{PartColorHex(part)}>{ChallengeManager.PartDisplayName(part)}</color>";

    static string PartColorHex(LeaderType part) => part switch
    {
        LeaderType.Planner => "FFC552",
        LeaderType.Programmer => "A9A9C0",
        LeaderType.Artist => "DBAC7C",
        _ => "FFFFFF"
    };

    // 하단 버튼 3택1 — needsClaim(성공했는데 보상 미수령)이면 receiveRewardButton, alreadyAchieved(성공+
    // 수령까지 끝난 뒤 다시 열람)면 confirmButton2, 그 외(진행중/실패/도전과제 없음)는 confirmButton.
    // needsClaim 상태로 뜨는 동안만 시간을 멈춘다 — 클릭으로 그냥 열람할 때는 시간이 그대로 흐른다.
    void SetButtonState(bool needsClaim, bool alreadyAchieved)
    {
        if (confirmButton != null) confirmButton.gameObject.SetActive(!needsClaim && !alreadyAchieved);
        if (confirmButton2 != null) confirmButton2.gameObject.SetActive(alreadyAchieved);
        if (receiveRewardButton != null)
        {
            receiveRewardButton.gameObject.SetActive(needsClaim);
            receiveRewardButton.interactable = true; // 흡수 연출 중 눌러둔 잠금을 다음 노출 때 항상 원복
        }

        if (needsClaim != _timeStopped)
        {
            _timeStopped = needsClaim;
            if (needsClaim) GameTimeManager.Instance?.StopTime();
            else GameTimeManager.Instance?.StartTime();
        }
    }

    void OnClickReceiveReward()
    {
        if (receiveRewardButton != null) receiveRewardButton.interactable = false; // 연출 중 중복 클릭 방지
        StartCoroutine(RewardFlyCoroutine(Hide));
    }

    // RewardIcon에서 미끄러져 나와(펼쳐짐 → 대기 → 흡수) 보상이 실제로 반영되는 곳으로 빨려들어간다 —
    // 팀장점수(리더존) 보상은 CurrentScorePanel의 해당 RoleIcon으로, 총점(파트총점) 보상은
    // DevelopmentPanelUI의 해당 파트 패널로(ChallengeManager.ClaimReward가 실제로 그쪽 값을 올리므로).
    // LeaderScoreUI.BonusFlyCoroutine과 동일한 3단계 구조를 아이콘 1개짜리로 옮긴 것 — 보상 지급(ClaimReward)은
    // 아이콘이 도착하는 순간에 실행해, 그 직후 갱신하는 표시값이 실제 반영된 최신값과 정확히 맞도록 한다.
    // 흡수+펀치가 끝나고 0.2초 뒤 onComplete(=Hide)를 불러 자동으로 닫힌다.
    IEnumerator RewardFlyCoroutine(System.Action onComplete)
    {
        var challenge = DevelopmentManager.Instance?.Challenge;
        if (challenge == null) { onComplete?.Invoke(); yield break; }

        LeaderType rewardPart = challenge.RewardPart;
        bool isPartTotal = challenge.Kind == ChallengeKind.PartTotal;

        RectTransform targetIcon;
        TextMeshProUGUI targetText; // 총점은 AddValuesInstant가 DevelopmentPanelUI 텍스트를 자동 갱신하므로 null로 둠
        if (isPartTotal)
        {
            var devPanelUI = DevelopmentPanelUI.Instance;
            targetIcon = rewardPart switch
            {
                LeaderType.Planner => devPanelUI != null ? devPanelUI.planningPanel : null,
                LeaderType.Programmer => devPanelUI != null ? devPanelUI.devPanel : null,
                LeaderType.Artist => devPanelUI != null ? devPanelUI.artPanel : null,
                _ => null
            };
            targetText = null;
        }
        else
        {
            Image roleIcon = rewardPart switch
            {
                LeaderType.Planner => planningScoreRoleIcon,
                LeaderType.Programmer => developScoreRoleIcon,
                LeaderType.Artist => artScoreRoleIcon,
                _ => null
            };
            targetIcon = roleIcon != null ? roleIcon.transform as RectTransform : null;
            targetText = rewardPart switch
            {
                LeaderType.Planner => planningScoreText,
                LeaderType.Programmer => developScoreText,
                LeaderType.Artist => artScoreText,
                _ => null
            };
        }

        // 연출에 필요한 참조가 없으면 보상만 조용히 지급하고 끝 — 씬 배선 누락이 보상 자체를 막으면 안 됨.
        if (rewardIcon == null || rewardIcon.sprite == null || targetIcon == null || mainPanel == null)
        {
            challenge.ClaimReward();
            onComplete?.Invoke();
            yield break;
        }

        Vector3 originPos = rewardIcon.transform.position;
        Vector3 targetPos = targetIcon.position;

        var go = new GameObject("RewardFlyIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(mainPanel.transform, false);
        go.transform.SetAsLastSibling(); // CurrentScorePanel 등 다른 자식들보다 항상 위에 그려지도록
        var rt = (RectTransform)go.transform;
        var img = go.GetComponent<Image>();
        img.sprite = rewardIcon.sprite;
        img.raycastTarget = false;
        rt.sizeDelta = ((RectTransform)rewardIcon.transform).sizeDelta;
        rt.position = originPos;
        rt.localScale = Vector3.zero;

        // 아래쪽 부채꼴 방향으로 흩어질 지점 하나 계산 — LeaderScoreUI.BuildDownwardFanScatterOffsets의
        // count=1 케이스와 동일한 공식. lossyScale 보정은 [[feedback_pixel_offset_world_position]] 참고.
        const float DownAngle = -90f;
        float angle = DownAngle + Random.Range(-rewardDipArcDegrees * 0.5f, rewardDipArcDegrees * 0.5f);
        float rad = angle * Mathf.Deg2Rad;
        float worldScale = rewardIcon.transform.lossyScale.x;
        Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * rewardDipRadius * Random.Range(0.7f, 1f);
        Vector3 dipTargetPos = originPos + (Vector3)(offset * worldScale);

        // ① 아래쪽으로 여유있게 미끄러지며 펼쳐짐
        float dipDur = Mathf.Max(0.01f, rewardDipDuration);
        float dipElapsed = 0f;
        while (dipElapsed < dipDur)
        {
            dipElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(dipElapsed / dipDur);
            float eased = 1f - Mathf.Pow(1f - t, 2f); // 완만한 ease-out
            float scaleT = t < 0.6f ? Mathf.Lerp(0f, 1.15f, t / 0.6f) : Mathf.Lerp(1.15f, 1f, (t - 0.6f) / 0.4f);
            rt.position = Vector3.Lerp(originPos, dipTargetPos, eased);
            rt.localScale = Vector3.one * scaleT;
            yield return null;
        }
        rt.position = dipTargetPos;
        rt.localScale = Vector3.one;

        // ② 펼쳐진 채로 잠깐 대기
        if (rewardDipHoldDuration > 0f)
            yield return new WaitForSeconds(rewardDipHoldDuration);

        // ③ 목표 RoleIcon으로 빨려들어가기 (강한 ease-in)
        float flyDur = Mathf.Max(0.01f, rewardFlyDuration);
        float flyElapsed = 0f;
        while (flyElapsed < flyDur)
        {
            flyElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(flyElapsed / flyDur);
            float eased = t * t * t * t;
            rt.position = Vector3.Lerp(dipTargetPos, targetPos, eased);
            rt.localScale = Vector3.one * (1f - eased * 0.7f);
            yield return null;
        }

        Destroy(go);

        // ClaimReward()는 팀장점수(리더존) 보상이든 파트총점 보상이든 항상 DevelopmentPanelUI 쪽 값만
        // 올린다(ChallengeManager.ClaimReward 참고) — CurrentScorePanel이 리더존일 때 보여주는
        // GetCurrentValueForPart(=리더 점수 총합)는 그 값을 안 읽으므로, ClaimReward 후 다시 조회해도
        // 그대로라 화면이 안 올라가는 것처럼 보였다. 그래서 지급 "전" 값을 미리 잡아두고 RewardScore를
        // 직접 더해 표시한다 — 실제로 어느 필드가 올랐는지와 무관하게 흡수된 만큼 눈에 보이게 반영.
        float beforeValue = targetText != null ? challenge.GetCurrentValueForPart(rewardPart) : 0f;

        // 흡수 임팩트 — 보상을 실제로 지급한 뒤, 목표(RoleIcon 또는 DevelopmentPanel 파트 패널)를
        // 펀치 스케일하고(총점이 아니면) 최신값으로 갱신.
        challenge.ClaimReward();
        FirePunch(targetIcon);
        if (targetText != null)
        {
            FirePunch(targetText.transform as RectTransform);
            targetText.text = Mathf.RoundToInt(beforeValue + challenge.RewardScore).ToString();
        }

        yield return new WaitForSeconds(0.2f);
        onComplete?.Invoke();
    }

    // DevelopmentPanelUI.FirePunch/LeaderScoreUI.FirePunch와 동일한 방식 — 팡 튀었다가 탄성으로 빠르게 정착.
    void FirePunch(RectTransform rt)
    {
        if (rt == null) return;
        rt.DOKill();
        rt.localScale = Vector3.one;
        rt.DOPunchScale(Vector3.one * rewardPunchStrength, rewardPunchDuration, rewardPunchVibrato, rewardPunchElasticity).SetUpdate(true);
    }

    void Hide()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        SetButtonState(false, false); // 멈춰있던 시간이 있으면 여기서 재개(카운터 상쇄)
        var cb = _onClosed;
        _onClosed = null;
        cb?.Invoke();
    }
}
