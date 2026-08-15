using UnityEngine;
using UnityEngine.UI;
using TMPro;

// LeaderSelectUI/.../LSRightPanel/MissionPanel 부착 — 지금 보고 있는 팀장점수 화면이 이번 프로젝트
// 도전과제의 도전 파트와 같을 때만 목표를 보여준다("N점 이상 달성", 진행률 표시 없음). 달성 시 SuccessPanel 활성화.
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
    public TextMeshProUGUI missionDetailText;

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
        if (!active)
        {
            SetChildrenActive(false);
            return;
        }

        // 지금 보고 있는 팀장점수 화면의 파트가 도전 파트와 다르면 아무것도 안 보여준다.
        bool relevant = dm.CurrentLeaderScoreType.HasValue && dm.CurrentLeaderScoreType.Value == c.ChallengePart;
        SetChildrenActive(relevant);
        if (!relevant) return;

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

        if (missionDetailText != null)
            missionDetailText.text = $"점수 {Mathf.RoundToInt(c.TargetValue)}점 이상 달성";

        if (successPanel != null)
            successPanel.SetActive(c.Resolved && c.Succeeded);
    }

    void SetChildrenActive(bool value)
    {
        if (missionTextRoot != null) missionTextRoot.SetActive(value);
        if (missionDetailRoot != null) missionDetailRoot.SetActive(value);
        if (successPanel != null && !value) successPanel.SetActive(false);
    }
}
