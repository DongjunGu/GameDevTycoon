using System;
using System.Collections.Generic;
using System.Text;
using BackEnd;
using LitJson;
using UnityEngine;

// 유저별 보유 직원 카드 (중복 포함)
// 뒤끝 테이블: OwnedCard { cardsJson:string } — 한 줄 압축
// JSON 포맷: [{"e":"emp_gold","g":0,"s":0,"n":2}, ...]  (e=empId, g=(int)grade, s=stage, n=장수)
//   stage: 같은 grade 안의 강화 단계. Epic/Unique만 1~2단계까지 올라감(합성으로 +1). 기본 0.
//
// 메모리: Dictionary<string, int> 키=$"{empId}|{(int)grade}|{stage}"
// 갱신 시 OutGameEmployeeManager.TryUpgradeMaxGrade + EmployeeManager.AcquireEmployee 자동 연동
public class OwnedCardManager : MonoBehaviour
{
    public static OwnedCardManager Instance { get; private set; }

    private readonly Dictionary<string, int> _cards = new();
    private string _rowInDate = null;

    public event Action OnChanged;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoadAsync(System.Action onComplete = null)
    {
        BackendRetry.Instance.GetMyData("OwnedCard", bro =>
        {
            _cards.Clear();
            if (bro.IsSuccess())
            {
                var rows = bro.FlattenRows();
                if (rows.Count > 0)
                {
                    JsonData row = rows[0];
                    _rowInDate = row["inDate"]?.ToString();
                    string json = row.ContainsKey("cardsJson") ? row["cardsJson"]?.ToString() : null;
                    Parse(json);
                    Debug.Log($"[OwnedCard] 로드: 카드 종류 {_cards.Count}");
                }
                else
                {
                    SeedDefaultCards();
                    Save(); // 신규 row insert (isDefault 직원 카드 포함)
                    Debug.Log($"[OwnedCard] 신규 유저 초기화 (default 카드 {_cards.Count}종)");
                }
            }
            else
            {
                Debug.LogError($"[OwnedCard] 로드 실패: {bro}");
            }
            // OwnedCard가 진실 source — OutGameEmployee.employeesJson과 어긋난 경우 보정
            OutGameEmployeeManager.Instance?.SyncFromOwnedCards();
            OnChanged?.Invoke();
            onComplete?.Invoke();
        });
    }

    static string Key(string empId, EmployeeGrade grade, int stage) => $"{empId}|{(int)grade}|{stage}";

    public static void ParseKey(string key, out string empId, out EmployeeGrade grade, out int stage)
    {
        empId = ""; grade = EmployeeGrade.Normal; stage = 0;
        if (string.IsNullOrEmpty(key)) return;
        var parts = key.Split('|');
        if (parts.Length < 2) { empId = parts[0]; return; }
        empId = parts[0];
        if (int.TryParse(parts[1], out var g)) grade = (EmployeeGrade)g;
        if (parts.Length >= 3 && int.TryParse(parts[2], out var s)) stage = s;
    }

    // 신규 유저: isDefault=true 직원에게 Normal 카드 1장씩 지급
    void SeedDefaultCards()
    {
        if (EmployeeManager.Instance?.poolEmployees == null) return;
        foreach (var emp in EmployeeManager.Instance.poolEmployees)
        {
            if (emp == null || string.IsNullOrEmpty(emp.id) || !emp.isDefault) continue;
            var k = Key(emp.id, EmployeeGrade.Unique, 0);
            _cards[k] = (_cards.TryGetValue(k, out var n) ? n : 0) + 1;
        }
    }

