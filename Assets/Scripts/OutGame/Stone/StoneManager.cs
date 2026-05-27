using System;
using System.Collections;
using System.Collections.Generic;
using BackEnd;
using LitJson;
using UnityEngine;

// 돌 매니저 (메타, 영구 보존 — 런 무관).
// Stones 리스트가 모든 돌을 보관 (적용중 + 보관). ActiveStoneId 가 그중 적용중인 돌을 가리킴.
// CEOManager 의 stage/progress 는 ActiveStone 에서 위임 조회. 강화/리셋도 ActiveStone 을 직접 mutate.
//
// 뒤끝 테이블: UserStoneList (단일 row)
//   stonesJson (string) — StoneListWrap JsonUtility 직렬화
//   activeStoneId (string) — 적용중 돌 id (빈 문자열 = 없음)
public class StoneManager : MonoBehaviour
{
    public static StoneManager Instance { get; private set; }

    [Header("Save Debounce")]
    public float saveDebounceSeconds = 0.7f;

    [Header("Limits")]
    [Tooltip("보관함 최대 개수. 0 이하면 무제한.")]
    public int maxStones = 0;

    public List<Stone> Stones { get; private set; } = new();
    public string ActiveStoneId { get; private set; }

    public Stone ActiveStone
    {
        get
        {
            if (string.IsNullOrEmpty(ActiveStoneId)) return null;
            return Stones.Find(s => s.id == ActiveStoneId);
        }
    }

    public event Action OnChanged;

    string _rowInDate;
    bool _dirty;
    Coroutine _saveCo;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()                       => FlushPendingSave();
    void OnApplicationQuit()               => FlushPendingSave();
    void OnApplicationPause(bool paused) { if (paused) FlushPendingSave(); }

    // ──────── 조작 ────────

    // 신규 (0,0,0) 돌 push. 활성 돌이 없으면 자동 활성화.
    public Stone AddNew()
    {
        if (maxStones > 0 && Stones.Count >= maxStones) return null;
        var s = new Stone();
        Stones.Add(s);
        bool autoActive = string.IsNullOrEmpty(ActiveStoneId);
        if (autoActive) ActiveStoneId = s.id;
        Dirty();
        if (autoActive) CEOManager.Instance?.RaiseChanged();
        return s;
    }

    public bool Remove(Stone s)
    {
        if (s == null) return false;
        bool wasActive = (s.id == ActiveStoneId);
        bool removed = Stones.Remove(s);
        if (!removed) return false;
        if (wasActive) ActiveStoneId = null;
        Dirty();
        if (wasActive) CEOManager.Instance?.RaiseChanged();
        return true;
    }

    // 선택 돌을 활성 슬롯으로. selected 가 이미 active 면 no-op.
    public bool SetActive(Stone selected)
    {
        if (selected == null) return false;
        if (!Stones.Contains(selected)) return false;
        if (ActiveStoneId == selected.id) return false;
        ActiveStoneId = selected.id;
        Dirty();
        CEOManager.Instance?.RaiseChanged();
        return true;
    }

    // CEOManager 의 강화/리셋이 ActiveStone 을 mutate 한 뒤 호출 — debounce 저장 트리거.
    public void MarkDirty() => Dirty();

    void Dirty()
    {
        OnChanged?.Invoke();
        ScheduleSave();
    }

    // ──────── 저장 (debounce) ────────

    void ScheduleSave()
    {
        _dirty = true;
        if (_saveCo != null) StopCoroutine(_saveCo);
        _saveCo = StartCoroutine(SaveAfterDelay());
    }

    IEnumerator SaveAfterDelay()
    {
        yield return new WaitForSecondsRealtime(saveDebounceSeconds);
        _saveCo = null;
        FlushPendingSave();
    }

    public void FlushPendingSave()
    {
        if (_saveCo != null) { StopCoroutine(_saveCo); _saveCo = null; }
        if (!_dirty) return;
        _dirty = false;
        Save();
    }

