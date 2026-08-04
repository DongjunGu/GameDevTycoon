using UnityEngine;
using UnityEngine.UI;

// 기여도 순위 RowPanel(DevelopmentResultUI.rowPrefab)에 부착 — 1~3등이면 등수별 프레임/왕관을 보여주고,
// 4등 이하는 그대로(등급색 배경 유지 + 왕관 없음) 둔다. DevelopmentResultUI.SetContributionRow가
// GradeSpriteSet.Apply(등급색 배경) 적용 직후 SetRank()를 호출 — 1~3등이면 이 프레임이 등급색을 덮어쓴다.
// frameImage/crownImage는 인스펙터 배선 없이 Awake에서 자동으로 찾는다(같은 프리팹 내부 참조라 자동 탐색이 더 안전).
public class RowRankVisual : MonoBehaviour
{
    [Header("등수별 프레임 스프라이트 (자기 자신의 배경 Image에 적용)")]
    public Sprite goldFrame;
    public Sprite silverFrame;
    public Sprite copperFrame;

    [Header("등수별 왕관 스프라이트")]
    public Sprite goldCrown;
    public Sprite silverCrown;
    public Sprite copperCrown;

    Image _frameImage;
    Image _crownImage;

    void Awake()
    {
        _frameImage = GetComponent<Image>();
        var crownTr = transform.Find("portraitPanel/portraitPanelChild/crownImage");
        if (crownTr != null) _crownImage = crownTr.GetComponent<Image>();
    }

    // rank: 1-based 순위. 1~3등만 프레임(등급색 배경 덮어씀)/왕관 표시, 4등 이하는 프레임은 그대로(등급색
    // 유지) 두고 왕관만 숨긴다.
    public void SetRank(int rank)
    {
        if (_frameImage == null) _frameImage = GetComponent<Image>();
        Sprite frame = rank switch { 1 => goldFrame, 2 => silverFrame, 3 => copperFrame, _ => null };
        Sprite crown = rank switch { 1 => goldCrown, 2 => silverCrown, 3 => copperCrown, _ => null };

        if (_frameImage != null && frame != null) _frameImage.sprite = frame;
        if (_crownImage != null)
        {
            _crownImage.sprite = crown;
            _crownImage.gameObject.SetActive(crown != null);
        }
    }
}
