using UnityEngine;
using BackEnd;
using LitJson;
using System.Collections;

public class GameTimeManager : MonoBehaviour
{
    public static GameTimeManager Instance { get; private set; }

    public const int StartYear = 2000;   // 게임 시작 연도 (연세 등 경과연수 계산 기준)

    public int Year { get; private set; } = StartYear;
    public int Month { get; private set; } = 1;
    public int Week { get; private set; } = 1;

    private string _rowInDate = null;
    private float _elapsed = 0f;
    private int _stopCount = 0;
    private bool _isRunning = false;
    private bool _isLoaded = false;

    // 연간 결제 진행 단계 (영속): 0=없음 / 1=임금 알림 대기(새해, 1월 1주차) / 2=연세 알림 대기(7월 1주차).
    // 임금과 연세는 더 이상 연쇄(같은 시점)가 아니라 서로 다른 시점에 독립적으로 트리거된다.
    // 알림 도중 종료해도 재접속 시 이어서 처리하기 위해 저장한다.
    private int _pendingNewYearStage = 0;
    public int PendingNewYearStage => _pendingNewYearStage;

    public bool IsRunning => _isRunning;

    public float secondsPerWeek = 6f;

    // 개발(Developing) 시계 속도: 전 규모 총 80초로 통일하되 주수(16/24/32)는 유지 → 80/주수
    public void SetProjectSpeed(ProjectScale scale)
    {
        secondsPerWeek = scale switch
        {
            ProjectScale.Small  => 80f / 16f, // 5.0초/주
            ProjectScale.Medium => 80f / 24f, // 3.33초/주
            ProjectScale.Large  => 80f / 32f, // 2.5초/주
            _ => 80f / 16f
        };
        Debug.Log($"[GameTimeManager] SetProjectSpeed: {scale} → {secondsPerWeek}초/주");
    }

