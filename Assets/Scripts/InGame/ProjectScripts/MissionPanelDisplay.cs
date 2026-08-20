using UnityEngine;
using UnityEngine.UI;
using TMPro;

// LeaderSelectUI/.../LSRightPanel/MissionPanel 부착 — 도전과제가 총점/팀장점수(리더존) 무관, 도전 파트가
// 지금 보고 있는 팀장점수 화면과 달라도 항상 목표를 보여준다("N점 이상 달성", 진행률 표시 없음).
// 달성 시 SuccessPanel 활성화.
// 표시·숨김은 CanvasGroup으로만 처리한다 — 자기 자신을 SetActive로 껐다 켜면 이 스크립트의 Update 루프
// 자체가 멈춰 다시 켜질 방법이 없어진다(비활성 오브젝트는 Update가 안 돎). MissionText/MissionDetail/SuccessPanel은
// MissionPanel의 자식이라 이것들을 SetActive해도 이 스크립트 자신은 영향받지 않는다.
public class MissionPanelDisplay : MonoBehaviour
{
    public CanvasGroup canvasGroup;
    public GameObject missionTextRoot;   // MissionText (제목 줄) 전체
    public GameObject missionDetailRoot; // MissionDetail (목표 줄) 전체
    public GameObject successPanel;      // 달성 시 활성화되는 뱃지

    public TextMeshProUGUI missionText2;
    public Image missionDetailIcon;
    public TextMeshProUGUI missionDetailText; // 파트총점(PartTotal) 전용 — "총점 N점 이상 달성"

    [Header("팀장점수(리더존 95/99) 전용 — \"팀장점수 [뱃지] 이상\", missionDetailText 대신 표시")]
    public GameObject missionDetailBadgeRow;
    public TextMeshProUGUI scoreBadgeText; // 뱃지 안 목표점수 숫자

    LeaderType? _lastIconPart;

    void Update()
    {
        var dm = DevelopmentManager.Instance;
        var c = dm != null ? dm.Challenge : null;
        bool active = c != null && c.IsActive;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = active ? 1f : 0f;
            canvasGroup.blocksRaycasts = active;
            canvasGroup.interactable = active;
        }
        SetChildrenActive(active);
        if (!active) return;

        if (missionText2 != null) missionText2.text = "도전과제";

        if (missionDetailIcon != null && _lastIconPart != c.ChallengePart)
        {
            _lastIconPart = c.ChallengePart;
            string fileName = c.ChallengePart switch
            {
                LeaderType.Planner => "Job_Plan_m",
                LeaderType.Programmer => "Job_Dev_m",
                LeaderType.Artist => "Job_Art_m",
                _ => null
            };
            var sprite = fileName != null ? Resources.Load<Sprite>($"Images/{fileName}") : null;
            missionDetailIcon.sprite = sprite;
            missionDetailIcon.enabled = sprite != null;
        }

        // 파트총점은 기존 텍스트 한 줄, 팀장점수(95/99존)는 목표점수를 뱃지 이미지 안에 숫자로 표시("팀장점수 [N] 이상").
        bool isLeaderZone = c.Kind != ChallengeKind.PartTotal;
        if (missionDetailText != null) missionDetailText.gameObject.SetActive(!isLeaderZone);
        if (missionDetailBadgeRow != null) missionDetailBadgeRow.SetActive(isLeaderZone);

        // 파트총점(PartTotal) — MissionAlertUI.rewardText와 동일한 조립 방식: "{파트}(파트별 색상) 총 점수 {N}(#E63356) 이상".
        if (!isLeaderZone && missionDetailText != null)
            missionDetailText.text = $"{ColorizePartName(c.ChallengePart)} 총 점수 <color=#E63356>{Mathf.RoundToInt(c.TargetValue)}</color> 이상";
        if (isLeaderZone && scoreBadgeText != null)
            scoreBadgeText.text = Mathf.RoundToInt(c.TargetValue).ToString();

        if (successPanel != null)
            successPanel.SetActive(c.Resolved && c.Succeeded);
    }

    // 파트명({기획/개발/아트})을 파트별 고정 색상으로 감싼 리치텍스트 — MissionAlertUI.ColorizePartName과 동일 색상표.
    static string ColorizePartName(LeaderType part) => $"<color=#{PartColorHex(part)}>{ChallengeManager.PartDisplayName(part)}</color>";

    static string PartColorHex(LeaderType part) => part switch
    {
        LeaderType.Planner => "FFC552",
        LeaderType.Programmer => "A9A9C0",
        LeaderType.Artist => "DBAC7C",
        _ => "FFFFFF"
    };

    void SetChildrenActive(bool value)
    {
        if (missionTextRoot != null) missionTextRoot.SetActive(value);
        if (missionDetailRoot != null) missionDetailRoot.SetActive(value);
        if (successPanel != null && !value) successPanel.SetActive(false);
    }
}
