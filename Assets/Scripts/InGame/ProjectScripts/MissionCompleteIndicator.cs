using UnityEngine;

// SupriseQuestUI/QuestPanel에 부착 — 도전과제(Challenge)가 성공으로 판정 나면 MissionCompleteImage("달성 완료!"
// 뱃지)를 켠다. ChallengeTestDisplay와 동일하게 이벤트 없이 Update 폴링으로 상태를 읽는다. RollNew로 다음
// 프로젝트에서 새 도전과제가 시작되면 Resolved가 다시 false가 되므로 자동으로 꺼진다.
public class MissionCompleteIndicator : MonoBehaviour
{
    public GameObject missionCompleteImage;

    void Update()
    {
        var c = DevelopmentManager.Instance != null ? DevelopmentManager.Instance.Challenge : null;
        bool show = c != null && c.IsActive && c.Resolved && c.Succeeded;
        if (missionCompleteImage != null && missionCompleteImage.activeSelf != show)
            missionCompleteImage.SetActive(show);
    }
}
