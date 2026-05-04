using System;
using UnityEngine;
using BackEnd;
using LitJson;

// 아웃게임 메타 재화 (인게임 G와 별개)
// Backend 테이블: OutGameCurrency { gold:int, diamond:int }
public class OutGameCurrencyManager : MonoBehaviour
{
    public static OutGameCurrencyManager Instance { get; private set; }

    private int _gold = 0;
    private int _diamond = 0;
    private string _rowInDate = null;

    public int Gold    => _gold;
    public int Diamond => _diamond;

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
                    _rowInDate = row["inDate"]?.ToString();
                    Debug.Log($"[OutGameCurrency] 로드: {_gold}G / {_diamond}D");
                }
                else
                {
                    _gold    = 0;
                    _diamond = 0;
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

    public void SaveCurrency()
    {
        var param = new Param();
        param.Add("gold",    _gold);
        param.Add("diamond", _diamond);

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
