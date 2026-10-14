using System;
using UnityEngine;
using BackEnd;
using LitJson;

// 아웃게임 메타 재화 (인게임 G와 별개)
// Backend 테이블: OutGameCurrency { gold:int, diamond:int, bookPlanning:int, bookDevelop:int, bookArt:int,
//                                   pityEmployeeSpecial:int, pityTraitSpecial:int }
public class OutGameCurrencyManager : MonoBehaviour
{
    public static OutGameCurrencyManager Instance { get; private set; }

    // 인덱스 = CEOManager.Part (0 기획 / 1 개발 / 2 아트)
    static readonly string[] BookColumns = { "bookPlanning", "bookDevelop", "bookArt" };

    private int _gold = 0;
    private int _diamond = 0;
    private readonly int[] _books = new int[3];
    private string _rowInDate = null;

    public int Gold    => _gold;
    public int Diamond => _diamond;
    public int GetBook(CEOManager.Part part) => _books[(int)part];

    // 상점 스페셜 뽑기 천장 카운트 (최고 등급 미당첨 연속 횟수). ShopPanelUI 가 갱신, 다음 SaveCurrency 때 같이 저장.
    public int pityEmployeeSpecial;
    public int pityTraitSpecial;

    public event Action OnChanged;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // 앱 시작 시 호출 (BackendManager.LoadAllAndEnterGame 체인)
    public void LoadCurrency(System.Action onComplete = null)
    {
        BackendRetry.Instance.GetMyData("OutGameCurrency", bro =>
        {
            if (bro.IsSuccess())
            {
                var rows = bro.FlattenRows();
                if (rows.Count > 0)
                {
                    JsonData row = rows[0];
                    _gold      = SafeInt(row, "gold", 0);
                    _diamond   = SafeInt(row, "diamond", 0);
                    for (int i = 0; i < _books.Length; i++) _books[i] = SafeInt(row, BookColumns[i], 0);
                    pityEmployeeSpecial = SafeInt(row, "pityEmployeeSpecial", 0);
                    pityTraitSpecial    = SafeInt(row, "pityTraitSpecial", 0);
                    _rowInDate = row["inDate"]?.ToString();
                    Debug.Log($"[OutGameCurrency] 로드: {_gold}G / {_diamond}D");
                }
                else
                {
                    _gold    = 0;
                    _diamond = 0;
                    Array.Clear(_books, 0, _books.Length);
                    pityEmployeeSpecial = pityTraitSpecial = 0;
                    SaveCurrency();
                    Debug.Log("[OutGameCurrency] 신규 유저 초기화 (0/0)");
                }
            }
            else
            {
                Debug.LogError($"[OutGameCurrency] 로드 실패: {bro}");
            }

            OnChanged?.Invoke();
            onComplete?.Invoke();
        });
    }

    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        _gold += amount;
        SaveCurrency();
        OnChanged?.Invoke();
        Debug.Log($"[OutGameCurrency] +{amount}G / 잔액 {_gold}G");
    }

    public bool SpendGold(int amount, bool saveImmediately = true)
    {
        if (amount <= 0) return false;
        if (_gold < amount) return false;
        _gold -= amount;
        if (saveImmediately) SaveCurrency();
        OnChanged?.Invoke();
        Debug.Log($"[OutGameCurrency] -{amount}G / 잔액 {_gold}G");
        return true;
    }

    public bool CanAffordGold(int amount) => _gold >= amount;

    public void AddDiamond(int amount)
    {
        if (amount <= 0) return;
        _diamond += amount;
        SaveCurrency();
        OnChanged?.Invoke();
        Debug.Log($"[OutGameCurrency] +{amount}D / 잔액 {_diamond}D");
    }

    public bool SpendDiamond(int amount, bool saveImmediately = true)
    {
        if (amount <= 0) return false;
        if (_diamond < amount) return false;
        _diamond -= amount;
        if (saveImmediately) SaveCurrency();
        OnChanged?.Invoke();
        Debug.Log($"[OutGameCurrency] -{amount}D / 잔액 {_diamond}D");
        return true;
    }

    public bool CanAffordDiamond(int amount) => _diamond >= amount;

    // 책 — CEO 강화 재화, 트랙(기획/개발/아트)별로 따로 보유 (CEOManager.TryUpgrade)
    public void AddBook(CEOManager.Part part, int amount)
    {
        if (amount <= 0) return;
        _books[(int)part] += amount;
        SaveCurrency();
        OnChanged?.Invoke();
        Debug.Log($"[OutGameCurrency] +{amount} {part}책 / 잔액 {_books[(int)part]}");
    }

    public bool SpendBook(CEOManager.Part part, int amount, bool saveImmediately = true)
    {
        if (amount <= 0) return false;
        if (_books[(int)part] < amount) return false;
        _books[(int)part] -= amount;
        if (saveImmediately) SaveCurrency();
        OnChanged?.Invoke();
        Debug.Log($"[OutGameCurrency] -{amount} {part}책 / 잔액 {_books[(int)part]}");
        return true;
    }

    public void SaveCurrency()
    {
        var param = new Param();
        param.Add("gold",    _gold);
        param.Add("diamond", _diamond);
        for (int i = 0; i < _books.Length; i++) param.Add(BookColumns[i], _books[i]);
        param.Add("pityEmployeeSpecial", pityEmployeeSpecial);
        param.Add("pityTraitSpecial",    pityTraitSpecial);

        if (!string.IsNullOrEmpty(_rowInDate))
        {
            Backend.GameData.UpdateV2("OutGameCurrency", _rowInDate, Backend.UserInDate, param, bro =>
            {
                if (!bro.IsSuccess())
                    Debug.LogError($"[OutGameCurrency] 저장 실패: {bro}");
            });
        }
        else
        {
            Backend.GameData.Insert("OutGameCurrency", param, bro =>
            {
                if (bro.IsSuccess())
                {
                    _rowInDate = bro.GetInDate();
                    Debug.Log("[OutGameCurrency] Insert 완료");
                }
                else
                {
                    Debug.LogError($"[OutGameCurrency] Insert 실패: {bro}");
                }
            });
        }
    }

    int SafeInt(JsonData row, string key, int fallback)
    {
        if (row.ContainsKey(key) && int.TryParse(row[key]?.ToString(), out int val))
            return val;
        return fallback;
    }
}
