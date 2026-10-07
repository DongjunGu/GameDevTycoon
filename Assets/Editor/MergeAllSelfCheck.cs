using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// OwnedCardManager.MergeAllInto (일괄 합성 순수 로직) 자체 검증. 키 = "empId|grade|stage"
public static class MergeAllSelfCheck
{
    [MenuItem("Tools/GameDevTycoon/Self Check/Merge All")]
    public static void Run()
    {
        var cards = new Dictionary<string, int>
        {
            { "A|0|0", 10 }, // 노말 10 → 레어 3 (노말 1 남음) → 에픽 1. 일괄 합성은 여기까지
            { "B|2|0", 1 },  // 에픽 낱장 — A 에픽과 묶이지 않고 그대로 (에픽 1단계부터는 일괄 합성 제외)
            { "C|2|2", 2 },  // 에픽 2단계 2장 — 유니크로 올라가지 않고 그대로
            { "D|3|0", 1 },  // 유니크 낱장 — 그대로
        };
        var touched = OwnedCardManager.MergeAllInto(cards);

        var expected = new Dictionary<string, int>
        {
            { "A|0|0", 1 }, { "A|2|0", 1 }, { "B|2|0", 1 }, { "C|2|2", 2 }, { "D|3|0", 1 },
        };
        bool ok = cards.Count == expected.Count;
        foreach (var kv in expected) ok &= cards.TryGetValue(kv.Key, out var n) && n == kv.Value;
        ok &= touched.SetEquals(new[] { "A" });

        if (ok) Debug.Log("[MergeAllSelfCheck] PASS");
        else Debug.LogError("[MergeAllSelfCheck] FAIL: " + string.Join(", ", cards));
    }
}
