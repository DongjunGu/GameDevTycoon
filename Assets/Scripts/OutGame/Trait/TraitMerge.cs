using System.Collections.Generic;
using UnityEngine;

// 특성 조합 — 보유 특성 3장을 소모해 새 특성 1장을 얻는다.
// 재료는 A/B/C 등급만 (S 는 재료 불가). 결과 등급은 재료 조합별 확률표로 굴리고,
// 그 등급의 특성 중 하나를 균등 확률로 뽑는다.
public static class TraitMerge
{
    public const int MaterialCount = 3;

    // 키: 재료 등급을 높은 순으로 나열 (AAB 등). 값: 결과 등급 확률(%) — { S, A, B, C } 순, 합 100
    static readonly Dictionary<string, int[]> Rates = new()
    {
        { "AAA", new[] { 10, 90,  0,  0 } },
        { "AAB", new[] {  5, 82, 13,  0 } },
        { "AAC", new[] {  4, 67, 25,  4 } },
        { "ABB", new[] {  3, 67, 30,  0 } },
        { "ABC", new[] {  2, 50, 40,  8 } },
        { "ACC", new[] {  1, 32, 47, 20 } },
        { "BBB", new[] {  0, 60, 40,  0 } },
        { "BBC", new[] {  0, 40, 50, 10 } },
        { "BCC", new[] {  0, 20, 60, 20 } },
        { "CCC", new[] {  0,  1, 69, 30 } },
    };

    static readonly TraitGrade[] ResultOrder = { TraitGrade.S, TraitGrade.A, TraitGrade.B, TraitGrade.C };

    static TraitMerge()
    {
        // 확률표를 고칠 때 합이 100 에서 어긋나면 바로 드러나게
        foreach (var kv in Rates)
        {
            int sum = 0;
            foreach (int r in kv.Value) sum += r;
            Debug.Assert(sum == 100, $"[TraitMerge] {kv.Key} 확률 합이 {sum} (100 이어야 함)");
        }
    }

    // 재료 3장의 등급으로 확률표 조회. 표에 없는 조합(S 포함 등)이면 false. rates = { S, A, B, C } (%)
    public static bool TryGetRates(TraitGrade a, TraitGrade b, TraitGrade c, out int[] rates)
    {
        var g = new[] { a, b, c };
        System.Array.Sort(g, (x, y) => y.CompareTo(x)); // 높은 등급 먼저
        return Rates.TryGetValue($"{g[0]}{g[1]}{g[2]}", out rates);
    }

    // roll: 0~99. 누적 확률로 결과 등급 결정
    public static TraitGrade RollGrade(int[] rates, int roll)
    {
        int acc = 0;
        for (int i = 0; i < ResultOrder.Length; i++)
        {
            acc += rates[i];
            if (roll < acc) return ResultOrder[i];
        }
        return TraitGrade.C;
    }

    // 조합 실행. 같은 특성을 여러 장 넣으려면 같은 id 를 그만큼 넘긴다 (보유 장수가 모자라면 실패).
    // 성공 시 재료 3장 차감 + 결과 1장 지급 + 즉시 저장. 실패 시 아무것도 바뀌지 않는다.
    public static bool TryMerge(string id1, string id2, string id3, out TraitChartRow result, out string reason)
    {
        result = null;
        reason = null;

        var mgr = OwnedTraitManager.Instance;
        var cache = TraitChartLoader.Cache;
        if (mgr == null || cache == null) { reason = "특성 정보를 불러오지 못했습니다."; return false; }

        var ids = new[] { id1, id2, id3 };
        var grades = new TraitGrade[MaterialCount];
        for (int i = 0; i < MaterialCount; i++)
        {
            if (string.IsNullOrEmpty(ids[i]) || !cache.TryGetValue(ids[i], out var row) || row == null)
            {
                reason = "재료 특성 3개를 선택해야 합니다.";
                return false;
            }
            grades[i] = row.grade;
        }

        if (!TryGetRates(grades[0], grades[1], grades[2], out var rates))
        {
            reason = "S등급 특성은 조합 재료로 쓸 수 없습니다.";
            return false;
        }

        var grade = RollGrade(rates, Random.Range(0, 100));
        var pool = new List<TraitChartRow>();
        foreach (var row in cache.Values)
            if (row != null && row.grade == grade) pool.Add(row);
        if (pool.Count == 0) { reason = $"{grade}등급 특성이 차트에 없습니다."; return false; }

        var picked = pool[Random.Range(0, pool.Count)];
        if (!mgr.TryExchange(ids, picked.traitId)) { reason = "보유한 재료 특성이 부족합니다."; return false; }

        result = picked;
        Debug.Log($"[TraitMerge] {grades[0]}{grades[1]}{grades[2]} → {picked.name} ({grade})");
        return true;
    }
}
