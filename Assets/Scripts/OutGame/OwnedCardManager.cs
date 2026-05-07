using System;
using System.Collections.Generic;
using System.Text;
using BackEnd;
using LitJson;
using UnityEngine;

// 유저별 보유 직원 카드 (중복 포함)
// 뒤끝 테이블: OwnedCard { cardsJson:string } — 한 줄 압축
// JSON 포맷: [{"e":"emp_gold","g":0,"s":0,"n":2}, ...]  (e=empId, g=(int)grade, s=stage, n=장수)
//   stage: 같은 grade 안의 강화 단계. Epic/Unique 1단계 합성으로 +1 됨. 기본 0.
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
            var k = Key(emp.id, EmployeeGrade.Normal, 0);
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
