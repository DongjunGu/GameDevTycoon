using UnityEngine;
using UnityEngine.UI;
using TMPro;

// HUDCanvas/MissionAlertUI — SupriseQuestUI-QuestPanel 또는 LeaderSelectUI-MissionPanel 클릭 시 열리는
// 도전과제 상세 팝업. mainPanel(MissionMainPanel, ModalLayer 부착)을 SetActive로 토글해 최상단에 띄운다
// (ModalLayer는 OnEnable/OnDisable로 동작하므로 SetActive 토글이 정석 — CanvasGroup 방식 아님).
public class MissionAlertUI : MonoBehaviour
{
    public static MissionAlertUI Instance { get; private set; }

    public GameObject mainPanel;      // MissionMainPanel
    public TextMeshProUGUI missionText;
    public Image rewardIcon;
    public TextMeshProUGUI rewardText;
    public Button confirmButton;
    public Button receiveRewardButton; // 보상 미수령 성공 상태일 때 confirmButton 대신 노출 — 눌러야 보상 지급
    public GameObject successPanel;   // MissionSuccessPanel

    [Header("보상 파트 아이콘 L")]
    public Sprite planIconL;
    public Sprite devIconL;
    public Sprite artIconL;

    void Awake()
    {
        Instance = this;
        if (confirmButton != null) confirmButton.onClick.AddListener(Hide);
        if (receiveRewardButton != null) receiveRewardButton.onClick.AddListener(OnClickReceiveReward);
        if (mainPanel != null) mainPanel.SetActive(false);
    }

    System.Action _onClosed; // Show(onClosed) 호출 시 저장 — 패널이 (어느 버튼으로든) 닫힐 때 1회 호출

    // onClosed: 패널이 닫히는 순간(ConfirmBtn/ReceiveRewardBtn 무관) 1회 호출되는 콜백 — 개발 완료 흐름처럼
    // "보상 확인 후 다음 단계로 진행"해야 하는 호출자가 사용. 그냥 열람용(QuestPanel 클릭 등)은 생략.
    public void Show(System.Action onClosed = null)
    {
        _onClosed = onClosed;
        var dm = DevelopmentManager.Instance;
        var c = dm != null ? dm.Challenge : null;
        if (c == null || !c.IsActive)
        {
            if (missionText != null) missionText.text = "없음";
            if (rewardIcon != null) rewardIcon.enabled = false;
            if (rewardText != null) rewardText.text = "";
            if (successPanel != null) successPanel.SetActive(false);
            SetClaimState(false);
            if (mainPanel != null) mainPanel.SetActive(true);
            return;
        }

        // 95존/99존은 내부 명칭을 그대로 노출하지 않고 "{파트} 팀장점수 N점 달성"으로 통일 표시(파트총점만 별도 문구).
        // "[파트]" 같은 대괄호 접두 형식은 안 붙인다.
        if (missionText != null)
            missionText.text = c.Kind == ChallengeKind.PartTotal
                ? $"파트 총점 <color=#E63356>{Mathf.RoundToInt(c.TargetValue)}</color>점 이상 달성"
                : $"{ChallengeManager.PartDisplayName(c.ChallengePart)} 팀장점수 <color=#E63356>{Mathf.RoundToInt(c.TargetValue)}</color>점 달성";

        if (rewardIcon != null)
        {
            var sprite = c.RewardPart switch
            {
                LeaderType.Planner => planIconL,
                LeaderType.Programmer => devIconL,
                LeaderType.Artist => artIconL,
                _ => null
            };
            rewardIcon.sprite = sprite;
            rewardIcon.enabled = sprite != null;
        }
        if (rewardText != null)
            rewardText.text = $"점수 +{Mathf.RoundToInt(c.RewardScore)}";

        bool succeeded = c.Resolved && c.Succeeded;
        if (successPanel != null) successPanel.SetActive(succeeded);
        SetClaimState(succeeded && !c.RewardApplied);
        if (mainPanel != null) mainPanel.SetActive(true);
    }

    // 보상 미수령 성공 상태(needsClaim)면 confirmButton 대신 receiveRewardButton만 노출 —
    // 플레이어가 직접 눌러야만 ClaimReward()가 호출되도록 강제.
    void SetClaimState(bool needsClaim)
    {
        if (confirmButton != null) confirmButton.gameObject.SetActive(!needsClaim);
        if (receiveRewardButton != null) receiveRewardButton.gameObject.SetActive(needsClaim);
    }

    void OnClickReceiveReward()
    {
        DevelopmentManager.Instance?.Challenge?.ClaimReward();
        Hide();
    }

    void Hide()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        var cb = _onClosed;
        _onClosed = null;
        cb?.Invoke();
    }
}
