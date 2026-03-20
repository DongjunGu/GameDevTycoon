// =====================================================
// DialogManager.cs
// 다이얼로그 재생, 상태 관리, 이력 관리 싱글톤
// =====================================================
// [Inspector 할당 필요]
//  - _dialogUI : DialogUI 컴포넌트가 붙은 GameObject
// =====================================================

using System;
using System.Collections.Generic;
using UnityEngine;

public class DialogManager : MonoBehaviour
{
    public static DialogManager Instance { get; private set; }

    // ─── 이벤트 (외부 위임) ───────────────────────────────────────
    // resultType(string), resultValue(int) 전달 → 각 Manager가 구독
    public event Action<string, int> OnChoiceResult;
    public event Action OnDialogEnd;

    // ─── 캐시 ────────────────────────────────────────────────────
    // key: dialogId
    private Dictionary<string, DialogNodeData> _nodeCache;
    // key: parentDialogId
    private Dictionary<string, List<ChoiceData>> _choiceCache;
    // key: dialogGroupId → 정렬된 노드 목록
    private Dictionary<string, List<DialogNodeData>> _groupCache;

    // ─── 재생 상태 ───────────────────────────────────────────────
    public bool IsPlaying { get; private set; }
    private DialogNodeData _currentNode;
    private string _pendingNextDialogId = null;

    // ─── 이력 (PlayerPrefs) ──────────────────────────────────────
    private const string PREFS_VIEWED = "dialog_viewed_groups";
    private HashSet<string> _viewedGroups = new HashSet<string>();

    [Header("Inspector 할당")]
    [SerializeField] private DialogUI _dialogUI;
    private Dictionary<string, string> _placeholders = new();


