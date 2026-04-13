using UnityEngine;
using BackEnd;
using LitJson;
using System.Collections;

public class GameTimeManager : MonoBehaviour
{
    public static GameTimeManager Instance { get; private set; }

    public int Year { get; private set; } = 2000;
    public int Month { get; private set; } = 1;
    public int Week { get; private set; } = 1;

    private string _rowInDate = null;
    private float _elapsed = 0f;
    private int _stopCount = 0;
    private bool _isRunning = false;
    private bool _isLoaded = false;

    public bool IsRunning => _isRunning;

    public float secondsPerWeek = 6f;

    public void SetProjectSpeed(ProjectScale scale)
    {
        secondsPerWeek = scale switch
        {
            ProjectScale.Small  => 5f,
            ProjectScale.Medium => 4.2f,
            ProjectScale.Large  => 3.9f,
            _ => 6f
        };
        Debug.Log($"[GameTimeManager] SetProjectSpeed: {scale} → {secondsPerWeek}초/주");
    }

    public void ResetSpeed()
    {
        secondsPerWeek = 6f;
    }

    public System.Action OnTimeChanged; // 시간 변경 시 콜백

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnApplicationPause(bool paused) { }

    void OnApplicationQuit() { }

    void Update()
    {
        if (!_isRunning) return;

        _elapsed += Time.deltaTime;
        if (_elapsed >= secondsPerWeek)
        {
            _elapsed -= secondsPerWeek;
            AdvanceWeek();
        }
    }

    // ── 로드 ──────────────────────────────────
    public void LoadGameTime(System.Action onComplete = null)
    {
        BackendRetry.Instance.GetMyData("UserGameTime", bro =>
        {
            if (bro.IsSuccess())
            {
                var rows = bro.FlattenRows();
                Debug.Log($"UserGameTime rows 수: {rows.Count}");

                if (rows.Count > 0)
                {
                    JsonData row = rows[rows.Count - 1];

                    foreach (var key in row.Keys)
                        Debug.Log($"key: {key} / value: {row[key]}");

                    Year = SafeInt(row, "year", 2000);
                    Month = SafeInt(row, "month", 1);
                    Week = SafeInt(row, "week", 1);
                    _rowInDate = SafeString(row, "inDate", "");
                    _isLoaded = true;

                    Debug.Log($"로드 완료: {Year}년 {Month}월 {Week}주 / rowInDate: {_rowInDate}");
                }
                else
                {
                    Debug.Log("신규 유저, Insert 시작");
                    Year = 2000;
                    Month = 1;
                    Week = 1;
                    _isLoaded = true;
                    SaveGameTime();
                }
            }

            HUDUI.Instance?.RefreshTime();
            onComplete?.Invoke();
        });
    }

    // ── 시간 진행 ─────────────────────────────
    void AdvanceWeek()
    {
        Week++;
        if (Week > 4)
        {
            Week = 1;
            Month++;
            if (Month > 12)
            {
                Month = 1;
                Year++;
                Debug.Log($"[연봉협상] {Year}년 시작 - 연봉협상 발생 예정");
                PayAnnualSalary();

            }
        }

        OnTimeChanged?.Invoke();
        HUDUI.Instance?.RefreshTime();
        LoanManager.Instance.CheckDueLoans();
        //SaveGameTime();

        Debug.Log($"시간 경과: {Year}년 {Month}월 {Week}주");
    }

    // ── 타이머 제어 ───────────────────────────
    public void StartTime()
    {
        _stopCount = Mathf.Max(0, _stopCount - 1);
        if (_stopCount == 0) _isRunning = true;
    }
    public void StopTime()
    {
        _stopCount++;
        _isRunning = false;
    }
    public void ForceStartTime() // 개발 시스템에서 강제 재개 시
    {
        _stopCount = 0;
        _isRunning = true;
    }

    // ── 저장 ──────────────────────────────────
    public void SaveGameTime()
    {
        if (!_isLoaded) return;
        Debug.Log($"저장 시도 - rowInDate: {_rowInDate} / {Year}년 {Month}월 {Week}주");

        var param = new Param();
        param.Add("year", Year);
        param.Add("month", Month);
        param.Add("week", Week);

        if (!string.IsNullOrEmpty(_rowInDate))
        {
            Backend.GameData.UpdateV2("UserGameTime", _rowInDate, Backend.UserInDate, param, bro =>
            {
                if (bro.IsSuccess())
                    Debug.Log($"게임 시간 업데이트 완료: {Year}년 {Month}월 {Week}주");
                else
                    Debug.LogError($"게임 시간 저장 실패: {bro}");
            });
        }
        else
        {
            Backend.GameData.Insert("UserGameTime", param, bro =>
            {
                if (bro.IsSuccess())
                {
                    _rowInDate = bro.GetInDate();
                    Debug.Log($"게임 시간 Insert 완료 / rowInDate: {_rowInDate}");
                }
                else
                {
                    Debug.LogError($"게임 시간 Insert 실패: {bro}");
                }
            });
        }
    }

    public string GetTimeString() => $"{Year}년 {Month}월 {Week}주";

    int SafeInt(JsonData row, string key, int fallback)
    {
        try { return int.Parse(row[key].ToString()); }
        catch { return fallback; }
    }

    string SafeString(JsonData row, string key, string fallback)
    {
        try { return row[key].ToString(); }
        catch { return fallback; }
    }

    public void OnClickSave()
    {
        SaveGameTime();
        ProjectSaveManager.Instance.SaveProject();
        EmployeeManager.Instance?.SaveAllEmployees();
    }
    void PayAnnualSalary()
    {
        int totalSalary = 0;
        foreach (var employee in EmployeeManager.Instance.ownedEmployees)
            totalSalary += employee.salary;
        if (!MoneyManager.Instance.CanAfford(totalSalary))
        {
            GameUIHelper.ShowLoanPrompt();
            return;
        }

        AlertUI.Instance.Show($"새해가 밝았습니다!\n직원들에게 임금을 지급합니다.\n지급액: {totalSalary:N0}G", () =>
        {
            int goldAfter = MoneyManager.Instance.Gold - totalSalary;

            MoneyManager.Instance.ForceSpendGold(totalSalary);
            SaveGameTime();

            if (goldAfter < 0)
            {
                AlertUI.Instance.Show($"파산하셨습니다!\n현재 재화: {goldAfter:N0}G", () =>
                {
                    SaveGameTime();
                    Debug.Log("파산 처리 예정");
                });
            }
            else
            {
                StartCoroutine(StartNegotiationDelay());
            }
        });
    }
    IEnumerator StartNegotiationDelay()
    {
        yield return new WaitForSeconds(2f);
        ForceStartTime();
        SalaryNegotiationManager.Instance.StartNegotiation();
    }
}