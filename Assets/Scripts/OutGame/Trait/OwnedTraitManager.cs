using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BackEnd;
using LitJson;
using UnityEngine;

// 유저 보유 특성 + 장착 슬롯 매니저
// 뒤끝 테이블: OwnedTrait
//   - ownedJson : "[{\"t\":\"trait_xxx\",\"n\":2}, ...]"  (t=traitId, n=장수)
//   - equippedJson : "[\"trait_a\",\"\",\"trait_b\"]"     (길이 EquipSlotCount, 빈 슬롯은 빈 문자열)
//   - unlockedSlots : INT (해금된 슬롯 수. 없으면 DefaultUnlockedSlots)
//
// 기본: default 장착 0개 / 슬롯은 2개만 해금
// 3~5번째 슬롯은 다이아로 순서대로 해금 (SlotUnlockCosts: 500 / 1000 / 2000)
// 메모리: 보유 dict + 장착 string[EquipSlotCount]
public class OwnedTraitManager : MonoBehaviour
{
    // 슬롯 배열 길이(최대치). 실제 사용 가능한 개수는 UnlockedSlotCount
    public const int EquipSlotCount = 5;
    public const int DefaultUnlockedSlots = 2;

    // 인덱스 = 슬롯 인덱스. 해금 비용(다이아). 기본 해금 슬롯은 0
    public static readonly int[] SlotUnlockCosts = { 0, 0, 500, 1000, 2000 };

    public static OwnedTraitManager Instance { get; private set; }

    [Header("Save Debounce")]
    [Tooltip("변경 후 이 시간(초) 동안 추가 변경이 없으면 서버 저장. 연쇄 클릭/뽑기 묶음")]
    public float saveDebounceSeconds = 0.7f;

    private readonly Dictionary<string, int> _owned = new();
    private readonly string[] _equipped = new string[EquipSlotCount];
    private string _rowInDate = null;
    private int _unlockedSlots = DefaultUnlockedSlots;

    private bool _dirty;
    private Coroutine _saveCo;

    public event Action OnChanged;

    public IReadOnlyDictionary<string, int> AllOwned => _owned;
    public IReadOnlyList<string> Equipped => _equipped;
    public int UnlockedSlotCount => _unlockedSlots;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()         => FlushPendingSave();
    void OnApplicationQuit() => FlushPendingSave();
    void OnApplicationPause(bool paused) { if (paused) FlushPendingSave(); }

    // 변경 발생 시 호출 — debounce 타이머 시작/리셋
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

    // 외부에서 즉시 저장 강제 (예: 인게임 진입 직전)
    public void FlushPendingSave()
    {
        if (_saveCo != null) { StopCoroutine(_saveCo); _saveCo = null; }
        if (!_dirty) return;
        _dirty = false;
        Save();
    }

    public void LoadAsync(Action onComplete = null)
    {
        BackendRetry.Instance.GetMyData("OwnedTrait", bro =>
        {
            _owned.Clear();
            ClearEquipped();
            _unlockedSlots = DefaultUnlockedSlots;

            if (bro.IsSuccess())
            {
                var rows = bro.FlattenRows();
                if (rows.Count > 0)
                {
                    JsonData row = rows[0];
                    _rowInDate = row["inDate"]?.ToString();
                    ParseOwned(row.ContainsKey("ownedJson") ? row["ownedJson"]?.ToString() : null);
                    ParseEquipped(row.ContainsKey("equippedJson") ? row["equippedJson"]?.ToString() : null);
                    _unlockedSlots = ParseUnlockedSlots(row);
                    ClampEquippedToUnlocked();
                    Debug.Log($"[OwnedTrait] 로드: 보유 {_owned.Count}종 / 장착 {CountEquipped()}개 / 해금 슬롯 {_unlockedSlots}개");
                }
                else
                {
                    Save(); // 신규 row insert (빈 상태)
                    Debug.Log("[OwnedTrait] 신규 유저 — 빈 상태로 초기화");
                }
            }
            else
            {
                Debug.LogError($"[OwnedTrait] 로드 실패: {bro}");
            }

            OnChanged?.Invoke();
            onComplete?.Invoke();
        });
    }