    void Save()
    {
        var wrap = new StoneListWrap { stones = Stones };
        string json = JsonUtility.ToJson(wrap);

        var param = new Param();
        param.Add("stonesJson", json);
        param.Add("activeStoneId", ActiveStoneId ?? "");

        if (!string.IsNullOrEmpty(_rowInDate))
        {
            Backend.GameData.UpdateV2("UserStoneList", _rowInDate, Backend.UserInDate, param, bro =>
            {
                if (!bro.IsSuccess()) Debug.LogError($"[StoneManager] Update 실패: {bro}");
            });
        }
        else
        {
            Backend.GameData.Insert("UserStoneList", param, bro =>
            {
                if (bro.IsSuccess())
                {
                    _rowInDate = bro.GetInDate();
                    Debug.Log("[StoneManager] Insert 완료");
                }
                else
                {
                    Debug.LogError($"[StoneManager] Insert 실패: {bro}");
                }
            });
        }
    }

    // ──────── 로드 + 마이그레이션 ────────

    public void LoadAsync(Action onComplete = null)
    {
        BackendRetry.Instance.GetMyData("UserStoneList", bro =>
        {
            Stones.Clear();
            ActiveStoneId = null;
            _rowInDate = null;

            if (bro.IsSuccess())
            {
                var rows = bro.FlattenRows();
                Debug.Log($"[StoneManager] LoadAsync rows.Count={rows.Count}");
                if (rows.Count > 0)
                {
                    JsonData row = rows[0];
                    _rowInDate = row["inDate"]?.ToString();

                    string json = row.ContainsKey("stonesJson") ? row["stonesJson"]?.ToString() : "";
                    if (!string.IsNullOrEmpty(json))
                    {
                        try
                        {
                            var wrap = JsonUtility.FromJson<StoneListWrap>(json);
                            if (wrap?.stones != null) Stones = wrap.stones;
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[StoneManager] stonesJson 파싱 실패: {e.Message}");
                        }
                    }

                    string aid = row.ContainsKey("activeStoneId") ? row["activeStoneId"]?.ToString() : "";
                    if (!string.IsNullOrEmpty(aid))
                    {
                        // 무결성: id 가 실제 stones 에 있어야 유효
                        if (Stones.Exists(s => s.id == aid)) ActiveStoneId = aid;
                    }
                    Debug.Log($"[StoneManager] 로드: Stones={Stones.Count} activeId={(ActiveStoneId ?? "(none)")}");
                }
                else
                {
                    Debug.Log("[StoneManager] 신규 유저 또는 빈 row");
                }
            }
            else
            {
                Debug.LogError($"[StoneManager] 로드 실패: {bro}");
            }

            // 구 UserCEO 마이그레이션: CEOManager 가 시드를 가지고 있고 아직 active 가 없다면, 시드로 새 돌 생성 + 활성화.
            TryMigrateFromCEO();

            // 신규 유저(돌 0개): 기본 돌 1개 지급 → 첫 로그인부터 조각 강화 가능.
            // (마이그레이션 시드가 있으면 위에서 이미 1개 생겨 skip. 0개일 때만 = 진짜 신규.)
            if (Stones.Count == 0)
            {
                var starter = new Stone();
                Stones.Add(starter);
                ActiveStoneId = starter.id;
                Dirty(); // 영속화(신규 row Insert / 기존 row Update)
                Debug.Log("[StoneManager] 신규 유저 기본 돌 1개 지급 (active)");
            }

            OnChanged?.Invoke();
            CEOManager.Instance?.RaiseChanged();
            onComplete?.Invoke();
        });
    }

    void TryMigrateFromCEO()
    {
        var ceo = CEOManager.Instance;
        if (ceo == null || !ceo.HasMigrationSeed) return;
        if (ActiveStone != null) return; // 이미 활성 돌 있으면 마이그레이션 안 함

        var seed = new Stone(
            ceo.MigPlanningStage, ceo.MigDevelopStage, ceo.MigArtStage,
            ceo.MigPlanningProgress, ceo.MigDevelopProgress, ceo.MigArtProgress
        );
        Stones.Add(seed);
        ActiveStoneId = seed.id;
        Dirty();
        Debug.Log($"[StoneManager] 마이그레이션: 구 UserCEO 상태로 active 돌 생성 P{seed.planningStage}/D{seed.developStage}/A{seed.artStage}");
    }
}
