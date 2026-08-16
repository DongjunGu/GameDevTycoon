using UnityEngine;
using UnityEngine.UI;

// SupriseQuestUI/QuestPanel, LeaderSelectUI/.../MissionPanel 등 도전과제 미리보기를 여는 버튼에 공용으로 부착.
// 자기 GameObject의 Button.onClick에 MissionAlertUI.Show()를 연결한다.
[RequireComponent(typeof(Button))]
public class OpenMissionAlertOnClick : MonoBehaviour
{
    void Start()
    {
        GetComponent<Button>().onClick.AddListener(() => MissionAlertUI.Instance?.Show());
    }
}