    int ParseUnlockedSlots(JsonData row)
    {
        if (row == null || !row.ContainsKey("unlockedSlots")) return DefaultUnlockedSlots;
        if (!int.TryParse(row["unlockedSlots"]?.ToString(), out int v)) return DefaultUnlockedSlots;
        return Mathf.Clamp(v, DefaultUnlockedSlots, EquipSlotCount);
    }

    // 잠긴 슬롯에 남아있는 장착 정보 제거 (구버전 데이터 방어)
    void ClampEquippedToUnlocked()
    {
        for (int i = _unlockedSlots; i < _equipped.Length; i++) _equipped[i] = null;
    }

    void ClearEquipped()
    {
        for (int i = 0; i < _equipped.Length; i++) _equipped[i] = null;
    }

    int CountEquipped()
    {
        int n = 0;
        for (int i = 0; i < _equipped.Length; i++)
            if (!string.IsNullOrEmpty(_equipped[i])) n++;
        return n;
    }

    void ParseOwned(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            JsonData arr = JsonMapper.ToObject(json);
            if (arr == null || !arr.IsArray) return;
            for (int i = 0; i < arr.Count; i++)
            {
                var item = arr[i];
                string t = item.ContainsKey("t") ? item["t"]?.ToString() : null;
                int n = item.ContainsKey("n") ? int.Parse(item["n"].ToString()) : 0;
                if (string.IsNullOrEmpty(t) || n <= 0) continue;
                _owned[t] = n;
            }
        }
        catch (Exception ex) { Debug.LogError($"[OwnedTrait] ownedJson 파싱 실패: {ex.Message}"); }
    }

    void ParseEquipped(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            JsonData arr = JsonMapper.ToObject(json);
            if (arr == null || !arr.IsArray) return;
            int n = Mathf.Min(arr.Count, _equipped.Length);
            for (int i = 0; i < n; i++)
            {
                string v = arr[i]?.ToString();
                _equipped[i] = string.IsNullOrEmpty(v) ? null : v;
            }
        }
        catch (Exception ex) { Debug.LogError($"[OwnedTrait] equippedJson 파싱 실패: {ex.Message}"); }
    }

    string SerializeOwned()
    {
        var sb = new StringBuilder();
        sb.Append('[');
        bool first = true;
        foreach (var kv in _owned)
        {
            if (kv.Value <= 0) continue;
            if (!first) sb.Append(',');
            sb.Append("{\"t\":\"").Append(kv.Key).Append("\",\"n\":").Append(kv.Value).Append('}');
            first = false;
        }
        sb.Append(']');
        return sb.ToString();
    }

    string SerializeEquipped()
    {
        var sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0; i < _equipped.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"').Append(_equipped[i] ?? "").Append('"');
        }
        sb.Append(']');
        return sb.ToString();
    }

    // ----- 조회 -----
    public int GetCount(string traitId)
        => string.IsNullOrEmpty(traitId) ? 0
         : (_owned.TryGetValue(traitId, out var n) ? n : 0);

    public bool IsOwned(string traitId) => GetCount(traitId) > 0;

    public int IndexOfEquipped(string traitId)
    {
        if (string.IsNullOrEmpty(traitId)) return -1;
        for (int i = 0; i < _equipped.Length; i++)
            if (_equipped[i] == traitId) return i;
        return -1;
    }

    public bool IsEquipped(string traitId) => IndexOfEquipped(traitId) >= 0;

    public bool IsSlotUnlocked(int slotIndex) => slotIndex >= 0 && slotIndex < _unlockedSlots;

    // 해금 비용(다이아). 이미 해금됐거나 범위 밖이면 0
    public int GetSlotUnlockCost(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= EquipSlotCount) return 0;
        if (IsSlotUnlocked(slotIndex)) return 0;
        return SlotUnlockCosts[slotIndex];
    }

    // 다음에 해금 가능한 슬롯만 열 수 있다 (순서대로)
    public bool CanUnlockSlot(int slotIndex) => slotIndex == _unlockedSlots && slotIndex < EquipSlotCount;

    public string GetEquipped(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _equipped.Length) return null;
        return _equipped[slotIndex];
    }

    // ----- 변경 -----
    // 변경 시 ScheduleSave() — debounce 후 1회 저장 (연쇄 호출은 마지막만 발사)
    // save 파라미터는 backward compat 용 (현재는 무시) — 항상 schedule
    public void AddTrait(string traitId, bool save = true)
    {
        if (string.IsNullOrEmpty(traitId)) return;
        _owned[traitId] = (_owned.TryGetValue(traitId, out var n) ? n : 0) + 1;
        ScheduleSave();
        OnChanged?.Invoke();
    }

    // 빈 슬롯에 자동 장착. 이미 장착돼있거나 빈 슬롯 없으면 false
    public bool TryEquip(string traitId)
    {
        if (string.IsNullOrEmpty(traitId)) return false;
        if (!IsOwned(traitId)) return false;
        if (IsEquipped(traitId)) return false;
        for (int i = 0; i < _unlockedSlots; i++)
        {
            if (string.IsNullOrEmpty(_equipped[i]))
            {
                _equipped[i] = traitId;
                ScheduleSave();
                OnChanged?.Invoke();
                return true;
            }
        }
        return false;
    }

    public void Unequip(string traitId)
    {
        int i = IndexOfEquipped(traitId);
        if (i < 0) return;
        _equipped[i] = null;
        ScheduleSave();
        OnChanged?.Invoke();
    }

    public void UnequipSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _equipped.Length) return;
        if (string.IsNullOrEmpty(_equipped[slotIndex])) return;
        _equipped[slotIndex] = null;
        ScheduleSave();
        OnChanged?.Invoke();
    }

    // 다이아 차감 후 슬롯 1칸 해금. 실패 사유는 reason 으로 반환
    public bool TryUnlockSlot(int slotIndex, out string reason)
    {
        reason = null;
        if (!CanUnlockSlot(slotIndex))
        {
            reason = "이전 슬롯부터 순서대로 해금해야 합니다.";
            return false;
        }

        int cost = SlotUnlockCosts[slotIndex];
        var wallet = OutGameCurrencyManager.Instance;
        if (wallet == null)
        {
            reason = "재화 정보를 불러오지 못했습니다.";
            return false;
        }
        if (!wallet.SpendDiamond(cost))
        {
            reason = $"다이아가 부족합니다. (필요 {cost:N0}D / 보유 {wallet.Diamond:N0}D)";
            return false;
        }

        _unlockedSlots = slotIndex + 1;
        // 재화 차감은 이미 서버 저장됨 — 슬롯 해금도 디바운스 없이 즉시 저장 (유실 시 다이아만 사라짐)
        _dirty = true;
        FlushPendingSave();
        OnChanged?.Invoke();
        Debug.Log($"[OwnedTrait] 슬롯 {slotIndex + 1} 해금 (-{cost}D) / 해금 슬롯 {_unlockedSlots}개");
        return true;
    }

    public void Save()
    {
        var param = new Param();
        param.Add("ownedJson", SerializeOwned());
        param.Add("equippedJson", SerializeEquipped());
        param.Add("unlockedSlots", _unlockedSlots);

        if (!string.IsNullOrEmpty(_rowInDate))
        {
            Backend.GameData.UpdateV2("OwnedTrait", _rowInDate, Backend.UserInDate, param, bro =>
            {
                if (!bro.IsSuccess()) Debug.LogError($"[OwnedTrait] Update 실패: {bro}");
            });
        }
        else
        {
            Backend.GameData.Insert("OwnedTrait", param, bro =>
            {
                if (bro.IsSuccess())
                {
                    _rowInDate = bro.GetInDate();
                    Debug.Log("[OwnedTrait] Insert 완료");
                }
                else
                {
                    Debug.LogError($"[OwnedTrait] Insert 실패: {bro}");
                }
            });
        }
    }
}