    // ─── 생명주기 ────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadHistory();
    }

    // ─── 초기화 (GameManager 등에서 Chart 로드 후 호출) ──────────
    public void Initialize()
    {
        Debug.Log("[DialogManager] Initialize 시작");
        _nodeCache = DialogChartLoader.LoadNodes();
        _choiceCache = DialogChartLoader.LoadChoices();
        BuildGroupCache();
        Debug.Log($"[DialogManager] 초기화 완료 — 노드: {_nodeCache.Count}개");
    }

    private void BuildGroupCache()
    {
        _groupCache = new Dictionary<string, List<DialogNodeData>>();
        foreach (var node in _nodeCache.Values)
        {
            if (!_groupCache.ContainsKey(node.dialogGroupId))
                _groupCache[node.dialogGroupId] = new List<DialogNodeData>();
            _groupCache[node.dialogGroupId].Add(node);
        }
        // order 기준 정렬
        foreach (var list in _groupCache.Values)
            list.Sort((a, b) => a.order.CompareTo(b.order));
    }

    // ─── 재생 API ────────────────────────────────────────────────

    /// <summary>
    /// 다이얼로그 그룹 재생.
    /// triggerOnce = true이면 이미 본 그룹은 무시.
    /// </summary>
    public void Play(string dialogGroupId, bool triggerOnce = false)
    {
        if (_groupCache == null)
        {
            Debug.LogError("[DialogManager] Initialize()가 아직 실행되지 않았습니다.");
            return;
        }
        Debug.Log($"[DialogManager] Play 호출 — groupCache null: {_groupCache == null}, dialogUI null: {_dialogUI == null}");
        if (IsPlaying)
        {
            Debug.LogWarning("[DialogManager] 이미 재생 중");
            return;
        }
        if (triggerOnce && _viewedGroups.Contains(dialogGroupId))
            return;
        if (!_groupCache.TryGetValue(dialogGroupId, out var nodes))
        {
            Debug.LogWarning($"[DialogManager] 그룹 없음: {dialogGroupId}");
            return;
        }

        IsPlaying = true;
        if (triggerOnce) MarkViewed(dialogGroupId);

        ShowNode(nodes[0]);
    }

    public bool HasGroup(string dialogGroupId) => _groupCache.ContainsKey(dialogGroupId);

    // ─── 진행 (DialogUI에서 호출) ────────────────────────────────

    /// <summary>터치 or 자동 진행 시 DialogUI가 호출</summary>
    public void Next()
    {
        if (_currentNode == null || _currentNode.hasChoice) return;

        if (string.IsNullOrEmpty(_currentNode.nextDialogId))
        {
            EndDialog();
            return;
        }

        if (_nodeCache.TryGetValue(_currentNode.nextDialogId, out var next))
            ShowNode(next);
        else
            EndDialog();
    }

    /// <summary>선택지 버튼 클릭 시 DialogUI가 호출</summary>
    public void OnChoiceSelected(ChoiceData choice)
    {
        if (!CheckCondition(choice))
        {
            Debug.Log($"[DialogManager] 조건 불충족: {choice.choiceId}");
            return;
        }

        // 결과 이벤트 발행
        if (choice.resultType != ResultType.None)
            OnChoiceResult?.Invoke(choice.resultType.ToString(), choice.resultValue);

        // ─── 일시정지 모드 ───────────────────────────────────────
        // pauseAfterResult = true 이면 nextDialogId를 저장만 하고 대기
        // 외부에서 DialogManager.Instance.Resume() 호출 시 재개
        if (choice.pauseAfterResult && !string.IsNullOrEmpty(choice.nextDialogId))
        {
            _pendingNextDialogId = choice.nextDialogId;
            _dialogUI.Hide(); // 대화창 잠깐 숨김
            Debug.Log($"[DialogManager] 일시정지 — 대기 중: {_pendingNextDialogId}");
            return;
        }
        // ────────────────────────────────────────────────────────

        if (string.IsNullOrEmpty(choice.nextDialogId))
        {
            EndDialog();
            return;
        }

        if (_nodeCache.TryGetValue(choice.nextDialogId, out var next))
            ShowNode(next);
        else
            EndDialog();
    }

    // ─── 내부 ────────────────────────────────────────────────────
    private void ShowNode(DialogNodeData node)
    {
        _currentNode = node;
        _choiceCache.TryGetValue(node.dialogId, out var choices);
        _dialogUI.Show(node, choices);
    }

    public void EndDialog()
    {
        IsPlaying = false;
        _currentNode = null;
        _dialogUI.Hide();
        OnDialogEnd?.Invoke();
    }

    private bool CheckCondition(ChoiceData choice)
    {
        return choice.conditionType switch
        {
            // ✅ private _gold 직접 접근 대신 CanAfford() 사용
            ConditionType.Gold => MoneyManager.Instance.CanAfford(choice.conditionValue),
            ConditionType.Level => true, // TODO: LevelManager 연결
            _ => true,
        };
    }

    // ─── 이력 ────────────────────────────────────────────────────
    private void MarkViewed(string groupId)
    {
        _viewedGroups.Add(groupId);
        SaveHistory();
    }

    private void SaveHistory()
    {
        PlayerPrefs.SetString(PREFS_VIEWED, string.Join(",", _viewedGroups));
        PlayerPrefs.Save();
    }

    private void LoadHistory()
    {
        var saved = PlayerPrefs.GetString(PREFS_VIEWED, "");
        if (string.IsNullOrEmpty(saved)) return;
        foreach (var id in saved.Split(','))
            if (!string.IsNullOrEmpty(id)) _viewedGroups.Add(id);
    }
    /// <summary>
    /// 외부 작업이 끝난 뒤 호출하면 pendingNextDialogId 로 이어서 재생
    /// </summary>
    public void Resume()
    {
        if (string.IsNullOrEmpty(_pendingNextDialogId))
        {
            Debug.LogWarning("[DialogManager] Resume 호출됐지만 대기 중인 노드 없음");
            return;
        }

        if (_nodeCache.TryGetValue(_pendingNextDialogId, out var next))
        {
            _pendingNextDialogId = null;
            ShowNode(next);
        }
        else
        {
            _pendingNextDialogId = null;
            EndDialog();
        }
    }

    public void SetPlaceholder(string key, string value)
    {
        _placeholders[key] = value;
    }

    public void ClearPlaceholders()
    {
        _placeholders.Clear();
    }

    // 텍스트 치환 (DialogUI에서 텍스트 표시 전 호출)
    public string ReplacePlaceholders(string text)
    {
        foreach (var kv in _placeholders)
            text = text.Replace("{" + kv.Key + "}", kv.Value);
        return text;
    }

}