    void Parse(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            JsonData arr = JsonMapper.ToObject(json);
            if (arr != null && arr.IsArray)
            {
                for (int i = 0; i < arr.Count; i++)
                {
                    var item = arr[i];
                    string e = item["e"]?.ToString();
                    int g = item.ContainsKey("g") ? int.Parse(item["g"].ToString()) : 0;
                    int s = item.ContainsKey("s") ? int.Parse(item["s"].ToString()) : 0; // 구버전 row(stage 없음)는 0
                    int n = item.ContainsKey("n") ? int.Parse(item["n"].ToString()) : 0;
                    if (string.IsNullOrEmpty(e) || n <= 0) continue;
                    _cards[Key(e, (EmployeeGrade)g, s)] = n;
                }
            }
        }
        catch (Exception ex) { Debug.LogError($"[OwnedCard] JSON 파싱 실패: {ex.Message}"); }
    }

    string Serialize()
    {
        var sb = new StringBuilder();
        sb.Append('[');
        bool first = true;
        foreach (var kv in _cards)
        {
            if (kv.Value <= 0) continue;
            ParseKey(kv.Key, out var e, out var grade, out var stage);
            if (string.IsNullOrEmpty(e)) continue;
            if (!first) sb.Append(',');
            sb.Append("{\"e\":\"").Append(e)
              .Append("\",\"g\":").Append((int)grade)
              .Append(",\"s\":").Append(stage)
              .Append(",\"n\":").Append(kv.Value)
              .Append('}');
            first = false;
        }
        sb.Append(']');
        return sb.ToString();
    }

    public int GetCount(string empId, EmployeeGrade grade, int stage = 0)
    {
        return _cards.TryGetValue(Key(empId, grade, stage), out var n) ? n : 0;
    }

    // 해당 직원이 보유한 카드 중 최고 grade. 카드 없으면 Normal (해금 기본 상태).
    public EmployeeGrade GetHighestGrade(string empId)
    {
        EmployeeGrade highest = EmployeeGrade.Normal;
        if (string.IsNullOrEmpty(empId)) return highest;
        foreach (var kv in _cards)
        {
            if (kv.Value <= 0) continue;
            ParseKey(kv.Key, out var e, out var grade, out _);
            if (e != empId) continue;
            if ((int)grade > (int)highest) highest = grade;
        }
        return highest;
    }

    // 해당 직원이 그 grade 에서 보유한 카드 중 최고 stage. 카드 없으면 0.
    // stage 는 Epic/Unique 에서만 1~2 까지 올라감 (합성 규칙 — EmployeeMergeUI.TryGetRecipe).
    public int GetHighestStage(string empId, EmployeeGrade grade)
    {
        int highest = 0;
        if (string.IsNullOrEmpty(empId)) return highest;
        foreach (var kv in _cards)
        {
            if (kv.Value <= 0) continue;
            ParseKey(kv.Key, out var e, out var g, out var stage);
            if (e != empId || g != grade) continue;
            if (stage > highest) highest = stage;
        }
        return highest;
    }

    // 보유 카드 전체 (UI 빌드용) — key 형식 "empId|grade|stage"
    public IReadOnlyDictionary<string, int> AllCards => _cards;

    // 카드 1장 추가 (뽑기/합성 시점 호출). save=false 주면 묶어서 한 번만 저장
    public void AddCard(string empId, EmployeeGrade grade, int stage = 0, bool save = true)
    {
        if (string.IsNullOrEmpty(empId)) return;
        var k = Key(empId, grade, stage);
        _cards[k] = (_cards.TryGetValue(k, out var n) ? n : 0) + 1;

        // 해금/등급 갱신 연동 (stage는 maxGrade에 영향 없음)
        if (OutGameEmployeeManager.Instance != null)
            OutGameEmployeeManager.Instance.TryUpgradeMaxGrade(empId, grade);
        if (EmployeeManager.Instance != null && !EmployeeManager.Instance.IsAcquired(empId))
            EmployeeManager.Instance.AcquireEmployee(empId);

        if (save) Save();
        OnChanged?.Invoke();
    }

    // 카드 차감 (합성 재료 소모용). 부족하면 false 반환하고 변경 없음
    // 차감 후 그 직원의 maxGrade를 보유 카드 기준으로 재계산 (다운그레이드 가능)
    public bool RemoveCard(string empId, EmployeeGrade grade, int stage = 0, int count = 1, bool save = true)
    {
        if (string.IsNullOrEmpty(empId) || count <= 0) return false;
        var k = Key(empId, grade, stage);
        if (!_cards.TryGetValue(k, out var n) || n < count) return false;
        n -= count;
        if (n <= 0) _cards.Remove(k);
        else _cards[k] = n;

        if (OutGameEmployeeManager.Instance != null)
            OutGameEmployeeManager.Instance.RecalcMaxGrade(empId);

        if (save) Save();
        OnChanged?.Invoke();
        return true;
    }

    // 일괄 합성 — 합성 가능한 카드를 전부 합성하고 1회 저장. 반환: 새로 생긴 카드 (key → 순증가 장수)
    public Dictionary<string, int> MergeAll()
    {
        var before = new Dictionary<string, int>(_cards);
        var touched = MergeAllInto(_cards);

        var gained = new Dictionary<string, int>();
        foreach (var kv in _cards)
        {
            int diff = kv.Value - (before.TryGetValue(kv.Key, out var b) ? b : 0);
            if (diff > 0) gained[kv.Key] = diff;
        }
        if (touched.Count == 0) return gained;

        if (OutGameEmployeeManager.Instance != null)
            foreach (var e in touched) OutGameEmployeeManager.Instance.RecalcMaxGrade(e);
        Save();
        OnChanged?.Invoke();
        return gained;
    }

    // 합성 단계 순서 — 각 단계 결과는 항상 뒤쪽 단계라 앞에서부터 한 번만 훑으면 연쇄 합성까지 끝난다
    // 일괄 합성은 에픽(0단계)을 만드는 데까지만 — 에픽 1단계부터는 슬롯 합성으로 직접 해야 한다
    static readonly (EmployeeGrade g, int s)[] MergeTiers =
    {
        (EmployeeGrade.Normal, 0), (EmployeeGrade.Rare, 0),
    };

    // 순수 로직 (저장/이벤트 없음). cards 를 직접 수정하고 변경된 직원 id 를 반환.
    // 같은 직원끼리 먼저 묶고, 아무 직원 허용 단계(단계 승급)는 남은 낱장끼리 empId 순으로 묶는다(앞 카드가 메인).
    // ponytail: 낱장 묶음의 메인 선택은 empId 순 고정 — 직원 우선순위가 필요해지면 여기서 정렬 기준만 교체
    public static HashSet<string> MergeAllInto(Dictionary<string, int> cards)
    {
        var touched = new HashSet<string>();
        void Change(string e, EmployeeGrade g, int s, int delta)
        {
            var k = Key(e, g, s);
            int n = (cards.TryGetValue(k, out var c) ? c : 0) + delta;
            if (n <= 0) cards.Remove(k); else cards[k] = n;
            touched.Add(e);
        }

        foreach (var (g, s) in MergeTiers)
        {
            if (!EmployeeMergeUI.TryGetRecipe(g, s, out var r)) continue;
            int size = r.MatCount + 1;
            var singles = new List<string>();

            foreach (var kv in new List<KeyValuePair<string, int>>(cards))
            {
                ParseKey(kv.Key, out var e, out var kg, out var ks);
                if (kg != g || ks != s) continue;
                int sets = kv.Value / size;
                if (sets > 0)
                {
                    Change(e, g, s, -sets * size);
                    Change(e, r.OutGrade, r.OutStage, sets);
                }
                if (!r.SameEmp) for (int i = 0; i < kv.Value % size; i++) singles.Add(e);
            }

            singles.Sort(StringComparer.Ordinal);
            for (int i = 0; i + size <= singles.Count; i += size)
            {
                for (int j = 0; j < size; j++) Change(singles[i + j], g, s, -1);
                Change(singles[i], r.OutGrade, r.OutStage, 1);
            }
        }
        return touched;
    }

    public void Save()
    {
        var param = new Param();
        param.Add("cardsJson", Serialize());

        if (!string.IsNullOrEmpty(_rowInDate))
        {
            Backend.GameData.UpdateV2("OwnedCard", _rowInDate, Backend.UserInDate, param, bro =>
            {
                if (!bro.IsSuccess()) Debug.LogError($"[OwnedCard] Update 실패: {bro}");
            });
        }
        else
        {
            Backend.GameData.Insert("OwnedCard", param, bro =>
            {
                if (bro.IsSuccess())
                {
                    _rowInDate = bro.GetInDate();
                    Debug.Log("[OwnedCard] Insert 완료");
                }
                else
                {
                    Debug.LogError($"[OwnedCard] Insert 실패: {bro}");
                }
            });
        }
    }
}
