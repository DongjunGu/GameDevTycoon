using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 인게임 모달 차단 게이트 — 차단할 UI 가 한 개라도 활성이면 "Blocked".
// 차단 대상 UI 의 GameObject 에 ModalGateRegistrant 컴포넌트를 부착하면 자동 등록/해제.
//
// 사용처: 다른 UI(상인 prompt 등)가 "현재 모달이 다 닫혔을 때" 만 표시되어야 할 때
//   ModalGate.Instance.WhenFree(() => { ... }); 한 줄로 큐잉.
//
// 진단: Blocked 일 때 _active 의 GameObject 이름들 디버그용 GetActiveNames() 로 노출.
public class ModalGate : MonoBehaviour
{
    public static ModalGate Instance { get; private set; }

    // 자동 lazy singleton — 명시적으로 씬에 GameObject 안 만들어도 첫 접근 시 생성.
    static ModalGate Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("[ModalGate]");
        DontDestroyOnLoad(go);
        return go.AddComponent<ModalGate>();
    }

    public static ModalGate I => Instance != null ? Instance : Ensure();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    readonly HashSet<MonoBehaviour> _active = new();
    readonly List<Action> _pending = new();

    public bool IsBlocked => _active.Count > 0;
    public int ActiveCount => _active.Count;

    public void Register(MonoBehaviour ui)
    {
        if (ui == null) return;
        _active.Add(ui);
    }

    public void Unregister(MonoBehaviour ui)
    {
        if (ui == null) return;
        _active.Remove(ui);
        TryFlush();
    }

    // 차단 풀리면 cb 실행. 이미 풀려있으면 즉시. 큐 순서 보장.
    public void WhenFree(Action cb)
    {
        if (cb == null) return;
        if (!IsBlocked) { cb(); return; }
        _pending.Add(cb);
    }

    void TryFlush()
    {
        if (IsBlocked || _pending.Count == 0) return;
        var snapshot = new List<Action>(_pending);
        _pending.Clear();
        foreach (var cb in snapshot)
        {
            try { cb?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
            if (IsBlocked) break; // 한 콜백이 새 모달을 띄우면 거기서 멈추고 그 모달 닫힐 때 이어감
        }
    }

    public IEnumerable<string> GetActiveNames()
    {
        foreach (var m in _active)
            if (m != null) yield return m.gameObject.name;
    }
}
