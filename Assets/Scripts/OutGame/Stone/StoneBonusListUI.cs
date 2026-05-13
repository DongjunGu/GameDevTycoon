using System.Collections.Generic;
using TMPro;
using UnityEngine;

// PieceRight 하단 스크롤 보너스 리스트 — 활성 돌의 기획/개발/아트 단계별 도달 보너스 표시.
// 도달된 단계만 row 생성. CEOManager.OnChanged 구독 → 강화/스위치 시 자동 Rebuild.
//
// 각 row 의 텍스트는 인게임 효과 적용 TODO. 표시 텍스트만.
public class StoneBonusListUI : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("섹션 헤더 (기획/개발/아트) prefab. TMP_Text 컴포넌트 필요.")]
    public TMP_Text headerPrefab;
    [Tooltip("단계별 보너스 row prefab. TMP_Text 컴포넌트 필요.")]
    public TMP_Text rowPrefab;

    [Header("Content")]
    [Tooltip("스폰 대상 부모 (VerticalLayoutGroup + ContentSizeFitter 권장).")]
    public Transform contentParent;

    readonly List<GameObject> _spawned = new();
    bool _subscribed;

    void OnEnable()
    {
        Subscribe();
        Rebuild();
    }

    void OnDisable() => Unsubscribe();

    void Subscribe()
    {
        if (_subscribed || CEOManager.Instance == null) return;
        CEOManager.Instance.OnChanged += Rebuild;
        _subscribed = true;
    }

    void Unsubscribe()
    {
        if (!_subscribed || CEOManager.Instance == null) return;
        CEOManager.Instance.OnChanged -= Rebuild;
        _subscribed = false;
    }

    public void Rebuild()
    {
        foreach (var go in _spawned) if (go != null) Destroy(go);
        _spawned.Clear();

        var ceo = CEOManager.Instance;
        if (ceo == null || !ceo.HasActive) return;

        SpawnSection("기획", ceo.PlanningStage, StatKind.Planning);
        SpawnSection("개발", ceo.DevelopStage, StatKind.Develop);
        SpawnSection("아트",  ceo.ArtStage,     StatKind.Art);
    }

    // 1~7 단계는 능력치 수치에 가산되어 PieceRight 상단 stat row 에 반영됨 (CEOManager.GetPlanning 등).
    // 8/9/10 단계만 카테고리 보너스라 row 로 표시.
    void SpawnSection(string title, int stage, StatKind kind)
    {
        if (stage < 8 || headerPrefab == null || rowPrefab == null || contentParent == null) return;

        var header = Instantiate(headerPrefab, contentParent);
        header.text = title;
        _spawned.Add(header.gameObject);

        int end = Mathf.Min(stage, 10);
        for (int i = 8; i <= end; i++)
        {
            var row = Instantiate(rowPrefab, contentParent);
            row.text = $"{i}단계  {GetBonusText(kind, i)}";
            _spawned.Add(row.gameObject);
        }
    }

    enum StatKind { Planning, Develop, Art }

    // 8/9/10 단계 카테고리 보너스 텍스트. 인게임 효과 적용은 TODO.
    static string GetBonusText(StatKind kind, int stage)
    {
        switch (kind)
        {
            case StatKind.Planning:
                if (stage == 8)  return "무료 강화권 1장 제공";
                if (stage == 9)  return "모든 기획 직원 기획 능력치 +100";
                if (stage == 10) return "매년 총 연봉의 5% 보조금 획득";
                break;
            case StatKind.Develop:
                if (stage == 8)  return "1년마다 커피 제공";
                if (stage == 9)  return "모든 개발 직원 개발 능력치 +100";
                if (stage == 10) return "매년 연봉 10% 할인";
                break;
            case StatKind.Art:
                if (stage == 8)  return "창의성 랜덤 블록 1개 추가 획득";
                if (stage == 9)  return "모든 아트 직원 아트 능력치 +100";
                if (stage == 10) return "게임 매출 3% 증가";
                break;
        }
        return "";
    }
}
