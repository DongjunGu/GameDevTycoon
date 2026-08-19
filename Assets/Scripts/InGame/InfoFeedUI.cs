using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 직원 머리 위 StatFloatingText(만족도/능력치 변화 등)를 대체하는 큐 형태 알림 목록.
// InfoPrefab 인스턴스를 container 맨 위(SetAsFirstSibling)에 계속 쌓고, 각자 displayDuration
// 뒤에 스스로 사라진다 — 개별 타이머가 생성 순서를 그대로 따르므로 먼저 뜬 알림이 먼저 사라지는
// 자연스러운 선입선출이 된다. [[project_leader_score_gostop]]과는 무관, 신규 시스템.
//
// InfoPrefab 루트는 container(InfoPanel)의 VerticalLayoutGroup이 세로 스택 위치를 직접 제어한다.
// 슬라이드는 루트가 아니라 한 단계 아래 자식 "Content"의 anchoredPosition을 움직여서 낸다 —
// LayoutGroup은 직계 자식만 건드리므로 손자뻘인 Content는 그 영향을 받지 않는다.
// [[feedback_layoutgroup_child_position_animation]] 참고.
public class InfoFeedUI : MonoBehaviour
{
    public static InfoFeedUI Instance { get; private set; }

    [Tooltip("InfoPrefab 인스턴스가 쌓이는 부모 — VerticalLayoutGroup 권장(새 알림이 맨 위)")]
    public RectTransform container;
    [Tooltip("Assets/Prefabs/InfoPrefab.prefab")]
    public GameObject infoPrefab;
    public float displayDuration = 2.5f;

    [Header("Slide")]
    public Vector2 shownAnchoredPos = Vector2.zero;
    public Vector2 hiddenAnchoredPos = new Vector2(700f, 0f);
    public float slideInDuration = 0.2f;
    public float slideOutDuration = 0.3f;

    const string GoodColorHex = "#E63356";
    const string BadColorHex  = "#517FFF";

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // 만족도 변화 — "{이름}의 만족도가 n 상승/하락했다." (이름 색상: 상승=good/하락=bad)
    public void ShowSatisfaction(EmployeeData emp, int delta)
    {
        if (emp == null || delta == 0) return;
        bool good = delta > 0;
        string dir = good ? "상승" : "하락";
        Spawn(emp, $"{Colorize(emp.employeeName, good)}의 만족도가 {Colorize(Mathf.Abs(delta).ToString(), good)} {dir}했다.");
    }

    // 능력치 버프/너프 — "{이름}의 능력치가 n주간 n% 버프/너프" (이름+n%+버프/너프 색상: 버프=good/너프=bad)
    public void ShowStatBuff(EmployeeData emp, int weeks, int percent, bool isBuff)
    {
        if (emp == null) return;
        string word = Colorize(isBuff ? "버프" : "너프", isBuff);
        Spawn(emp, $"{Colorize(emp.employeeName, isBuff)}의 능력치가 {weeks}주간 {Colorize($"{Mathf.Abs(percent)}%", isBuff)} {word}");
    }

    // 잭팟(각성 모드) — "{이름}의 각성 모드 발동" (이름+각성 색상: 항상 good)
    public void ShowJackpot(EmployeeData emp)
    {
        if (emp == null) return;
        Spawn(emp, $"{Colorize(emp.employeeName, true)}의 {Colorize("각성", true)} 모드 발동");
    }

    // 개발 지연 — "개발기간이 n주 지연되었다." (개발기간/n주 색상: 항상 bad)
    public void ShowDevelopmentDelay(EmployeeData emp, int weeks)
    {
        Spawn(emp, $"{Colorize("개발기간", false)}이 {Colorize($"{weeks}주", false)} 지연되었다.");
    }

    // 정형화된 템플릿에 안 맞는 경우(디버프 회복 등) — 직접 만든 리치텍스트 메시지를 그대로 표시.
    // Colorize()로 직원이름 등 색을 맞춰서 넘길 것.
    public void ShowCustom(EmployeeData emp, string richMessage) => Spawn(emp, richMessage);

