using UnityEngine;
using TMPro;

// Level4 테스트 전용(QuestItemSimpleforTest1) — 도전과제 시스템의 살아있는 현재 상태를 종류 무관하게 표시.
public class ChallengeTestDisplay : MonoBehaviour
{
    public TextMeshProUGUI descText;

    void Update()
    {
        var dm = DevelopmentManager.Instance;
        var c = dm != null ? dm.Challenge : null;
        if (c == null || !c.IsActive)
        {
            if (descText != null) descText.text = "도전과제 없음";
            return;
        }

        string spriteAsset = ChallengeManager.PartSpriteAsset(c.ChallengePart);
        string spriteName  = ChallengeManager.PartSpriteName(c.ChallengePart);
        string partName    = ChallengeManager.PartDisplayName(c.ChallengePart);
        // 목표는 RollNew(판 시작)에서 이미 확정돼 있으므로(GetBestCandidate 기준) 종류명 대신 실제 목표 점수를 바로 보여준다.
        string label = $"<sprite=\"{spriteAsset}\" name=\"{spriteName}\"> {partName} {Mathf.RoundToInt(c.TargetValue)}점 달성";
        if (c.Resolved) label += c.Succeeded ? " (성공)" : " (실패)";
        if (descText != null) descText.text = label;
    }
}
