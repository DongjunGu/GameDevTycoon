using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BackEnd;
using LitJson;

// 상점 패널 — 5 카테고리 (기간한정/뽑기/다이아/골드/패키지). 좌측 카테고리 바, 우측 카테고리별 컨텐츠.
// 현재는 뽑기 카테고리만 구현 — 직원 일반/스페셜, 특성 일반/스페셜 각각 1회/10회 (다이아).
// 뽑기는 뒤끝 펑션 "gacha"(BackendFunction/Gacha/Function.cs)가 서버에서 처리 — 등급 확률은 뒤끝 콘솔 확률 테이블,
// 같은 등급 안에서는 균등, 스페셜 천장(직원 10회=에픽, 특성 30회=S)도 서버. 아래 가격은 표시/사전체크용(서버 상수와 맞출 것).
public class ShopPanelUI : MonoBehaviour
{
    public enum Category { LimitedTime = 0, Gacha = 1, Diamond = 2, Gold = 3, Package = 4 }

    [Header("Category Buttons (인덱스 = Category enum)")]
    public Button[] categoryButtons = new Button[5];
    [Tooltip("선택 시 켜질 노란 강조 GameObject — 각 버튼별.")]
    public GameObject[] categoryHighlights = new GameObject[5];
    [Tooltip("선택 시 활성화될 우측 컨텐츠 — 각 카테고리별.")]
    public GameObject[] categoryContents = new GameObject[5];

    [Tooltip("선택된 카테고리 버튼 프레임 (Shop_Frame_Selected). 선택 시 버튼 자식 아이콘·텍스트는 흰색.")]
    public Sprite categorySelectedSprite;

    [Header("Default")]
    public Category defaultCategory = Category.Gacha;

    // 선택 해제 시 되돌릴 원래 프레임/색 (최초 1회 캐시)
    readonly Dictionary<Image, Sprite> _defaultFrames = new();
    readonly Dictionary<Graphic, Color> _defaultColors = new();

    [Header("Gacha Buttons (뽑기 카테고리)")]
    public Button employeeNormalBtn;
    public Button employeeSpecialBtn;
    [Tooltip("10연차 버튼 — 비워두면 해당 10연차 없음.")]
    public Button employeeNormal10Btn;
    public Button employeeSpecial10Btn;
    public Button traitNormalBtn;
    public Button traitSpecialBtn;
    public Button traitNormal10Btn;
    public Button traitSpecial10Btn;

    [Header("Result")]
    public ShopGachaResultPanelUI resultPanel;

    [Header("Employee Gacha Cost (다이아)")]
    public int normalGachaCost    = 60;
    public int normalGacha10Cost  = 600;
    public int specialGachaCost   = 250;
    public int specialGacha10Cost = 2500;

    [Header("Trait Gacha Cost (다이아)")]
    public int traitNormalCost    = 100;
    public int traitNormal10Cost  = 1000;
    public int traitSpecialCost   = 400;
    public int traitSpecial10Cost = 4000;

    Category _selected;

    void OnEnable()
    {
        WireCategories();
        WireGachaButtons();
        ApplyCategory(defaultCategory);
        WarmUpGacha();
    }

    // 펑션 콜드 스타트(첫 호출 ~6초)를 상점 여는 순간 미리 소모 — 결과는 무시
    static void WarmUpGacha()
    {
        if (!Backend.IsLogin) return;
        var param = new Param();
        param.Add("kind", "ping");
        Backend.BFunc.InvokeFunction("gacha", param, _ => { });
    }

    void WireCategories()
    {
        if (categoryButtons == null) return;
        for (int i = 0; i < categoryButtons.Length; i++)
        {
            int idx = i;
            if (categoryButtons[i] == null) continue;
            categoryButtons[i].onClick.RemoveAllListeners();
            categoryButtons[i].onClick.AddListener(() => ApplyCategory((Category)idx));
        }
    }

