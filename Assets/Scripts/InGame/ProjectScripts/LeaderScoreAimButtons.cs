using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 4회차 조준(약/중/강) 선택 버튼 3개 ↔ DevelopmentManager 연결부.
// 이벤트 구독 대신 IsWaitingForRound4Aim을 매 프레임 폴링 — Awake/OnEnable 실행 순서에
// 좌우되지 않도록(LeaderScoreUI.Instance 세팅 타이밍 문제 회피).
public class LeaderScoreAimButtons : MonoBehaviour
{
    [Tooltip("버튼 3개를 감싸는 부모 (평소엔 비활성 상태로 둘 것)")]
    public GameObject aimButtonsRoot;
    public Button lowButton;
    public Button midButton;
    public Button highButton;

    [Header("라벨 (UText — 명칭만 표시, 비워두면 버튼 자식의 TextMeshProUGUI를 자동으로 찾아서 씀)")]
    public TextMeshProUGUI lowLabel;
    public TextMeshProUGUI midLabel;
    public TextMeshProUGUI highLabel;

    [Header("구간 라벨 (UInterval — \"(min~max)\" 형식)")]
    public TextMeshProUGUI lowIntervalLabel;
    public TextMeshProUGUI midIntervalLabel;
    public TextMeshProUGUI highIntervalLabel;

    [Header("등장 연출 (약→중→강 순서로 0.5초 간격, 아래에서 위로)")]
    public float riseDistance = 30f;
    public float riseDuration = 0.3f;
    public float riseStagger = 0.15f;

    Vector2 _lowRestPos, _midRestPos, _highRestPos;
    Sequence _riseSeq;

    void Start()
    {
        lowButton?.onClick.AddListener(() => Select(LeaderScoreAim.Low));
        midButton?.onClick.AddListener(() => Select(LeaderScoreAim.Mid));
        highButton?.onClick.AddListener(() => Select(LeaderScoreAim.High));

        // 클릭이 한 번도 없었던 이 시점(Start)에만 버튼 "자신"의 RectTransform이 진짜 위치를 갖는다 —
        // GlobalButtonClickBounce가 첫 클릭 때 __ClickBounceWrapper를 끼워넣으면서 버튼 자신의
        // anchoredPosition은 풀스트레치(0,0)로 덮어써버리므로, 이후로는 GetSlotRect()로 다시 찾아야 함.
        if (lowButton != null) _lowRestPos = lowButton.GetComponent<RectTransform>().anchoredPosition;
        if (midButton != null) _midRestPos = midButton.GetComponent<RectTransform>().anchoredPosition;
        if (highButton != null) _highRestPos = highButton.GetComponent<RectTransform>().anchoredPosition;

        // aimButtonsRoot의 HorizontalLayoutGroup은 자식(버튼)이 활성화될 때마다 리빌드되며 childControlWidth/
        // Height와 무관하게 anchoredPosition을 항상 강제 재배치한다 — 그래서 위에서 캐싱한 위치로 아무리
        // DOTween을 걸어도 매번 즉시 원위치로 스냅되어(한 프레임 안에) "순차 등장" 없이 한번에 나타난 것처럼
        // 보였다. 3개 버튼 배치는 고정이라 런타임에 리빌드될 필요가 없으므로, 디자인 배치를 그대로 이어받은
        // 지금 이 시점에 완전히 꺼서 이후로는 우리 코드가 준 anchoredPosition이 그대로 유지되게 한다.
        if (aimButtonsRoot != null)
        {
            var layoutGroup = aimButtonsRoot.GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup != null) layoutGroup.enabled = false;
        }