    // 디버깅(BugFixing) 시계 속도: 규모 무관 4초/주 고정
    public void SetDebuggingSpeed()
    {
        secondsPerWeek = 4f;
        Debug.Log($"[GameTimeManager] SetDebuggingSpeed → 4초/주");
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
                    _pendingNewYearStage = SafeInt(row, "pendingNewYearStage", 0);
                    _rowInDate = SafeString(row, "inDate", "");
                    _isLoaded = true;

                    int negMonth = SafeInt(row, "negotiationMonth", 0);
                    int negWeek  = SafeInt(row, "negotiationWeek", 0);
                    bool negDone = SafeString(row, "negotiatedThisYear", "False") == "True";
                    SalaryNegotiationManager.Instance?.LoadSchedule(negMonth, negWeek, negDone);

                    EmployeeManager.Instance?.LoadExitStats(
                        SafeInt(row, "yearlyExitCount", 0),
                        SafeInt(row, "exitCountYear", 0)
                    );
                    RandomEventManager.Instance?.LoadHiringPenalty(
                        SafeInt(row, "hiringPenalty", 0),
                        SafeInt(row, "hiringPenaltyEndYear", -1)
                    );
                    RandomEventManager.Instance?.LoadRomanceState(
                        SafeString(row, "activeCoupleIds", ""),
                        SafeString(row, "pendingRomance", "")
                    );
                    RandomEventManager.Instance?.LoadUnstableCompanyWeeksLeft(
                        SafeInt(row, "unstableCompanyWeeksLeft", -1)
                    );
                    RandomEventManager.Instance?.LoadCoffeeRequestWeeksLeft(
                        SafeInt(row, "coffeeRequestWeeksLeft", -1)
                    );
                    RandomEventManager.Instance?.LoadEnergyDrinkRequestWeeksLeft(
                        SafeInt(row, "energyDrinkRequestWeeksLeft", -1)
                    );
                    RandomEventManager.Instance?.LoadYearlyRecoverState(
                        SafeString(row, "yearlySatRecover", "")
                    );
                    RandomEventManager.Instance?.LoadYearlyAllRecoverState(
                        SafeString(row, "yearlyAllRecover", "")
                    );
                    MerchantManager.Instance?.LoadSchedule(
                        SafeString(row, "merchantSchedule", ""),
                        SafeString(row, "merchantItems", ""),
                        SafeInt(row, "merchantVisitedYear", 0)
                    );
                    DispatchManager.Instance?.LoadDispatch(
                        SafeString(row, "dispatchEmployeeId", ""),
                        SafeString(row, "dispatchEmployeeName", ""),
                        SafeString(row, "dispatchDeskId", ""),
                        SafeInt(row, "dispatchWeeksLeft", 0),
                        SafeString(row, "dispatchOutcome", "")
                    );
                    // 데이터만 복원 — 재공개 트리거는 GameScene 진입 후 GameSceneInitializer.Start 에서 처리
                    // (여기는 LoadingScene 단계라 HiringUI 가 아직 없음).
                    EmployeeManager.Instance?.LoadHiringPending(
                        SafeInt(row, "hiringPendingTier", -1),
                        SafeInt(row, "hiringPendingWeeks", 0)
                    );
                    EmployeeManager.Instance?.LoadHiringLists(
                        SafeString(row, "hiringListA", ""),
                        SafeString(row, "hiringListB", "")
                    );
                    MasteryManager.Instance?.LoadSaveString(
                        SafeString(row, "masteryJson", "")
                    );

                    Debug.Log($"로드 완료: {Year}년 {Month}월 {Week}주 / rowInDate: {_rowInDate}");
                    SaveGameTime(); // 신규 컬럼 자동 추가
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
                QuestManager.Instance?.UpdateProgress(QuestType.SurviveYears, 1);
                SalaryNegotiationManager.Instance?.ResetYearFlag();
                HUDUI.Instance?.RefreshYearFee();   // 연세는 해마다 증가 (Year 파생)
                PayAnnualSalary();
            }
        }

        // 연세는 새해가 아니라 7월 1주차에 별도로 차감
        if (Month == 7 && Week == 1)
            StartYearFeeDeduction();

        OnTimeChanged?.Invoke();
        HUDUI.Instance?.RefreshTime();
        LoanManager.Instance.CheckDueLoans();

        // 11~12월 연봉협상 트리거 체크 (롤오버 후 새 주차 기준 — HUD 표시와 일치)
        if (Month == 11 || Month == 12)
            SalaryNegotiationManager.Instance?.CheckNegotiationTrigger(Month, Week);

        Debug.Log($"시간 경과: {Year}년 {Month}월 {Week}주");
    }

    // ── 타이머 제어 ───────────────────────────
    // _isRunning이 변할 때만 발행 (true=정지로 전환, false=재개로 전환)
    public event System.Action<bool> OnTimeStopChanged;

    public void StartTime()
    {
        bool prevRunning = _isRunning;
        _stopCount = Mathf.Max(0, _stopCount - 1);
        if (_stopCount == 0) _isRunning = true;
        if (!prevRunning && _isRunning) OnTimeStopChanged?.Invoke(false);
    }
    public void StopTime()
    {
        bool prevRunning = _isRunning;
        _stopCount++;
        _isRunning = false;
        if (prevRunning && !_isRunning) OnTimeStopChanged?.Invoke(true);
    }
    public void ForceStartTime() // 개발 시스템에서 강제 재개 시
    {
        bool prevRunning = _isRunning;
        _stopCount = 0;
        _isRunning = true;
        if (!prevRunning && _isRunning) OnTimeStopChanged?.Invoke(false);
    }

    // 새 런 시작 — 2000년 1월 1주 + 연관 매니저 상태(임시) 리셋 후 서버 저장
    // 호출 전 SalaryNegotiationManager/RandomEventManager/EmployeeManager 가 ResetForNewRun으로
    // 자기 메모리를 비워둔 상태여야 SaveGameTime 이 빈 값으로 row를 덮어쓴다
    public void ResetForNewRun(System.Action onComplete = null)
    {
        Year = 2000;
        Month = 1;
        Week = 1;
        _elapsed = 0f;
        _stopCount = 0;
        _isRunning = false;
        _pendingNewYearStage = 0;
        _isLoaded = true; // SaveGameTime 가드 통과를 위해 set
        SaveGameTime(onComplete);
    }

    // ── 저장 ──────────────────────────────────
    public void SaveGameTime(System.Action onComplete = null)
    {
        if (!_isLoaded) { onComplete?.Invoke(); return; }
        EmployeeManager.Instance?.SaveAllEmployees();
        Debug.Log($"저장 시도 - rowInDate: {_rowInDate} / {Year}년 {Month}월 {Week}주");

        var param = new Param();
        param.Add("year", Year);
        param.Add("month", Month);
        param.Add("week", Week);
        param.Add("pendingNewYearStage", _pendingNewYearStage);
        param.Add("negotiationMonth",    SalaryNegotiationManager.Instance?.ScheduledMonth ?? 0);
        param.Add("negotiationWeek",     SalaryNegotiationManager.Instance?.ScheduledWeek  ?? 0);
        param.Add("negotiatedThisYear",  SalaryNegotiationManager.Instance?.NegotiatedThisYear ?? false);
        param.Add("yearlyExitCount",      EmployeeManager.Instance?.YearlyExitCount      ?? 0);
        param.Add("exitCountYear",        EmployeeManager.Instance?.ExitCountYear        ?? 0);
        param.Add("hiringPenalty",        RandomEventManager.Instance?.HiringPenalty        ?? 0);
        param.Add("hiringPenaltyEndYear", RandomEventManager.Instance?.HiringPenaltyEndYear ?? -1);
        param.Add("hiringPendingTier",    EmployeeManager.Instance?.HiringPendingTier   ?? -1);
        param.Add("hiringPendingWeeks",   EmployeeManager.Instance?.HiringPendingWeeks  ?? 0);
        param.Add("hiringListA",          EmployeeManager.Instance?.HiringListAJson      ?? "");
        param.Add("hiringListB",          EmployeeManager.Instance?.HiringListBJson      ?? "");
        param.Add("activeCoupleIds",            RandomEventManager.Instance?.GetActiveCoupleString()           ?? "");
        param.Add("pendingRomance",             RandomEventManager.Instance?.GetPendingRomanceString()          ?? "");
        param.Add("unstableCompanyWeeksLeft",   RandomEventManager.Instance?.GetUnstableCompanyWeeksLeft()     ?? -1);
        param.Add("coffeeRequestWeeksLeft",       RandomEventManager.Instance?.GetCoffeeRequestWeeksLeft()         ?? -1);
        param.Add("energyDrinkRequestWeeksLeft",  RandomEventManager.Instance?.GetEnergyDrinkRequestWeeksLeft()    ?? -1);
        param.Add("yearlySatRecover",             RandomEventManager.Instance?.GetYearlyRecoverState()             ?? "-1:-1");
        param.Add("yearlyAllRecover",             RandomEventManager.Instance?.GetYearlyAllRecoverState()          ?? "-1:-1");
        param.Add("merchantSchedule",             MerchantManager.Instance?.GetScheduleString()                     ?? "");
        param.Add("merchantItems",                MerchantManager.Instance?.GetItemsString()                        ?? "");
        param.Add("merchantVisitedYear",          MerchantManager.Instance?.VisitedYear                             ?? 0);
        param.Add("dispatchEmployeeId",           DispatchManager.Instance?.DispatchEmployeeId                      ?? "");
        param.Add("dispatchEmployeeName",         DispatchManager.Instance?.DispatchEmployeeName                    ?? "");
        param.Add("dispatchDeskId",               DispatchManager.Instance?.DispatchDeskId                          ?? "");
        param.Add("dispatchWeeksLeft",            DispatchManager.Instance?.DispatchWeeksLeft                       ?? 0);
        param.Add("dispatchOutcome",              DispatchManager.Instance?.GetOutcomeJson()                        ?? "");
        param.Add("masteryJson",                  MasteryManager.Instance?.GetSaveString()                          ?? "");

        if (!string.IsNullOrEmpty(_rowInDate))
        {
            Backend.GameData.UpdateV2("UserGameTime", _rowInDate, Backend.UserInDate, param, bro =>
            {
                if (bro.IsSuccess())
                    Debug.Log($"게임 시간 업데이트 완료: {Year}년 {Month}월 {Week}주");
                else
                    Debug.LogError($"게임 시간 저장 실패: {bro}");
                onComplete?.Invoke();
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
                onComplete?.Invoke();
            });
        }
    }

    public string GetTimeString() => $"{Year}년 {Month}월 {Week}주차";

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
        MoneyManager.Instance?.SaveMoney();
        ProjectSaveManager.Instance.SaveProject();
        EmployeeManager.Instance?.SaveAllEmployees();
    }
    void PayAnnualSalary()
    {
        // 미결제 단계 = 임금. 알림 도중 종료해도 재접속 시 재발동되도록 진행 연도와 함께 영속화.
        _pendingNewYearStage = 1;
        SaveGameTime();

        int totalSalary = EmployeeManager.Instance.GetTotalSalary();
        // 잔액 충분 여부 무관하게 새해 알림(지불하기) 먼저. 확인 후 자금 부족 분기.
        AlertUI.Instance.ShowMoney(
            "새해가 밝았습니다!\n직원들에게 임금을 지급합니다.",
            -totalSalary,
            () => TryPaySalary(totalSalary)
        );
    }

    // 재접속 복원 — 임금(1월 1주차)/연세(7월 1주차) 알림 도중 종료했던 경우 단계에 맞춰 재발동.
    public void ResumeNewYearPaymentOnReconnect()
    {
        if (_pendingNewYearStage == 1)      PayAnnualSalary();
        else if (_pendingNewYearStage == 2) ProcessYearFeeDeduction();
    }

    // 잔액 충분 → 차감, 부족 → brokeRescue 시도 → 그래도 부족 → 대출 prompt
    void TryPaySalary(int totalSalary)
    {
        if (MoneyManager.Instance.CanAfford(totalSalary))
        {
            ProcessSalaryDeduction(totalSalary);
            return;
        }

        if (TraitEffectApplier.TryConsumeBrokeRescue(out int rescueSalary, out string rescueName))
        {
            AlertUI.Instance.ShowMoney(
                $"가난한 회사 발동!\n{rescueName}의 연봉을 지급합니다.",
                rescueSalary,
                () =>
                {
                    MoneyManager.Instance.AddGold(rescueSalary);
                    TryPaySalary(totalSalary); // 재시도 (재귀 1회 max — brokeRescue 는 한 런당 1회)
                });
            return;
        }

        // [대출 시스템 비활성화] 잔액 부족 → 바로 파산.
        // 대출 복구 시 아래 블록 주석 해제.
        TriggerBankruptcy();
        /*
        // 대출 활성으로 prompt 불가 / 사용자 prompt 거절 → 임금 못 받음 → 파산
        // 대출 받고 닫으면 → 다시 임금 차감 시도. 대출 안 받고 그냥 닫으면 → 파산.
        GameUIHelper.ShowLoanPrompt(
            onDecline: TriggerBankruptcy,
            onClose: didTakeLoan =>
            {
                if (didTakeLoan) TryPaySalary(totalSalary);
                else             TriggerBankruptcy();
            }
        );
        */
    }

    // 다른 파산 소스(랜덤이벤트 선택지 등)도 재사용 — 잔액이 마이너스가 되면 이 메서드로 통일해서 호출한다.
    public void TriggerBankruptcy()
    {
        AlertUI.Instance.Show("임금을 지급할 자본이 없습니다.\n파산합니다.", () =>
        {
            Debug.Log("[파산] PayAnnualSalary → OutGameScene");
            if (RunStateManager.Instance != null)
                RunStateManager.Instance.EndRun(success =>
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene("OutGameScene");
                });
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene("OutGameScene");
        });
    }

    void ProcessSalaryDeduction(int totalSalary)
    {
        int goldAfter = MoneyManager.Instance.Gold - totalSalary;

        MoneyManager.Instance.ForceSpendGold(totalSalary);
        _pendingNewYearStage = 0; // 임금 완료 — 연세는 새해가 아니라 7월 1주차에 별도 처리하므로 여기서 종료
        SaveGameTime();
        ProjectSaveManager.Instance?.SaveProject();

        if (goldAfter < 0)
        {
            AlertUI.Instance.ShowMoney("파산하셨습니다!", goldAfter, () =>
            {
                SaveGameTime();
                MoneyManager.Instance?.SaveMoney();
                ProjectSaveManager.Instance?.SaveProject();
                TriggerBankruptcy();
            });
        }
        // else: 임금 지급 완료. 연세는 7월 1주차에 AdvanceWeek 이 별도로 트리거.
    }

    // 7월 1주차에 진입하면 호출 — 연세(운영비) 차감 단계 시작.
    void StartYearFeeDeduction()
    {
        _pendingNewYearStage = 2; // 연세 알림 대기 (알림 도중 종료 시 재접속 복원용)
        SaveGameTime();
        ProcessYearFeeDeduction();
    }

    // 연세(운영비) 차감 — AlertUI 확인 시 차감 + 4-set 저장. 부족하면 파산.
    void ProcessYearFeeDeduction()
    {
        int yearFee = HUDUI.Instance != null ? HUDUI.Instance.CurrentYearFee : 0;

        AlertUI.Instance.ShowMoney(
            "연세가 차감됩니다.",
            -yearFee,
            () =>
            {
                int goldAfter = MoneyManager.Instance.Gold - yearFee;
                MoneyManager.Instance.ForceSpendGold(yearFee, saveImmediately: false);

                _pendingNewYearStage = 0; // 연세까지 완료 → 새해 결제 종료 (저장 전에 클리어)

                // 4-set 저장 (SaveGameTime 이 SaveAllEmployees 자동 fan-out)
                SaveGameTime();
                MoneyManager.Instance?.SaveMoney();
                ProjectSaveManager.Instance?.SaveProject();

                if (goldAfter < 0)
                {
                    TriggerBankruptcy();
                    return;
                }

                // 새해 후속 처리 (기존 임금 성공 분기에서 이동)
                bool rumorTriggered = RandomEventManager.Instance?.CheckUnstableCompanyOnNewYear(Year) ?? false;
                EmployeeManager.Instance?.ResetYearlyExitCount();
                if (!rumorTriggered)
                    ForceStartTime();
            }
        );
    }
}