    void WireGachaButtons()
    {
        WireBtn(employeeNormalBtn,   () => OnEmployeeGacha(false, 1));
        WireBtn(employeeSpecialBtn,  () => OnEmployeeGacha(true,  1));
        WireBtn(employeeNormal10Btn, () => OnEmployeeGacha(false, 10));
        WireBtn(employeeSpecial10Btn,() => OnEmployeeGacha(true,  10));
        WireBtn(traitNormalBtn,      () => OnTraitGacha(false, 1));
        WireBtn(traitSpecialBtn,     () => OnTraitGacha(true,  1));
        WireBtn(traitNormal10Btn,    () => OnTraitGacha(false, 10));
        WireBtn(traitSpecial10Btn,   () => OnTraitGacha(true,  10));
    }

    void WireBtn(Button b, UnityEngine.Events.UnityAction handler)
    {
        if (b == null) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(handler);
    }

    void ApplyCategory(Category cat)
    {
        _selected = cat;
        for (int i = 0; i < 5; i++)
        {
            bool on = (i == (int)cat);
            if (categoryHighlights != null && i < categoryHighlights.Length && categoryHighlights[i] != null)
                categoryHighlights[i].SetActive(on);
            if (categoryContents != null && i < categoryContents.Length && categoryContents[i] != null)
                categoryContents[i].SetActive(on);
            if (categoryButtons != null && i < categoryButtons.Length && categoryButtons[i] != null)
                SetCategoryVisual(categoryButtons[i], on);
        }
    }

    // 선택: 프레임 교체 + 자식 아이콘·텍스트 흰색 / 해제: 원래 프레임·색 복원
    void SetCategoryVisual(Button button, bool on)
    {
        if (button.TryGetComponent(out Image frame))
        {
            if (!_defaultFrames.ContainsKey(frame)) _defaultFrames[frame] = frame.sprite;
            frame.sprite = on && categorySelectedSprite != null ? categorySelectedSprite : _defaultFrames[frame];
        }
        foreach (var g in button.GetComponentsInChildren<Graphic>(true))
        {
            if (g.gameObject == button.gameObject) continue; // 프레임 자신은 제외
            if (!_defaultColors.ContainsKey(g)) _defaultColors[g] = g.color;
            g.color = on ? Color.white : _defaultColors[g];
        }
    }


    // ──────────── 가챠 (뒤끝 펑션 "gacha") ────────────
    // 다이아 차감·추첨(뒤끝 확률)·천장·지급은 전부 서버에서. 클라는 결과로 메모리만 갱신하고 연출.
    bool _busy;

    void OnEmployeeGacha(bool special, int count)
    {
        int cost = special
            ? (count > 1 ? specialGacha10Cost : specialGachaCost)
            : (count > 1 ? normalGacha10Cost  : normalGachaCost);
        RequestGacha("employee", special, count, cost);
    }

    void OnTraitGacha(bool special, int count)
    {
        int cost = special
            ? (count > 1 ? traitSpecial10Cost : traitSpecialCost)
            : (count > 1 ? traitNormal10Cost  : traitNormalCost);
        RequestGacha("trait", special, count, cost);
    }

    void RequestGacha(string kind, bool special, int count, int cost)
    {
        if (_busy) return; // 응답 전 연타 차단
        var wallet = OutGameCurrencyManager.Instance;
        if (wallet == null || OwnedCardManager.Instance == null || OwnedTraitManager.Instance == null) return;
        if (!CheckDiamond(wallet, cost)) return; // 표시용 사전 체크 — 실제 검증은 서버

        // 디바운스 대기 중인 특성 저장을 먼저 보내서 서버가 최신 row 를 읽게
        OwnedTraitManager.Instance.FlushPendingSave();

        _busy = true;
        float sentAt = Time.realtimeSinceStartup;
        var param = new Param();
        param.Add("kind", kind);
        param.Add("special", special);
        param.Add("count", count);
        Backend.BFunc.InvokeFunction("gacha", param, bro =>
        {
            _busy = false;
            if (this == null) return;
            OnGachaResult(kind, special, count, bro, (Time.realtimeSinceStartup - sentAt) * 1000f);
        });
    }