    // 특정 직원이 없는(전체 대상) 메시지 — 초상화 없음(InfoPortraitImage는 GameObject 유지, Image만 비활성화)
    public void ShowGlobal(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        Spawn(null, message);
    }

    // 전체 직원 만족도 변화 — "모든 직원의 만족도가 n 상승/하락했다." ("모든 직원"+n 색상: 상승=good/하락=bad)
    public void ShowGlobalSatisfaction(int delta)
    {
        if (delta == 0) return;
        bool good = delta > 0;
        string dir = good ? "상승" : "하락";
        Spawn(null, $"{Colorize("모든 직원", good)}의 만족도가 {Colorize(Mathf.Abs(delta).ToString(), good)} {dir}했다.");
    }

    public static string Colorize(string text, bool good) => $"<color={(good ? GoodColorHex : BadColorHex)}>{text}</color>";

    void Spawn(EmployeeData emp, string richMessage)
    {
        if (infoPrefab == null || container == null) return;
        // 이 오브젝트(또는 조상)가 비활성이면 StartCoroutine 자체가 예외를 던져 아래 코루틴이 시작되지 못하고
        // 방금 만든 InfoPrefab이 슬라이드아웃/Destroy 없이 영구히 남는다 — 예: ConfirmPanelMoneyElevator가
        // HUD를 통째로 숨기는 패널(EmployeePanel 등)이 열려 있는 동안 아이템 사용 등으로 호출된 경우.
        // 아무도 못 보는 상태라 표시할 이유도 없으므로 조용히 스킵한다.
        if (!isActiveAndEnabled) return;

        var go = Instantiate(infoPrefab, container);
        go.transform.SetAsFirstSibling(); // 새로 생긴 게 맨 위로

        var text = go.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null) text.text = richMessage;

        var portraitImg = FindPortraitImage(go);
        if (portraitImg != null)
        {
            Sprite sp = (emp != null && !string.IsNullOrEmpty(emp.portraitId))
                ? Resources.Load<Sprite>($"Portraits/Mini/{emp.portraitId}")
                : null;
            portraitImg.sprite = sp;
            // 특정 직원이 없는 알림(전체 대상)은 InfoPortraitImage GameObject를 끄지 않고
            // Image 컴포넌트만 비활성화 — 레이아웃 슬롯(HorizontalLayoutGroup 폭)은 그대로 유지.
            portraitImg.enabled = sp != null;
        }

        var content = go.transform.Find("Content") as RectTransform;
        StartCoroutine(SlideLifecycle(go, content));
    }

    static Image FindPortraitImage(GameObject go)
    {
        var t = go.transform.Find("Content/InfoPortrait/InfoPortraitImage");
        return t != null ? t.GetComponent<Image>() : null;
    }

    IEnumerator SlideLifecycle(GameObject go, RectTransform content)
    {
        if (content != null)
        {
            content.anchoredPosition = hiddenAnchoredPos;
            yield return SlideTo(content, hiddenAnchoredPos, shownAnchoredPos, slideInDuration);
        }

        float t = 0f;
        float hold = Mathf.Max(0f, displayDuration);
        while (t < hold)
        {
            if (GameTimeManager.Instance == null || GameTimeManager.Instance.IsRunning)
                t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (content != null)
            yield return SlideTo(content, shownAnchoredPos, hiddenAnchoredPos, slideOutDuration);

        if (go != null) Destroy(go);
    }

    IEnumerator SlideTo(RectTransform rect, Vector2 start, Vector2 end, float duration)
    {
        float dur = Mathf.Max(0.0001f, duration);
        float t = 0f;
        while (t < dur)
        {
            if (GameTimeManager.Instance == null || GameTimeManager.Instance.IsRunning)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                k = 1f - (1f - k) * (1f - k); // ease-out
                rect.anchoredPosition = Vector2.Lerp(start, end, k);
            }
            yield return null;
        }
        rect.anchoredPosition = end;
    }
}
