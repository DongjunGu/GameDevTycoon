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

    [Header("라벨 (비워두면 버튼 자식의 TextMeshProUGUI를 자동으로 찾아서 씀)")]
    public TextMeshProUGUI lowLabel;
    public TextMeshProUGUI midLabel;
    public TextMeshProUGUI highLabel;

    void Start()
    {
        lowButton?.onClick.AddListener(() => Select(LeaderScoreAim.Low));
        midButton?.onClick.AddListener(() => Select(LeaderScoreAim.Mid));
        highButton?.onClick.AddListener(() => Select(LeaderScoreAim.High));
        HideButtons();
    }

    void Update()
    {
        // 계산 완료(IsPendingRound4Aim)가 아니라 "1~3회차 연출까지 다 끝난" 시점을 봐야 함 — 그래야
        // 패널 열리자마자 뜨지 않고 실제로 3회차 애니메이션이 끝난 뒤에 버튼이 나타난다.
        bool waiting = LeaderScoreUI.Instance != null && LeaderScoreUI.Instance.IsWaitingForRound4Aim;
        if (aimButtonsRoot != null && aimButtonsRoot.activeSelf != waiting)
        {
            aimButtonsRoot.SetActive(waiting);
            if (waiting) RefreshLabels(); // 지금 대기 중인 그 직원(K/S/M) 기준으로 예상 점수 범위 갱신
        }
    }

    void RefreshLabels()
    {
        SetLabel(lowButton, ref lowLabel, "약", LeaderScoreAim.Low);
        SetLabel(midButton, ref midLabel, "중", LeaderScoreAim.Mid);
        SetLabel(highButton, ref highLabel, "강", LeaderScoreAim.High);
    }

    // 이 조준을 고르면 스트레스(ds)가 얼마나 오를지 범위(반올림 정수)를 표시 — DevelopmentManager.GetAimDsRange가 단일 소스.
    void SetLabel(Button btn, ref TextMeshProUGUI label, string name, LeaderScoreAim aim)
    {
        if (label == null && btn != null) label = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (label == null || DevelopmentManager.Instance == null) return;

        var (min, max) = DevelopmentManager.Instance.GetAimDsRange(aim);
        label.text = $"{name} ({min}~{max})";
    }

    void HideButtons()
    {
        if (aimButtonsRoot != null) aimButtonsRoot.SetActive(false);
    }

    void Select(LeaderScoreAim aim)
    {
        Debug.Log($"[LeaderScoreAimButtons] Select({aim}) 호출됨 — DevelopmentManager.Instance={(DevelopmentManager.Instance != null)}");
        HideButtons();
        DevelopmentManager.Instance.SelectRound4Aim(aim);
    }
}