    void OnGachaResult(string kind, bool special, int count, BackendReturnObject bro, float elapsedMs)
    {
        string label = special ? "스페셜" : "일반";
        JsonData res = ParseFunctionResult(bro);
        Debug.Log($"[Shop] gacha 응답 {elapsedMs:0}ms / 서버구간 {(res != null && res.ContainsKey("ms") ? res["ms"].ToString() : "-")}");
        if (res == null || !res.ContainsKey("ok") || res["ok"].ToString().ToLower() != "true")
        {
            string err = res != null && res.ContainsKey("error") ? res["error"].ToString() : bro.ToString();
            // ponytail: 아웃게임 알림 UI 없음. 실패 팝업 생기면 교체.
            Debug.LogError($"[Shop] {kind} 가챠({label}) x{count} 실패: {err}");
            return;
        }

        OutGameCurrencyManager.Instance.ApplyServerState(
            ToInt(res["diamond"]), ToInt(res["pityEmployeeSpecial"]), ToInt(res["pityTraitSpecial"]));

        var draws = res["draws"];
        if (kind == "employee")
        {
            var pool = EmployeeManager.Instance?.poolEmployees;
            var gained = new List<(string, EmployeeGrade)>();
            var shown  = new List<(EmployeeData emp, EmployeeGrade grade)>();
            for (int i = 0; i < draws.Count; i++)
            {
                string id = draws[i]["id"].ToString();
                var grade = (EmployeeGrade)ToInt(draws[i]["grade"]);
                gained.Add((id, grade));
                var emp = pool?.Find(e => e.id == id);
                if (emp != null) shown.Add((emp, grade));
                Debug.Log($"[Shop] 직원 가챠({label}) → {emp?.employeeName ?? id} ({grade})");
            }
            OwnedCardManager.Instance.ApplyServerCards(res["cardsJson"].ToString(), gained);
            ShowDrawsSequential(shown, 0);
        }
        else
        {
            var cache = TraitChartLoader.Cache;
            var shown = new List<TraitChartRow>();
            for (int i = 0; i < draws.Count; i++)
            {
                string id = draws[i]["id"].ToString();
                TraitChartRow row = null;
                if (cache != null && cache.TryGetValue(id, out row)) shown.Add(row);
                Debug.Log($"[Shop] 특성 가챠({label}) → {row?.name ?? id}");
            }
            OwnedTraitManager.Instance.ApplyServerOwned(res["ownedJson"].ToString());
            ShowTraitsSequential(shown, 0);
        }
        Debug.Log($"[Shop] {kind} 가챠({label}) x{count} 완료 / 잔액 {OutGameCurrencyManager.Instance.Diamond}D");
    }

    // 펑션 반환값: StringToStream 으로 보낸 JSON 문자열이 {"result":"..."} 로 감싸져 올 수 있음 → 풀어서 반환
    static JsonData ParseFunctionResult(BackendReturnObject bro)
    {
        if (bro == null || !bro.IsSuccess()) return null;
        try
        {
            JsonData rv = bro.GetReturnValuetoJSON();
            if (rv != null && rv.IsObject && rv.ContainsKey("result") && rv["result"].IsString)
                return JsonMapper.ToObject(rv["result"].ToString());
            return rv;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Shop] 가챠 응답 파싱 실패: {e.Message} / {bro}");
            return null;
        }
    }

    static int ToInt(JsonData v) => int.TryParse(v?.ToString(), out var n) ? n : 0;

    bool CheckDiamond(OutGameCurrencyManager wallet, int cost)
    {
        if (wallet.CanAffordDiamond(cost)) return true;
        // ponytail: 아웃게임 알림 UI 없음. 다이아 부족 팝업 생기면 교체.
        Debug.LogWarning($"[Shop] 다이아 부족 — 필요 {cost}D / 보유 {wallet.Diamond}D");
        return false;
    }

    // 닫기 버튼을 누를 때마다 다음 장. 1회 뽑기도 같은 경로.
    void ShowDrawsSequential(List<(EmployeeData emp, EmployeeGrade grade)> draws, int index)
    {
        if (resultPanel == null || index >= draws.Count) return;
        resultPanel.onClosed = () => ShowDrawsSequential(draws, index + 1);
        resultPanel.ShowEmployee(draws[index].emp, draws[index].grade);
    }

    void ShowTraitsSequential(List<TraitChartRow> draws, int index)
    {
        if (resultPanel == null || index >= draws.Count) return;
        resultPanel.onClosed = () => ShowTraitsSequential(draws, index + 1);
        resultPanel.ShowTrait(draws[index]);
    }
}