        HideButtons();
    }

    // 버튼이 아직 한 번도 클릭 안 됐으면 버튼 자신의 RectTransform을, 클릭돼서 GlobalButtonClickBounce가
    // __ClickBounceWrapper로 감싸버렸으면 그 래퍼(실제로 슬롯 위치/앵커를 물려받은 쪽)를 반환한다.
    // 매번 다시 조회해야 함 — 클릭은 언제든 일어날 수 있어 캐싱하면 그 시점 이후로 조용히 어긋난다.
    static RectTransform GetSlotRect(Button btn)
    {
        if (btn == null) return null;
        var rt = btn.transform as RectTransform;
        if (rt == null) return null;
        var parent = rt.parent;
        if (parent != null && parent.name == "__ClickBounceWrapper")
            return parent as RectTransform;
        return rt;
    }

    void Update()
    {
        // 계산 완료(IsPendingRound4Aim)가 아니라 "1~3회차 연출까지 다 끝난" 시점을 봐야 함 — 그래야
        // 패널 열리자마자 뜨지 않고 실제로 3회차 애니메이션이 끝난 뒤에 버튼이 나타난다.
        bool waiting = LeaderScoreUI.Instance != null && LeaderScoreUI.Instance.IsWaitingForRound4Aim;
        if (aimButtonsRoot != null && aimButtonsRoot.activeSelf != waiting)
        {
            aimButtonsRoot.SetActive(waiting);
            if (waiting)
            {
                RefreshLabels(); // 지금 대기 중인 그 직원(K/S/M) 기준으로 예상 점수 범위 갱신
                PlayRiseAnimation();
            }
        }
    }

    void PlayRiseAnimation()
    {
        _riseSeq?.Kill();
        _riseSeq = DOTween.Sequence().SetUpdate(true);
        InsertRise(lowButton, GetSlotRect(lowButton), _lowRestPos, 0f);
        InsertRise(midButton, GetSlotRect(midButton), _midRestPos, riseStagger);
        InsertRise(highButton, GetSlotRect(highButton), _highRestPos, riseStagger * 2f);
    }

    // 도미노처럼: 자기 차례가 되기 전까지는 버튼 자체를 꺼서 완전히 안 보이게 해두고, 차례가 되는 순간
    // SetActive(true)와 동시에 아래(riseDistance만큼)에서 원래 자리까지 위로 슬라이드만 시킨다(스케일
    // 애니메이션 없음 — "뽀잉" 하고 커지는 느낌 대신 순수하게 아래에서 위로 올라오는 느낌).
    void InsertRise(Button btn, RectTransform rt, Vector2 restPos, float delay)
    {
        if (rt == null) return;
        if (btn != null) btn.gameObject.SetActive(false);
        rt.anchoredPosition = restPos + new Vector2(0f, -riseDistance);
        rt.localScale = Vector3.one;

        if (btn != null) _riseSeq.InsertCallback(delay, () => btn.gameObject.SetActive(true));
        _riseSeq.Insert(delay, rt.DOAnchorPos(restPos, riseDuration).SetEase(Ease.OutCubic).SetUpdate(true));
    }

    void RefreshLabels()
    {
        SetLabel(lowLabel, "약");
        SetLabel(midLabel, "중");
        SetLabel(highLabel, "강");
        SetIntervalLabel(lowIntervalLabel, LeaderScoreAim.Low);
        SetIntervalLabel(midIntervalLabel, LeaderScoreAim.Mid);
        SetIntervalLabel(highIntervalLabel, LeaderScoreAim.High);
    }

    void SetLabel(TextMeshProUGUI label, string name)
    {
        if (label != null) label.text = name;
    }

    // 이 조준을 고르면 스트레스(ds)가 얼마나 오를지 범위(반올림 정수)를 UInterval에 "(min~max)" 형식으로
    // 표시 — DevelopmentManager.GetAimDsRange가 단일 소스.
    void SetIntervalLabel(TextMeshProUGUI intervalLabel, LeaderScoreAim aim)
    {
        if (intervalLabel == null || DevelopmentManager.Instance == null) return;

        var (min, max) = DevelopmentManager.Instance.GetAimDsRange(aim);
        intervalLabel.text = $"({min}~{max})";
    }

    void HideButtons()
    {
        _riseSeq?.Kill();
        if (aimButtonsRoot != null) aimButtonsRoot.SetActive(false);
    }

    void OnDestroy()
    {
        _riseSeq?.Kill();
    }

    void Select(LeaderScoreAim aim)
    {
        Debug.Log($"[LeaderScoreAimButtons] Select({aim}) 호출됨 — DevelopmentManager.Instance={(DevelopmentManager.Instance != null)}");
        HideButtons();
        DevelopmentManager.Instance.SelectRound4Aim(aim);
    }
}
