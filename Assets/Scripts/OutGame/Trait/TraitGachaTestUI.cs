using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 특성 테스트 뽑기 — 차트 전체에서 균등 분포 1장 (가격 무료)
// TODO: 등급별 가중치/통화 차감은 정식 가챠에서 정의 (지금은 균등)
public class TraitGachaTestUI : MonoBehaviour
{
    [Header("References")]
    public Button gachaButton;

    [Tooltip("한 번 클릭에 뽑을 특성 수")]
    public int drawCount = 1;

    void Awake()
    {
        if (gachaButton != null)
        {
            gachaButton.onClick.RemoveListener(OnClickGacha);
            gachaButton.onClick.AddListener(OnClickGacha);
        }
    }

    void OnClickGacha()
    {
        if (OwnedTraitManager.Instance == null) return;

        var cache = TraitChartLoader.Cache;
        if (cache == null || cache.Count == 0)
        {
            Debug.LogWarning("[TraitGacha] 특성 차트가 비어있음");
            return;
        }

        // 균등 분포: traitId 리스트로 평탄화 후 랜덤
        var ids = new List<string>(cache.Keys);

        for (int i = 0; i < drawCount; i++)
        {
            string id = ids[Random.Range(0, ids.Count)];
            bool save = (i == drawCount - 1); // 마지막 한 번만 저장
            OwnedTraitManager.Instance.AddTrait(id, save: save);

            cache.TryGetValue(id, out var row);
            Debug.Log($"[TraitGacha] +{row?.name ?? id} ({row?.grade})");
        }
    }
}
