using System;
using System.Collections;
using BackEnd;
using LitJson;
using UnityEngine;

// 뒤끝 테이블 "RunState" 단일 row 관리
// 컬럼:
//   playing (boolean)            — 인게임 진행 중 여부 플래그
//   resetInProgress (boolean)    — NewRunInitializer 도중 죽었는지 추적
//   firstSaleConsumed (boolean)  — 초심자의 행운 1회성 발동 플래그
//   brokeRescueFired (boolean)   — 가난한 회사 1회성 발동 플래그
//   tutorial (boolean)           — 이번 런이 스크립트된 튜토리얼 런인지 여부
//
// 흐름:
// - 로그인 직후 LoadAsync 로 모든 플래그 확인
// - LogoScenario 가 ResetInProgress ? 자동재시도 : (IsPlaying ? GameScene : OutGameScene) 분기
// - 아웃→인 진입(NewRunInitializer) 시 SetResetInProgress(true) → reset 끝나면 StartRun() 으로 모든 플래그 일관 갱신
// - StartRun/EndRun 호출 시 firstSaleConsumed/brokeRescueFired/tutorial 도 리셋 (새 런 시작/종료)
// - 1회성 trait 효과 발동 시 SetFirstSaleConsumed(true) / SetBrokeRescueFired(true) 호출
// - tutorial 은 LogoScenario 의 최초 온보딩 진입(NewRunInitializer.StartNewRun(tutorial: true)) 에서만 true 로 시작,
//   그 런이 끝나면(EndRun) 다시 false 로 영구 복귀 — GameScene 의 TutorialController 가 이 값으로 실행 여부 게이트
public class RunStateManager : MonoBehaviour
{
    public static RunStateManager Instance { get; private set; }

    public bool IsPlaying          { get; private set; }
    public bool ResetInProgress    { get; private set; }
    public bool FirstSaleConsumed  { get; private set; }
    public bool BrokeRescueFired   { get; private set; }
    public bool IsTutorial         { get; private set; }

    private string _rowInDate;

    const int MaxRetries = 3;
    const float RetryDelay = 2f;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoadAsync(Action onComplete = null)
    {
        BackendRetry.Instance.GetMyData("RunState", bro =>
        {
            IsPlaying          = false;
            ResetInProgress    = false;
            FirstSaleConsumed  = false;
            BrokeRescueFired   = false;
            IsTutorial         = false;
            _rowInDate         = null;

            if (bro.IsSuccess())
            {
                var rows = bro.FlattenRows();
                if (rows.Count > 0)
                {
                    JsonData row = rows[0];
                    _rowInDate         = row["inDate"]?.ToString();
                    IsPlaying          = ParseBool(row, "playing");
                    ResetInProgress    = ParseBool(row, "resetInProgress");
                    FirstSaleConsumed  = ParseBool(row, "firstSaleConsumed");
                    BrokeRescueFired   = ParseBool(row, "brokeRescueFired");
                    IsTutorial         = ParseBool(row, "tutorial");
                    Debug.Log($"[RunState] 로드: playing={IsPlaying}, resetInProgress={ResetInProgress}, firstSaleConsumed={FirstSaleConsumed}, brokeRescueFired={BrokeRescueFired}, tutorial={IsTutorial}");
                }
                else
                {
                    Save(null); // 신규 — 빈 row 생성 (모든 플래그 false)
                    Debug.Log("[RunState] 신규 row 생성");
                }
            }
            else
            {
                Debug.LogError($"[RunState] 로드 실패: {bro}");
            }
            onComplete?.Invoke();
        });
    }

    public void StartRun(Action<bool> onComplete = null, bool tutorial = false)
    {
        IsPlaying          = true;
        ResetInProgress    = false;
        FirstSaleConsumed  = false;
        BrokeRescueFired   = false;
        IsTutorial         = tutorial;
        Save(onComplete);
    }

    public void EndRun(Action<bool> onComplete = null)
    {
        IsPlaying          = false;
        ResetInProgress    = false;
        FirstSaleConsumed  = false;
        BrokeRescueFired   = false;
        IsTutorial         = false;
        Save(onComplete);
    }

    // 테스트/디버그 전용 — 재로그인 없이 즉시 tutorial 플래그만 토글 (CharacterEventTester 등에서 사용)
    public void SetTutorial(bool value, Action<bool> onComplete = null)
    {
        IsTutorial = value;
        Save(onComplete);
    }

    public void SetResetInProgress(bool value, Action<bool> onComplete = null)
    {
        ResetInProgress = value;
        Save(onComplete);
    }

    public void SetFirstSaleConsumed(bool value, Action<bool> onComplete = null)
    {
        FirstSaleConsumed = value;
        Save(onComplete);
    }

    public void SetBrokeRescueFired(bool value, Action<bool> onComplete = null)
    {
        BrokeRescueFired = value;
        Save(onComplete);
    }

    void Save(Action<bool> onComplete = null)
    {
        StartCoroutine(SaveWithRetry(onComplete, MaxRetries));
    }

    IEnumerator SaveWithRetry(Action<bool> onComplete, int retriesLeft)
    {
        BackendReturnObject result = null;

        var param = new Param();
        param.Add("playing",           IsPlaying);
        param.Add("resetInProgress",   ResetInProgress);
        param.Add("firstSaleConsumed", FirstSaleConsumed);
        param.Add("brokeRescueFired",  BrokeRescueFired);
        param.Add("tutorial",          IsTutorial);

        if (!string.IsNullOrEmpty(_rowInDate))
            Backend.GameData.UpdateV2("RunState", _rowInDate, Backend.UserInDate, param, bro => result = bro);
        else
            Backend.GameData.Insert("RunState", param, bro => result = bro);

        yield return new WaitUntil(() => result != null);

        if (result.IsSuccess())
        {
            if (string.IsNullOrEmpty(_rowInDate))
                _rowInDate = result.GetInDate();
            Debug.Log($"[RunState] Save 성공: playing={IsPlaying}, resetInProgress={ResetInProgress}, firstSaleConsumed={FirstSaleConsumed}, brokeRescueFired={BrokeRescueFired}, tutorial={IsTutorial}");
            onComplete?.Invoke(true);
        }
        else if (retriesLeft <= 0)
        {
            Debug.LogError($"[RunState] Save 최종 실패: {result}");
            onComplete?.Invoke(false);
        }
        else
        {
            Debug.LogWarning($"[RunState] Save 실패, {RetryDelay}초 후 재시도 (남은: {retriesLeft - 1}): {result}");
            yield return new WaitForSeconds(RetryDelay);
            yield return SaveWithRetry(onComplete, retriesLeft - 1);
        }
    }

    static bool ParseBool(JsonData row, string key)
    {
        if (!row.ContainsKey(key)) return false;
        var s = row[key]?.ToString();
        return s == "True" || s == "true" || s == "1";
    }
}
