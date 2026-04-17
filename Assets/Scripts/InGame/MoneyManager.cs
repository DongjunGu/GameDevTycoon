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
        DontDestroyOnLoad(gameObject);
    }

    // 앱 시작 시 호출
    public void LoadMoney(System.Action onComplete = null)
    {
        BackendRetry.Instance.GetMyData("UserMoney", bro =>
        {
            if (bro.IsSuccess())
            {
                var rows = bro.FlattenRows();
                if (rows.Count > 0)
                {
                    JsonData row = rows[0];
                    _gold = SafeInt(row, "gold", 0);
                    _rowInDate = row["inDate"]?.ToString();
                    Debug.Log($"재화 로드 완료: {_gold}G");
                }
                else
                {
                    _gold = 10000;
                    SaveMoney();
                    Debug.Log("신규 유저 초기 재화 지급: 10,000G");
                }
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
    public bool SpendGold(int amount, bool saveImmediately = true)
    {
        if (amount <= 0) return false;
        if (_gold < amount)
        {
            Debug.Log($"재화 부족: 필요 {amount}G / 보유 {_gold}G");
            if (LoanManager.Instance != null && LoanManager.Instance.activeLoans.Count == 0)
            {
                ConfirmUI.Instance?.Show(
                    "돈이 부족합니다.\n대출하시겠습니까?",
                    onConfirm: () => LoanUI.Instance?.Open()
                );
            }
            return false;
        }
        _gold -= amount;
        if (saveImmediately) SaveMoney();
        HUDUI.Instance?.RefreshMoney();
        Debug.Log($"재화 차감: -{amount}G / 잔액: {_gold}G");
        return true;
    }

    // 잔액 확인
    public bool CanAfford(int amount) => _gold >= amount;

    public void SaveMoney()
    {
        Debug.Log($"[MoneyManager] SaveMoney 호출 — 현재 잔액: {_gold:N0}G");
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
    public void ForceSpendGold(int amount, bool saveImmediately = true)
    {
        _gold -= amount;
        if (saveImmediately) SaveMoney();
        HUDUI.Instance?.RefreshMoney();
        Debug.Log($"강제 차감: -{amount:N0}G / 잔액: {_gold:N0}G");
    }






    void Start()
    {
        DialogManager.Instance.OnChoiceResult += HandleDialogResult;
    }

    void HandleDialogResult(string resultType, int resultValue)
    {
        if (resultType == "GoldChange")
        {
            if (resultValue >= 0)
                MoneyManager.Instance.AddGold(resultValue);
            else
                MoneyManager.Instance.SpendGold(-resultValue);

            GameTimeManager.Instance?.SaveGameTime();
            string msg = resultValue >= 0
                ? $"+{resultValue:N0}G 지급됐습니다."
                : $"{resultValue:N0}G 차감됐습니다.";
            ShowAfterDialog(msg);
        }
        if (resultType == "OpenHiring")
        {
            HiringUI.Instance.OpenHiring();
        }
        if (resultType == "SatisfactionChange")
        {
            string empId = DialogManager.Instance.ContextEmployeeId;
            if (!string.IsNullOrEmpty(empId))
            {
                var emp = EmployeeManager.Instance.GetEmployee(empId);
                if (emp != null)
                {
                    emp.satisfaction = UnityEngine.Mathf.Clamp(emp.satisfaction + resultValue, 0, 100);
                    EmployeeManager.Instance.UpdateEmployee(emp);

                    GameTimeManager.Instance?.SaveGameTime();
                    string sign = resultValue >= 0 ? "+" : "";
                    ShowAfterDialog($"{emp.employeeName}의 만족도가 {sign}{resultValue} 변했습니다.\n현재 만족도: {emp.satisfaction}");
                }
            }
        }
    }

    void ShowAfterDialog(string message)
    {
        void OnEnd()
        {
            DialogManager.Instance.OnDialogEnd -= OnEnd;
            AlertUI.Instance.Show(message);
        }
        DialogManager.Instance.OnDialogEnd += OnEnd;
    }
    public void OnTestDialogButton()
    {
        EventDialogTable.PlayManual("event_game_start");        // 단순 진행
    }

    public void OnTestHireDialogButton()
    {
        EventDialogTable.PlayManual("event_first_hire");        // 선택지 + 골드 차감
    }

    public void OnTestProjectDialogButton()
    {
        EventDialogTable.PlayManual("event_project_complete");  // 2단계 분기
    }
}