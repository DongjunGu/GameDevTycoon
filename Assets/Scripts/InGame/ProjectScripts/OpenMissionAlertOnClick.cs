using UnityEngine;
using UnityEngine.UI;

// SupriseQuestUI/QuestPanel, LeaderSelectUI/.../MissionPanel 등 도전과제 미리보기를 여는 버튼에 공용으로 부착.
// 자기 GameObject의 Button.onClick에 MissionAlertUI.Show()를 연결한다.
[RequireComponent(typeof(Button))]
public class OpenMissionAlertOnClick : MonoBehaviour
{
    [Tooltip("MissionAlertUI의 CurrentScorePanel(3파트 현재 점수) 노출 여부 — MissionPanel은 true, QuestPanel은 false로 설정")]
    public bool showCurrentScorePanel = true;

    void Start()
    {
        GetComponent<Button>().onClick.AddListener(() => MissionAlertUI.Instance?.Show(null, showCurrentScorePanel));
    }
}
