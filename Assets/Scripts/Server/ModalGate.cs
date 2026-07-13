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
    Coroutine _flushCo;

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

    // 씬 전환(파산→아웃게임, 재접속 등) 안전망 — DontDestroyOnLoad 싱글톤이라 Unregister 를 놓친 등록이
    // 남으면 IsBlocked 가 그 세션 내내 영구 true 로 고착된다. GameTimeManager.ForceStartTime 과 같은 지점에서
    // 호출해 잔여 등록을 정리한다.
    public void ClearAll()
    {
        _active.Clear();
        _pending.Clear();
        if (_flushCo != null) { StopCoroutine(_flushCo); _flushCo = null; }
    }

    // 차단 풀리면 cb 실행. 이미 풀려있으면 즉시. 큐 순서 보장.
    public void WhenFree(Action cb)
    {
        if (cb == null) return;
        if (!IsBlocked) { cb(); return; }
        _pending.Add(cb);
    }

    // 한 프레임 뒤로 미뤄서 큐를 비운다 — 직전 모달을 닫은 그 클릭(wasPressedThisFrame)이
    // 같은 프레임에 새로 뜨는 다음 모달의 "탭하면 스킵/선택" 입력 감지에 그대로 새어들어가는 것을 방지.
    // (예: 채용 완료 버튼 클릭 → 같은 프레임에 다음 랜덤이벤트 패널이 뜨면서 그 클릭을 스킵/선택 입력으로 오인)
    void TryFlush()
    {
        if (IsBlocked || _pending.Count == 0) return;
        if (_flushCo != null) return; // 이미 다음 프레임 플러시 예약됨
        _flushCo = StartCoroutine(FlushNextFrame());
    }

    IEnumerator FlushNextFrame()
    {
        yield return null;
        _flushCo = null;

        if (IsBlocked || _pending.Count == 0) yield break;
        var snapshot = new List<Action>(_pending);
        _pending.Clear();
        foreach (var cb in snapshot)
        {
            try { cb?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
            if (IsBlocked) break; // 한 콜백이 새 모달을 띄우면 거기서 멈추고 그 모달 닫힐 때 이어감
        }

        // 콜백들이 아무것도 재등록 안 했는데 그 사이 새 pending 이 더 쌓였으면 이어서 플러시.
        if (!IsBlocked && _pending.Count > 0) TryFlush();
    }

    public IEnumerable<string> GetActiveNames()
    {
        foreach (var m in _active)
            if (m != null) yield return m.gameObject.name;
    }
}
