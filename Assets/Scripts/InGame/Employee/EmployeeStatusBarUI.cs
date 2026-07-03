using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 하단 상태바 — 보유 직원마다 슬롯(초상화 + 만족도 슬라이더) 생성
// - 채용/해고/저장 로드 시 OfficeManager가 호출 → 슬롯 추가/제거/리빌드
// - 슬롯 자체의 만족도 보간/색/클릭은 EmployeeSatisfactionSlider가 담당
// - toggleButton을 누르면 slotContainer가 위로 올라오고/내려가는 토글 슬라이드
public class EmployeeStatusBarUI : MonoBehaviour
{
    public static EmployeeStatusBarUI Instance { get; private set; }

    [Header("References")]
    [Tooltip("슬롯이 늘어설 컨테이너 (HorizontalLayoutGroup 등) — RectTransform이어야 슬라이드 가능")]
    public Transform slotContainer;
    [Tooltip("EmployeeSatisfactionSlider 스크립트가 붙은 슬롯 프리팹")]
    public GameObject slotPrefab;

    [Header("Toggle Slide")]
    [Tooltip("누를 때마다 slidePanel 표시/숨김 토글")]
    public Button toggleButton;
    [Tooltip("실제로 슬라이드/활성화될 패널 (slotContainer의 부모) — 비워두면 slotContainer가 직접 슬라이드")]
    public RectTransform slidePanel;
    [Tooltip("표시 상태일 때 slidePanel의 anchoredPosition")]
    public Vector2 shownAnchoredPos;
    [Tooltip("숨김 상태일 때 slidePanel의 anchoredPosition (보통 화면 아래로 내려간 위치)")]
    public Vector2 hiddenAnchoredPos;
    [Tooltip("슬라이드 시간(초)")]
    public float slideDuration = 0.3f;
    [Tooltip("시작 시 숨김 상태로 시작")]
    public bool startHidden = true;
    [Tooltip("toggleButton의 화살표/아이콘 — 슬라이드로 들어가면(Show) Y축 flip")]
    public Transform buttonImage;

    private readonly Dictionary<string, EmployeeSatisfactionSlider> _slots = new();
    private bool _isShown;
    private Coroutine _slideCo;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // 씬 시작 시점에 이미 보유 직원이 있다면 초기 빌드
        Rebuild();

        // 토글 버튼 연결 + 초기 상태
        if (toggleButton != null) toggleButton.onClick.AddListener(Toggle);

        var rect = GetSlideRect();
        if (rect != null)
        {
            rect.anchoredPosition = startHidden ? hiddenAnchoredPos : shownAnchoredPos;
        }
        _isShown = !startHidden;
        UpdateButtonImageFlip();
    }

    RectTransform GetSlideRect()
    {
        if (slidePanel != null) return slidePanel;
        return slotContainer as RectTransform;
    }

    public void Toggle()
    {
        if (_isShown) Hide();
        else Show();
    }

    public void Show()
    {
        var rect = GetSlideRect();
        if (rect == null) return;
        _isShown = true;
        UpdateButtonImageFlip();
        if (_slideCo != null) StopCoroutine(_slideCo);
        _slideCo = StartCoroutine(SlideTo(rect, shownAnchoredPos));
    }

    public void Hide()
    {
        var rect = GetSlideRect();
        if (rect == null) return;
        _isShown = false;
        UpdateButtonImageFlip();
        if (_slideCo != null) StopCoroutine(_slideCo);
        _slideCo = StartCoroutine(SlideTo(rect, hiddenAnchoredPos));
    }

    // 패널이 슬라이드로 들어가 있는(Show) 상태면 Y축 flip, 나가 있으면(Hide) 원래대로.
    void UpdateButtonImageFlip()
    {
        if (buttonImage == null) return;
        var s = buttonImage.localScale;
        s.y = _isShown ? -Mathf.Abs(s.y) : Mathf.Abs(s.y);
        buttonImage.localScale = s;
    }

    IEnumerator SlideTo(RectTransform rect, Vector2 target)
    {
        Vector2 start = rect.anchoredPosition;
        float t = 0f;
        float dur = Mathf.Max(0.0001f, slideDuration);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime; // 게임 일시정지 중에도 동작
            float k = Mathf.Clamp01(t / dur);
            k = 1f - (1f - k) * (1f - k); // ease-out
            rect.anchoredPosition = Vector2.Lerp(start, target, k);
            yield return null;
        }
        rect.anchoredPosition = target;
        _slideCo = null;
    }

    // 보유 직원 전체로 다시 빌드 — 저장 로드/씬 진입 시 호출
    public void Rebuild()
    {
        if (slotContainer == null || slotPrefab == null) return;
        if (EmployeeManager.Instance == null) return;

        // 기존 슬롯 모두 제거
        foreach (var kv in _slots)
            if (kv.Value != null) Destroy(kv.Value.gameObject);
        _slots.Clear();

        // 등급 내림차순 → 레벨 내림차순 → 직군(기획>개발>아트) — DispatchPanelUI/EmployeeListUI 와 동일 규칙
        var sorted = new List<EmployeeData>(EmployeeManager.Instance.ownedEmployees);
        sorted.Sort(CompareEmployees);
        foreach (var emp in sorted)
            AddSlot(emp.id);
    }

    public void AddSlot(string employeeId)
    {
        if (string.IsNullOrEmpty(employeeId)) return;
        if (slotContainer == null || slotPrefab == null) return;
        if (_slots.ContainsKey(employeeId)) return;

        var go = Instantiate(slotPrefab, slotContainer);
        var slot = go.GetComponent<EmployeeSatisfactionSlider>();
        if (slot == null) slot = go.GetComponentInChildren<EmployeeSatisfactionSlider>();
        if (slot == null)
        {
            Destroy(go);
            return;
        }
        slot.SetEmployee(employeeId);
        _slots[employeeId] = slot;

        // 정렬 유지 — 새로 추가된 슬롯을 정렬 기준에 맞는 위치로 이동 (Rebuild 순차 추가 시엔 항상 맨 뒤라 그대로 유지됨)
        var emp = EmployeeManager.Instance?.GetEmployee(employeeId);
        if (emp != null)
        {
            int index = 0;
            foreach (var kv in _slots)
            {
                if (kv.Key == employeeId) continue;
                var otherEmp = EmployeeManager.Instance.GetEmployee(kv.Key);
                if (otherEmp != null && CompareEmployees(otherEmp, emp) <= 0) index++;
            }
            go.transform.SetSiblingIndex(index);
        }
    }

    static int CompareEmployees(EmployeeData a, EmployeeData b)
    {
        int c = b.grade.CompareTo(a.grade);
        if (c != 0) return c;
        c = b.enhancementLevel.CompareTo(a.enhancementLevel);
        if (c != 0) return c;
        return RoleOrder(a.role).CompareTo(RoleOrder(b.role));
    }

    static int RoleOrder(EmployeeRole role) => role switch
    {
        EmployeeRole.Planner    => 0,
        EmployeeRole.Programmer => 1,
        EmployeeRole.Artist     => 2,
        _ => 3
    };

    public void RemoveSlot(string employeeId)
    {
        if (string.IsNullOrEmpty(employeeId)) return;
        if (_slots.TryGetValue(employeeId, out var slot))
        {
            if (slot != null) Destroy(slot.gameObject);
            _slots.Remove(employeeId);
        }
    }
}
