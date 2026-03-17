using UnityEngine;
using BackEnd;
using LitJson;

public class GameTimeManager : MonoBehaviour
{
    public static GameTimeManager Instance { get; private set; }

    public int Year { get; private set; } = 2000;
    public int Month { get; private set; } = 1;
    public int Week { get; private set; } = 1;

    private string _rowInDate = null;
    private float _elapsed = 0f;
    private bool _isRunning = false;

    public float secondsPerWeek = 10f; // 인스펙터에서 조정 가능

    public System.Action OnTimeChanged; // 시간 변경 시 콜백

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

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
        Backend.GameData.GetMyData("UserGameTime", new Where(), bro =>
        {
            if (bro.IsSuccess())
            {
                var rows = bro.FlattenRows();
                Debug.Log($"UserGameTime rows 수: {rows.Count}");

                if (rows.Count > 0)
                {
                    JsonData row = rows[rows.Count - 1];

                    // row 키 전체 출력
                    foreach (var key in row.Keys)
                        Debug.Log($"key: {key} / value: {row[key]}");

                    Year = SafeInt(row, "year", 2000);
                    Month = SafeInt(row, "month", 1);
                    Week = SafeInt(row, "week", 1);
                    _rowInDate = SafeString(row, "inDate", "");

                    Debug.Log($"로드 완료: {Year}년 {Month}월 {Week}주 / rowInDate: {_rowInDate}");
                }
                else
                {
                    Debug.Log("신규 유저, Insert 시작");
                    Year = 2000;
                    Month = 1;
                    Week = 1;
                    SaveGameTime();
                }
            }
            else
            {
                Debug.LogError($"게임 시간 로드 실패: {bro}");
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
            }
        }

        OnTimeChanged?.Invoke();
        HUDUI.Instance?.RefreshTime();
        //SaveGameTime();

        Debug.Log($"시간 경과: {Year}년 {Month}월 {Week}주");
    }

    // ── 타이머 제어 ───────────────────────────
    public void StartTime() => _isRunning = true;
    public void StopTime() => _isRunning = false;
    public void ResumeTime() => _isRunning = true;

    // ── 저장 ──────────────────────────────────
    public void SaveGameTime()
    {
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
    }

}