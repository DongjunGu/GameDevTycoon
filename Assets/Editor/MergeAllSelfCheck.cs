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
            { "A|0|0", 10 }, // 노말 10 → 레어 3 (노말 1 남음) → 에픽 1 → B 낱장과 묶여 A 에픽 1단계
            { "B|2|0", 1 },
            { "C|2|2", 2 },  // 같은 직원 에픽 2단계 2장 → C 유니크
            { "D|3|0", 1 },  // 유니크 낱장 — C 유니크와 묶여 C 유니크 1단계 (empId 순 앞이 메인)
        };
        var touched = OwnedCardManager.MergeAllInto(cards);

        var expected = new Dictionary<string, int> { { "A|0|0", 1 }, { "A|2|1", 1 }, { "C|3|1", 1 } };
        bool ok = cards.Count == expected.Count;
        foreach (var kv in expected) ok &= cards.TryGetValue(kv.Key, out var n) && n == kv.Value;
        ok &= touched.SetEquals(new[] { "A", "B", "C", "D" });

        if (ok) Debug.Log("[MergeAllSelfCheck] PASS");
        else Debug.LogError("[MergeAllSelfCheck] FAIL: " + string.Join(", ", cards));
    }
}
