using System.Collections.Generic;
using UnityEngine;
using BackEnd;
using LitJson;

[System.Serializable]
public class LoanData
{
    public int amount;      // 빌린 금액
    public int year;        // 만기 연도
    public int month;       // 만기 월
    public int week;        // 만기 주
    public string rowInDate;
}

public class LoanManager : MonoBehaviour
{
    public static LoanManager Instance { get; private set; }

    public List<LoanData> activeLoans = new();

    // 대출 단계별 금액
    public static readonly int[] LoanAmounts = { 10000, 20000, 40000, 60000, 100000 };

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 대출 실행 ─────────────────────────────
    public void TakeLoan(int tierIndex)
    {
        int amount = LoanAmounts[tierIndex];

        // 만기일 = 현재 시간 + 1년
        int dueYear = GameTimeManager.Instance.Year + 1;
        int dueMonth = GameTimeManager.Instance.Month;
        int dueWeek = GameTimeManager.Instance.Week;

        var loan = new LoanData
        {
            amount = amount,
            year = dueYear,
            month = dueMonth,
            week = dueWeek,
        };

        SaveLoan(loan, () =>
        {
            activeLoans.Add(loan);
            MoneyManager.Instance.AddGold(amount);
            GameTimeManager.Instance.SaveGameTime();
            LoanUI.Instance.RefreshUI();
            Debug.Log($"대출 실행: {amount:N0}G / 만기: {dueYear}년 {dueMonth}월 {dueWeek}주");
        });
    }

    // ── 만기 체크 (GameTimeManager에서 매주 호출) ──
    public void CheckDueLoans()
    {
        var dueLoans = new List<LoanData>();

        foreach (var loan in activeLoans)
        {
            if (GameTimeManager.Instance.Year == loan.year &&
                GameTimeManager.Instance.Month == loan.month &&
                GameTimeManager.Instance.Week == loan.week)
            {
                dueLoans.Add(loan);
            }
        }

        if (dueLoans.Count == 0) return;

        GameTimeManager.Instance.StopTime();
        ProcessDueLoan(dueLoans, 0);
    }

    void ProcessDueLoan(List<LoanData> dueLoans, int index)
    {
        if (index >= dueLoans.Count)
        {
            GameTimeManager.Instance.StartTime();
            return;
        }

        var loan = dueLoans[index];

        AlertUI.Instance.Show(
            $"대출 만기일입니다.\n{loan.amount:N0}G를 징수하겠습니다.",
            () =>
            {
                int goldAfter = MoneyManager.Instance.Gold - loan.amount;
                MoneyManager.Instance.ForceSpendGold(loan.amount);

                activeLoans.Remove(loan);
                DeleteLoan(loan);
                GameTimeManager.Instance.SaveGameTime();
                
                if (goldAfter < 0)
                {
                    AlertUI.Instance.Show(
                        $"대출을 상환할 자본이 없습니다.\n파산합니다.",
                        () =>
                        {
                            Debug.Log("파산 처리 예정");
                        }
                    );
                    return;
                }

                ProcessDueLoan(dueLoans, index + 1);
            }
        );
    }

    // ── 뒤끝 저장/로드/삭제 ───────────────────
    void SaveLoan(LoanData loan, System.Action onComplete = null)
    {
        var param = new Param();
        param.Add("amount", loan.amount);
        param.Add("year", loan.year);
        param.Add("month", loan.month);
        param.Add("week", loan.week);

        Backend.GameData.Insert("UserLoans", param, bro =>
        {
            if (bro.IsSuccess())
            {
                loan.rowInDate = bro.GetInDate();
                Debug.Log("대출 저장 완료");
                onComplete?.Invoke();
            }
            else
            {
                Debug.LogError($"대출 저장 실패: {bro}");
            }
        });
    }

    void DeleteLoan(LoanData loan)
    {
        if (string.IsNullOrEmpty(loan.rowInDate)) return;

        Backend.GameData.DeleteV2("UserLoans", loan.rowInDate, Backend.UserInDate, bro =>
        {
            if (!bro.IsSuccess())
                Debug.LogError($"대출 삭제 실패: {bro}");
        });
    }

    public void LoadLoans(System.Action onComplete = null)
    {
        Backend.GameData.GetMyData("UserLoans", new Where(), bro =>
        {
            if (!bro.IsSuccess())
            {
                Debug.Log("대출 내역 없음");
                onComplete?.Invoke();
                return;
            }

            var rows = bro.FlattenRows();
            if (rows == null || rows.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            activeLoans.Clear();
            foreach (var row in rows)
            {
                LitJson.JsonData jsonRow = (LitJson.JsonData)row;
                var loan = new LoanData
                {
                    amount = SafeInt(jsonRow, "amount", 0),
                    year = SafeInt(jsonRow, "year", 2000),
                    month = SafeInt(jsonRow, "month", 1),
                    week = SafeInt(jsonRow, "week", 1),
                    rowInDate = SafeString(jsonRow, "inDate", ""),
                };
                activeLoans.Add(loan);
            }

            Debug.Log($"대출 {activeLoans.Count}건 로드");
            onComplete?.Invoke();
        });
    }

    static int SafeInt(JsonData row, string key, int fallback)
    {
        try { return int.Parse(row[key].ToString()); }
        catch { return fallback; }
    }

    static string SafeString(JsonData row, string key, string fallback)
    {
        try { return row[key].ToString(); }
        catch { return fallback; }
    }
}