using System;
using BackEnd;
using LitJson;
using UnityEngine;

// 뒤끝 테이블 "RunState" 단일 row 관리
// 컬럼: playing (boolean) — 인게임 진행 중 여부 플래그
//
// 흐름:
// - 로그인 직후 LoadAsync 로 IsPlaying 확인
// - LogoScenario 가 IsPlaying ? GameScene : OutGameScene 분기
// - 아웃→인 진입(NewRunInitializer 종료) 시 StartRun() → playing=true
// - 인게임 파산 감지 시 EndRun() → playing=false → 다음 로딩에서 아웃게임 분기
public class RunStateManager : MonoBehaviour
{
    public static RunStateManager Instance { get; private set; }

    public bool IsPlaying { get; private set; }
    private string _rowInDate;

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
            IsPlaying = false;
            _rowInDate = null;

            if (bro.IsSuccess())
            {
                var rows = bro.FlattenRows();
                if (rows.Count > 0)
                {
                    JsonData row = rows[0];
                    _rowInDate = row["inDate"]?.ToString();
                    IsPlaying = ParseBool(row, "playing");
                    Debug.Log($"[RunState] 로드: playing={IsPlaying}");
                }
                else
                {
                    Save(); // 신규 — 빈 row 생성 (playing=false)
                    Debug.Log("[RunState] 신규 row 생성 (playing=false)");
                }
            }
            else
            {
                Debug.LogError($"[RunState] 로드 실패: {bro}");
            }
            onComplete?.Invoke();
        });
    }

    public void StartRun(Action onComplete = null)
    {
        IsPlaying = true;
        Save(onComplete);
    }

    public void EndRun(Action onComplete = null)
    {
        IsPlaying = false;
        Save(onComplete);
    }

    void Save(Action onComplete = null)
    {
        var param = new Param();
        param.Add("playing", IsPlaying);

        if (!string.IsNullOrEmpty(_rowInDate))
        {
            Backend.GameData.UpdateV2("RunState", _rowInDate, Backend.UserInDate, param, bro =>
            {
                if (!bro.IsSuccess()) Debug.LogError($"[RunState] Update 실패: {bro}");
                onComplete?.Invoke();
            });
        }
        else
        {
            Backend.GameData.Insert("RunState", param, bro =>
            {
                if (bro.IsSuccess())
                {
                    _rowInDate = bro.GetInDate();
                    Debug.Log("[RunState] Insert 완료");
                }
                else
                {
                    Debug.LogError($"[RunState] Insert 실패: {bro}");
                }
                onComplete?.Invoke();
            });
        }
    }

    static bool ParseBool(JsonData row, string key)
    {
        if (!row.ContainsKey(key)) return false;
        var s = row[key]?.ToString();
        return s == "True" || s == "true" || s == "1";
    }
}
