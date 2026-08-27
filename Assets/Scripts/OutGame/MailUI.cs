using System.Collections;
using System.Collections.Generic;
using BackEnd;
using LitJson;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// OutGameScene MailPanel — 뒤끝 관리자 우편(PostType.Admin) 목록 조회 + 일괄 수령.
//
// [뒤끝 콘솔 설정]
//  1) CDN 차트 "MailReward" 업로드 (컬럼: mailRewardId, type, amount, description) — MailReward_Chart.csv 참고.
//  2) 우편 발송 시 아이템 "이름"에 mailRewardId(예: "gold_5000") 입력. 커스텀 JSON 불필요.
//     실제 지급 내용은 전부 MailRewardChartLoader.Cache 가 정의한다.
//     우편 chargeAmount 가 2 이상이면 amount × chargeAmount 로 배수 지급.
//
// [동작]
//  - MailPanel이 켜질 때(OnEnable) 목록 조회 → statusText에 "받을 우편 N개" / "없습니다"
//  - receiveButton 클릭 → 우편을 한 건씩 ReceivePostItem → mailRewardId를 차트에서 조회해 OutGameCurrencyManager에 반영
//  - 보상은 뒤끝이 자동 지급하지 않으므로 여기서 직접 지급하고, AddGold/AddDiamond가 SaveCurrency까지 처리.
[DisallowMultipleComponent]
public class MailUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button receiveButton;

    private struct Reward { public string type; public int amount; }
    private readonly List<(string inDate, List<Reward> rewards)> _posts = new();
    private bool _busy;

    void Awake()
    {
        if (receiveButton != null)
        {
            receiveButton.onClick.RemoveListener(OnClickReceiveAll);
            receiveButton.onClick.AddListener(OnClickReceiveAll);
        }
    }

    void OnEnable()
    {
        if (!_busy) StartCoroutine(RefreshCo());
    }

    // ── 목록 조회 ────────────────────────────────────────────────
    IEnumerator RefreshCo()
    {
        _busy = true;
        SetStatus("우편 확인 중...");
        if (receiveButton != null) receiveButton.interactable = false;

        BackendReturnObject bro = null;
        Backend.UPost.GetPostList(PostType.Admin, r => bro = r);
        yield return new WaitUntil(() => bro != null);

        _posts.Clear();
        if (!bro.IsSuccess())
        {
            SetStatus("우편을 불러오지 못했습니다.");
            _busy = false;
            yield break;
        }

        try { Debug.Log($"[MailUI] GetPostList RAW = {bro.GetReturnValuetoJSON()?.ToJson()}"); } catch { }
        ParsePostList(bro);

        int n = _posts.Count;
        SetStatus(n == 0 ? "받을 우편이 없습니다." : $"받을 우편 {n}개");
        if (receiveButton != null) receiveButton.interactable = n > 0;
        _busy = false;
    }

    void ParsePostList(BackendReturnObject bro)
    {
        JsonData list = null;
        try
        {
            JsonData root = bro.GetReturnValuetoJSON();
            if (root != null && root.ContainsKey("postList")) list = root["postList"];
        }
        catch { }
        if (list == null || !list.IsArray)
        {
            try { list = bro.FlattenRows(); } catch { list = null; }
        }
        if (list == null || !list.IsArray) return;

        for (int i = 0; i < list.Count; i++)
        {
            JsonData post = list[i];
            string inDate = SafeStr(post, "inDate");
            if (string.IsNullOrEmpty(inDate)) continue;
            _posts.Add((inDate, ParseRewards(post)));
        }
    }

    // 우편 아이템의 "이름"(mailRewardId) 을 MailReward 차트에서 조회해 실제 보상으로 변환.
    List<Reward> ParseRewards(JsonData post)
    {
        var result = new List<Reward>();
        JsonData items = null;
        try { if (post.ContainsKey("items")) items = post["items"]; } catch { }
        if (items == null || !items.IsArray)
        {
            Debug.LogWarning($"[MailUI] post에 items 배열 없음 — post={post?.ToJson()}");
            return result;
        }

        var chart = MailRewardChartLoader.Cache;

        for (int i = 0; i < items.Count; i++)
        {
            JsonData it = items[i];
            int count = SafeInt(it, "itemCount", SafeInt(it, "chargeAmount", SafeInt(it, "count", 1)));
            if (count < 1) count = 1;

            // 우편 발송 시 차트(RewardChart = MailReward_Chart.csv)를 붙이면 뒤끝이 그 row 전체를
            // it["item"] 에 객체로 임베드해 준다 → type/amount 를 바로 읽는다.
            // 혹시 인라인 값이 없으면 mailRewardId 로 클라 차트(MailRewardChartLoader)를 폴백 조회.
            JsonData data = null;
            try { if (it.ContainsKey("item")) data = it["item"]; } catch { }
            if (data == null || !data.IsObject) data = it;

            string type   = SafeStr(data, "type");
            int    amount = SafeInt(data, "amount", 0);

            if (string.IsNullOrEmpty(type) || amount <= 0)
            {
                string rid = SafeStr(data, "mailRewardId");
                if (string.IsNullOrEmpty(rid)) rid = SafeStr(it, "item");
                if (!string.IsNullOrEmpty(rid) && chart.TryGetValue(rid, out var row) && row != null)
                {
                    if (string.IsNullOrEmpty(type)) type = row.type;
                    if (amount <= 0) amount = row.amount;
                }
            }

            if (string.IsNullOrEmpty(type) || amount <= 0)
            {
                Debug.LogWarning($"[MailUI] 보상 해석 실패 — type/amount 없음. item RAW={it?.ToJson()}");
                continue;
            }

            int finalAmount = amount * count;
            Debug.Log($"[MailUI] 보상 해석: type={type} amount={amount}×{count}={finalAmount}");
            result.Add(new Reward { type = type.Trim().ToLowerInvariant(), amount = finalAmount });
        }
        return result;
    }

    // ── 일괄 수령 ────────────────────────────────────────────────
    void OnClickReceiveAll()
    {
        if (_busy || _posts.Count == 0) return;
        StartCoroutine(ReceiveAllCo());
    }

    IEnumerator ReceiveAllCo()
    {
        _busy = true;
        if (receiveButton != null) receiveButton.interactable = false;
        SetStatus("우편 수령 중...");

        int totalGold = 0, totalDiamond = 0, ok = 0, fail = 0;
        var snapshot = new List<(string inDate, List<Reward> rewards)>(_posts);

        foreach (var p in snapshot)
        {
            BackendReturnObject bro = null;
            Backend.UPost.ReceivePostItem(PostType.Admin, p.inDate, r => bro = r);
            yield return new WaitUntil(() => bro != null);

            if (!bro.IsSuccess()) { Debug.LogWarning($"[MailUI] ReceivePostItem 실패: {bro}"); fail++; continue; }
            ok++;
            Debug.Log($"[MailUI] 수령 성공 inDate={p.inDate} 보상 {p.rewards.Count}건 / CurrencyMgr={(OutGameCurrencyManager.Instance != null ? "OK" : "NULL")}");
            foreach (var rw in p.rewards)
            {
                if (rw.type == "gold")    { OutGameCurrencyManager.Instance?.AddGold(rw.amount);    totalGold += rw.amount; }
                else if (rw.type == "diamond") { OutGameCurrencyManager.Instance?.AddDiamond(rw.amount); totalDiamond += rw.amount; }
                else Debug.LogWarning($"[MailUI] 미지원 보상 type='{rw.type}' amount={rw.amount} — 무시");
            }
        }

        var sb = new System.Text.StringBuilder();
        if (ok > 0)
        {
            sb.Append($"우편 {ok}개 수령");
            if (totalGold > 0)    sb.Append($" · 골드 +{totalGold:N0}");
            if (totalDiamond > 0) sb.Append($" · 다이아 +{totalDiamond:N0}");
        }
        else sb.Append("수령한 우편이 없습니다.");
        if (fail > 0) sb.Append($" (실패 {fail})");
        SetStatus(sb.ToString());

        _busy = false;
        yield return RefreshCo();
    }

    // ── 헬퍼 ────────────────────────────────────────────────────
    void SetStatus(string s) { if (statusText != null) statusText.text = s; }

    static string SafeStr(JsonData d, string k)
    {
        try { return d != null && d.ContainsKey(k) && d[k] != null ? d[k].ToString() : ""; }
        catch { return ""; }
    }

    static int SafeInt(JsonData d, string k, int fallback)
    {
        try { return d != null && d.ContainsKey(k) && int.TryParse(d[k].ToString(), out int v) ? v : fallback; }
        catch { return fallback; }
    }
}
