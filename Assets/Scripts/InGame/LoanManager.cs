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
    public int repayAmount;
}

public class LoanManager : MonoBehaviour
{
    [Header("Settings")]
    [Range(0f, 0.5f)]
    public float interestRate = 0.04f;

    public static LoanManager Instance { get; private set; }

    public List<LoanData> activeLoans = new();

    // 대출 단계별 금액
    public static readonly int[] LoanAmounts = { 10000, 20000, 40000, 60000, 100000 };

    // ResetForNewRun 마다 ++. SaveLoan 비동기 Insert 콜백이 reset 이후 늦게 도착해 stale 대출 잔재를 만드는 race 차단.
    private int _runEpoch = 0;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 대출 실행 ─────────────────────────────
    // onComplete: 대출금이 실제로 입금(AddGold)된 후 호출. 임금 재지급 등 "대출 후 차감 재시도"
    //             호출자는 이 콜백에서 재시도해야 함 — SaveLoan(서버 Insert)이 비동기라
    //             TakeLoan 반환 직후에는 아직 잔액이 늘지 않았기 때문(파산 오판 race 방지).
    public void TakeLoan(int tierIndex, System.Action onComplete = null)
    {
        int amount = LoanAmounts[tierIndex];

        // 만기일 = 현재 시간 + 1년
        int dueYear = GameTimeManager.Instance.Year + 1;
        int dueMonth = GameTimeManager.Instance.Month;
        int dueWeek = GameTimeManager.Instance.Week;

        var loan = new LoanData
        {
            amount = amount,
            repayAmount = CalcRepayAmount(amount),
            year = dueYear,
            month = dueMonth,
            week = dueWeek,
        };

        SaveLoan(loan, () =>
        {
            activeLoans.Add(loan);
            MoneyManager.Instance.AddGold(amount);
            GameTimeManager.Instance.SaveGameTime();
            ProjectSaveManager.Instance?.SaveProject();
            LoanUI.Instance.RefreshUI();
            Debug.Log($"대출 실행: {amount:N0}G / 만기: {dueYear}년 {dueMonth}월 {dueWeek}주");
            onComplete?.Invoke();
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
            $"대출 만기일입니다.\n원금: {loan.amount:N0}G\n이자율: {LoanManager.Instance.interestRate * 100:F0}%\n상환금액: {loan.repayAmount:N0}G",
            () =>
            {
                int goldAfter = MoneyManager.Instance.Gold - loan.repayAmount;
                MoneyManager.Instance.ForceSpendGold(loan.repayAmount);

                activeLoans.Remove(loan);
                DeleteLoan(loan);
                GameTimeManager.Instance.SaveGameTime();
                ProjectSaveManager.Instance?.SaveProject();

                if (goldAfter < 0)
                {
                    AlertUI.Instance.Show(
                        $"대출을 상환할 자본이 없습니다.\n파산합니다.",
                        () =>
                        {
                            Debug.Log("[파산] RunState.EndRun → OutGameScene");
                            if (RunStateManager.Instance != null)
                                EndRunAndLoad();
                            else
                                UnityEngine.SceneManagement.SceneManager.LoadScene("OutGameScene");
                        }
                    );
                    return;
                }

                ProcessDueLoan(dueLoans, index + 1);
            }
        );
    }

    // 파산 → RunState.EndRun 저장 후 OutGameScene 직행.
    // 저장 실패 시 씬 진입 금지 (다음 부팅에서 playing=true 로 빈 게임씬 진입 방지).
    // AlertUI 로 재시도 유도.
    void EndRunAndLoad()
    {
        RunStateManager.Instance.EndRun(success =>
        {
            if (success)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("OutGameScene");
                return;
            }
            AlertUI.Instance.Show(
                "진행 상태 저장에 실패했습니다.\n인터넷 상태 확인 후 다시 시도해주세요.",
                EndRunAndLoad
            );
        });
    }

    // ── 뒤끝 저장/로드/삭제 ───────────────────
    void SaveLoan(LoanData loan, System.Action onComplete = null)
    {
        var param = new Param();
        param.Add("amount", loan.amount);
        param.Add("year", loan.year);
        param.Add("month", loan.month);
        param.Add("week", loan.week);
        param.Add("repayAmount", loan.repayAmount);

        int epochAtCall = _runEpoch;
        Backend.GameData.Insert("UserLoans", param, bro =>
        {
            if (!bro.IsSuccess())
            {
                Debug.LogError($"대출 저장 실패: {bro}");
                return;
            }

            string newRowInDate = bro.GetInDate();

            // 콜백 도착 시점이 이미 새 런이면 stale — 서버 row 즉시 삭제.
            if (epochAtCall != _runEpoch)
            {
                Debug.LogWarning($"[Stale] SaveLoan 콜백 무시 (런 변경됨) - 서버 row {newRowInDate} 삭제");
                Backend.GameData.DeleteV2("UserLoans", newRowInDate, Backend.UserInDate, _ => { });
                return;
            }

            loan.rowInDate = newRowInDate;
            Debug.Log("대출 저장 완료");
            onComplete?.Invoke();
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

    // 새 런 시작 — 메모리 클리어 + 서버 직접 fetch 로 모든 row 일괄 삭제 (inFlight Insert race 안전)
    public void ResetForNewRun(System.Action onComplete = null)
    {
        _runEpoch++;
        activeLoans.Clear();

        Backend.GameData.GetMyData("UserLoans", new Where(), bro =>
        {
            if (!bro.IsSuccess())
            {
                Debug.Log($"[Reset] UserLoans fetch 실패/빈 - 삭제 스킵: {bro}");
                onComplete?.Invoke();
                return;
            }

            var rows = bro.FlattenRows();
            if (rows == null || rows.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            var ids = new List<string>();
            foreach (var row in rows)
            {
                string inDate = TryGetInDate((JsonData)row);
                if (!string.IsNullOrEmpty(inDate)) ids.Add(inDate);
            }

            if (ids.Count == 0) { onComplete?.Invoke(); return; }

            int pending = ids.Count;
            foreach (var inDate in ids)
            {
                Backend.GameData.DeleteV2("UserLoans", inDate, Backend.UserInDate, broDel =>
                {
                    if (!broDel.IsSuccess()) Debug.LogError($"[Reset] UserLoans delete 실패: {broDel}");
                    pending--;
                    if (pending == 0) onComplete?.Invoke();
                });
            }
        });
    }

    static string TryGetInDate(JsonData row)
    {
        try { return row["inDate"].ToString(); } catch { return ""; }
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
                    repayAmount = SafeInt(jsonRow, "repayAmount", 0),
                };
                activeLoans.Add(loan);
            }

            Debug.Log($"대출 {activeLoans.Count}건 로드");
            onComplete?.Invoke();
        });
    }

    public int CalcRepayAmount(int principal)
    {
        return Mathf.RoundToInt(principal * (1 + interestRate));
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