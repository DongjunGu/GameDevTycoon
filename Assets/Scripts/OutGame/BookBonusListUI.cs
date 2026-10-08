using System.Collections.Generic;
using TMPro;
using UnityEngine;

// PieceRight 하단 스크롤 보너스 리스트 — 기획/개발/아트 트랙의 레벨 마일스톤(10/20/30/40) 특수효과 표시.
// 전체 마일스톤을 항상 표시하고, 미달성은 흐리게. CEOManager.OnChanged 구독 → 강화 시 자동 Rebuild.
public class BookBonusListUI : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("섹션 헤더 (기획/개발/아트) prefab. TMP_Text 컴포넌트 필요.")]
    public TMP_Text headerPrefab;
    [Tooltip("마일스톤 row prefab. TMP_Text 컴포넌트 필요.")]
    public TMP_Text rowPrefab;

    [Header("Content")]
    [Tooltip("스폰 대상 부모 (VerticalLayoutGroup + ContentSizeFitter 권장).")]
    public Transform contentParent;

    [Tooltip("미달성 마일스톤 row 의 알파")]
    public float lockedAlpha = 0.4f;

    public static readonly int[] Milestones = { 10, 20, 30, 40 };

    // [Part][마일스톤 인덱스] 표시 텍스트.
    public static readonly string[][] BonusTexts =
    {
        new[] { "대표 기획 능력치 +100", "도전과제 달성 시 기획·개발·아트 점수 전부 상승", "도전과제 달성 점수 2배",  "모든 기획 점수 +10%" },
        new[] { "대표 개발 능력치 +100", "90·95·99 존 보너스 1.5배",                       "잭팟 확률 11% → 12%",    "모든 개발 점수 +10%" },
        new[] { "대표 아트 능력치 +100", "가방 퍼펙트 보너스 +10 → +20",                   "창의성 칸당 점수 +5%",   "모든 아트 점수 +10%" },
    };

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

        if (CEOManager.Instance == null || headerPrefab == null || rowPrefab == null || contentParent == null) return;

        SpawnSection("기획", CEOManager.Part.Planning);
        SpawnSection("개발", CEOManager.Part.Develop);
        SpawnSection("아트", CEOManager.Part.Art);
    }

    void SpawnSection(string title, CEOManager.Part part)
    {
        var header = Instantiate(headerPrefab, contentParent);
        header.text = title;
        _spawned.Add(header.gameObject);

        for (int i = 0; i < Milestones.Length; i++)
        {
            var row = Instantiate(rowPrefab, contentParent);
            row.text  = $"Lv.{Milestones[i]}  {BonusTexts[(int)part][i]}";
            row.alpha = CEOManager.HasMilestone(part, Milestones[i]) ? 1f : lockedAlpha;
            _spawned.Add(row.gameObject);
        }
    }
}
