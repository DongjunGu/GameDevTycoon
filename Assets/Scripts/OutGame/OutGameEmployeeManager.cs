using System;
using System.Collections.Generic;
using System.Text;
using BackEnd;
using LitJson;
using UnityEngine;

// 유저별 아웃게임 직원 풀 (전체 직원 + 달성한 maxGrade)
// 뒤끝 테이블: OutGameEmployee { employeesJson:string } — 한 줄 압축
// JSON 포맷: [{"e":"emp_gold","m":1}, ...]  (e=empId, m=(int)maxGrade)
public class OutGameEmployeeManager : MonoBehaviour
{
    public static OutGameEmployeeManager Instance { get; private set; }

    private readonly Dictionary<string, EmployeeGrade> _maxGradeMap = new();
    private string _rowInDate = null;

    public event Action OnChanged;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // BackendManager.LoadAllAndEnterGame 체인에서 호출 — EmployeeManager.LoadMasterPool 이후
    public void LoadAsync(System.Action onComplete = null)
    {
        BackendRetry.Instance.GetMyData("OutGameEmployee", bro =>
        {
            _maxGradeMap.Clear();
            if (bro.IsSuccess())
            {
                var rows = bro.FlattenRows();
                if (rows.Count > 0)
                {
                    JsonData row = rows[0];
                    _rowInDate = row["inDate"]?.ToString();
                    string json = row.ContainsKey("employeesJson") ? row["employeesJson"]?.ToString() : null;
                    Parse(json);
                    Debug.Log($"[OutGameEmployee] 로드: {_maxGradeMap.Count}명");
                }
                else
                {
                    InitFromMasterPool();
                    Save();
                    Debug.Log($"[OutGameEmployee] 신규 유저 초기화 ({_maxGradeMap.Count}명)");
                }
            }
            else
            {
                Debug.LogError($"[OutGameEmployee] 로드 실패: {bro}");
            }
            OnChanged?.Invoke();
            onComplete?.Invoke();
        });
    }

    void InitFromMasterPool()
    {
        if (EmployeeManager.Instance == null || EmployeeManager.Instance.poolEmployees == null) return;
        foreach (var emp in EmployeeManager.Instance.poolEmployees)
            if (emp != null && !string.IsNullOrEmpty(emp.id))
                _maxGradeMap[emp.id] = EmployeeGrade.Normal;
    }

    void Parse(string json)
    {
        if (string.IsNullOrEmpty(json)) { InitFromMasterPool(); return; }
        try
        {
            JsonData arr = JsonMapper.ToObject(json);
            if (arr != null && arr.IsArray)
            {
                for (int i = 0; i < arr.Count; i++)
                {
                    var item = arr[i];
                    string e = item["e"]?.ToString();
                    int m = item.ContainsKey("m") ? int.Parse(item["m"].ToString()) : 0;
                    if (!string.IsNullOrEmpty(e))
                        _maxGradeMap[e] = (EmployeeGrade)m;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[OutGameEmployee] JSON 파싱 실패: {ex.Message}");
        }

        // 마스터에 있지만 JSON에 없는 신규 직원은 Normal로 채움
        if (EmployeeManager.Instance?.poolEmployees != null)
        {
            foreach (var emp in EmployeeManager.Instance.poolEmployees)
                if (emp != null && !string.IsNullOrEmpty(emp.id) && !_maxGradeMap.ContainsKey(emp.id))
                    _maxGradeMap[emp.id] = EmployeeGrade.Normal;
        }
    }

    string Serialize()
    {
        var sb = new StringBuilder();
        sb.Append('[');
        bool first = true;
        foreach (var kv in _maxGradeMap)
        {
            if (!first) sb.Append(',');
            sb.Append("{\"e\":\"").Append(kv.Key).Append("\",\"m\":").Append((int)kv.Value).Append('}');
            first = false;
        }
        sb.Append(']');
        return sb.ToString();
    }

    public EmployeeGrade GetMaxGrade(string empId)
    {
        return _maxGradeMap.TryGetValue(empId, out var g) ? g : EmployeeGrade.Normal;
    }

    public bool IsAcquired(string empId)
    {
        // OutGameEmployee 테이블에 row가 있고 maxGrade가 0(Normal) 이상이면 보유 가능
        // 단순 해금 판정은 EmployeeManager.IsAcquired (AcquiredEmployee 테이블) 사용 — 호출자 책임
        return _maxGradeMap.ContainsKey(empId);
    }

    // 카드 등급이 현재 maxGrade보다 높으면 갱신 + 저장 + true 반환
    public bool TryUpgradeMaxGrade(string empId, EmployeeGrade newGrade)
    {
        if (string.IsNullOrEmpty(empId)) return false;
        var current = GetMaxGrade(empId);
        if ((int)newGrade <= (int)current && _maxGradeMap.ContainsKey(empId)) return false;
        _maxGradeMap[empId] = newGrade;
        Save();
        OnChanged?.Invoke();
        return true;
    }

    public void Save()
    {
        var param = new Param();
        param.Add("employeesJson", Serialize());

        if (!string.IsNullOrEmpty(_rowInDate))
        {
            Backend.GameData.UpdateV2("OutGameEmployee", _rowInDate, Backend.UserInDate, param, bro =>
            {
                if (!bro.IsSuccess()) Debug.LogError($"[OutGameEmployee] Update 실패: {bro}");
            });
        }
        else
        {
            Backend.GameData.Insert("OutGameEmployee", param, bro =>
            {
                if (bro.IsSuccess())
                {
                    _rowInDate = bro.GetInDate();
                    Debug.Log("[OutGameEmployee] Insert 완료");
                }
                else
                {
                    Debug.LogError($"[OutGameEmployee] Insert 실패: {bro}");
                }
            });
        }
    }
}
