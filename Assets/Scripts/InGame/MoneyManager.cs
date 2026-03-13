using UnityEngine;
using BackEnd;
using LitJson;

public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance { get; private set; }

    private int _gold = 0;
    private string _rowInDate = null;

    public int Gold => _gold;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // 앱 시작 시 호출
    public void LoadMoney(System.Action onComplete = null)
    {
        Backend.GameData.GetMyData("UserMoney", new Where(), bro =>
        {
            if (bro.IsSuccess())
            {
                var rows = bro.FlattenRows();
                if (rows.Count > 0)
                {
                    JsonData row = rows[0];
                    _gold       = SafeInt(row, "gold", 0);
                    _rowInDate  = row["inDate"]?.ToString();
                    Debug.Log($"재화 로드 완료: {_gold}G");
                }
                else
                {
                    // 신규 유저 초기 지급
                    _gold = 10000;
                    SaveMoney();
                    Debug.Log("신규 유저 초기 재화 지급: 10,000G");
                }
            }
            else
            {
                Debug.LogError($"재화 로드 실패: {bro}");
            }

            HUDUI.Instance?.RefreshAll();
            onComplete?.Invoke();
        });
    }

    // 재화 지급
    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        _gold += amount;
        SaveMoney();
        HUDUI.Instance?.RefreshMoney();
        Debug.Log($"재화 지급: +{amount}G / 잔액: {_gold}G");
    }

    // 재화 차감
    public bool SpendGold(int amount)
    {
        if (amount <= 0) return false;
        if (_gold < amount)
        {
            Debug.Log($"재화 부족: 필요 {amount}G / 보유 {_gold}G");
            return false;
        }
        _gold -= amount;
        SaveMoney();
        HUDUI.Instance?.RefreshMoney();
        Debug.Log($"재화 차감: -{amount}G / 잔액: {_gold}G");
        return true;
    }

    // 잔액 확인
    public bool CanAfford(int amount) => _gold >= amount;

    void SaveMoney()
    {
        var param = new Param();
        param.Add("gold", _gold);

        if (!string.IsNullOrEmpty(_rowInDate))
        {
            Backend.GameData.UpdateV2("UserMoney", _rowInDate, Backend.UserInDate, param, bro =>
            {
                if (!bro.IsSuccess())
                    Debug.LogError($"재화 저장 실패: {bro}");
            });
        }
        else
        {
            Backend.GameData.Insert("UserMoney", param, bro =>
            {
                if (bro.IsSuccess())
                {
                    _rowInDate = bro.GetInDate();
                    Debug.Log($"재화 Insert 완료");
                }
                else
                {
                    Debug.LogError($"재화 Insert 실패: {bro}");
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