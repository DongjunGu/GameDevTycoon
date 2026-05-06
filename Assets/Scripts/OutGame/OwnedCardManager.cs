using System;
using System.Collections.Generic;
using System.Text;
using BackEnd;
using LitJson;
using UnityEngine;

// 유저별 보유 직원 카드 (중복 포함)
// 뒤끝 테이블: OwnedCard { cardsJson:string } — 한 줄 압축
// JSON 포맷: [{"e":"emp_gold","g":0,"n":2}, ...]  (e=empId, g=(int)grade, n=장수)
//
// 메모리: Dictionary<string, int> 키=$"{empId}|{(int)grade}"
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
                    Save(); // 신규 row insert
                    Debug.Log("[OwnedCard] 신규 유저 초기화");
                }
            }
            else
            {
                Debug.LogError($"[OwnedCard] 로드 실패: {bro}");
            }
            OnChanged?.Invoke();
            onComplete?.Invoke();
        });
    }

    static string Key(string empId, EmployeeGrade grade) => $"{empId}|{(int)grade}";

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
                    int n = item.ContainsKey("n") ? int.Parse(item["n"].ToString()) : 0;
                    if (string.IsNullOrEmpty(e) || n <= 0) continue;
                    _cards[Key(e, (EmployeeGrade)g)] = n;
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
            int barIdx = kv.Key.IndexOf('|');
            if (barIdx <= 0) continue;
            string e = kv.Key.Substring(0, barIdx);
            string g = kv.Key.Substring(barIdx + 1);
            if (!first) sb.Append(',');
            sb.Append("{\"e\":\"").Append(e).Append("\",\"g\":").Append(g).Append(",\"n\":").Append(kv.Value).Append('}');
            first = false;
        }
        sb.Append(']');
        return sb.ToString();
    }

    public int GetCount(string empId, EmployeeGrade grade)
    {
        return _cards.TryGetValue(Key(empId, grade), out var n) ? n : 0;
    }

    // 직원별 모든 등급의 카드 (grade → count)
    public Dictionary<EmployeeGrade, int> GetCardsByEmployee(string empId)
    {
        var result = new Dictionary<EmployeeGrade, int>();
        foreach (var kv in _cards)
        {
            int barIdx = kv.Key.IndexOf('|');
            if (barIdx <= 0) continue;
            if (kv.Key.Substring(0, barIdx) != empId) continue;
            if (kv.Value <= 0) continue;
            int g = int.Parse(kv.Key.Substring(barIdx + 1));
            result[(EmployeeGrade)g] = kv.Value;
        }
        return result;
    }

    // 보유 카드 전체 (UI 빌드용)
    public IReadOnlyDictionary<string, int> AllCards => _cards;

    // 카드 1장 추가 (뽑기 시점 호출). save=false 주면 묶어서 한 번만 저장
    public void AddCard(string empId, EmployeeGrade grade, bool save = true)
    {
        if (string.IsNullOrEmpty(empId)) return;
        var k = Key(empId, grade);
        _cards[k] = (_cards.TryGetValue(k, out var n) ? n : 0) + 1;

        // 해금/등급 갱신 연동
        if (OutGameEmployeeManager.Instance != null)
            OutGameEmployeeManager.Instance.TryUpgradeMaxGrade(empId, grade);
        if (EmployeeManager.Instance != null && !EmployeeManager.Instance.IsAcquired(empId))
            EmployeeManager.Instance.AcquireEmployee(empId);

        if (save) Save();
        OnChanged?.Invoke();
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
