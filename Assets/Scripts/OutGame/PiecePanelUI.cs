using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 아웃게임 "조각(Piece)" 패널 — CEO 능력치 강화 UI
//
// 좌측: 기획/개발/아트 3 row
//   - 강화 확률 % 라벨
//   - 10칸 슬라이더 (현재 단계 / 10)
//   - 단계 숫자 (사각형 안)
// 우측: 현재 스탯 박스 (기획/개발/아트 = base + stage*per)
// 하단: 강화하기 버튼 (TotalStage >= maxTotalStage 시 비활성)
//
// 인스펙터 연결: CEOManager(OnChanged 구독) 메타라 별도 인자 없음.
public class PiecePanelUI : MonoBehaviour
{
    [Header("Sliders (단계/10)")]
    public Slider planningSlider;
    public Slider developSlider;
    public Slider artSlider;

    [Header("Stage Texts (사각형 안 — 현재 단계)")]
    public TMP_Text planningStageText;
    public TMP_Text developStageText;
    public TMP_Text artStageText;

    [Header("Probability Texts (강화 확률 % )")]
    public TMP_Text planningProbText;
    public TMP_Text developProbText;
    public TMP_Text artProbText;

    [Header("Current Stats (우측 패널)")]
    public TMP_Text planningStatText;
    public TMP_Text developStatText;
    public TMP_Text artStatText;

    [Header("Buttons")]
    public Button upgradeButton;
    public Button resetButton;
    public Button listButton;

    [Header("Stone List Panel (보관함)")]
    [Tooltip("listButton 클릭 시 토글되는 패널 GameObject. 인스펙터에 StoneListPanelUI 가 붙어있어야 함.")]
    public GameObject listPanel;

    private bool _subscribed;

    void OnEnable()
    {
        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveAllListeners();
            upgradeButton.onClick.AddListener(OnClickUpgrade);
        }
        if (resetButton != null)
        {
            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(OnClickReset);
        }
        if (listButton != null)
        {
            listButton.onClick.RemoveAllListeners();
            listButton.onClick.AddListener(OnClickList);
        }
        if (listPanel != null) listPanel.SetActive(false);
        Subscribe();
        Refresh();
    }

    void OnClickList()
    {
        if (listPanel == null) return;
        listPanel.SetActive(!listPanel.activeSelf);
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void Subscribe()
    {
        if (_subscribed || CEOManager.Instance == null) return;
        CEOManager.Instance.OnChanged += Refresh;
        _subscribed = true;
    }

    void Unsubscribe()
    {
        if (!_subscribed || CEOManager.Instance == null) return;
        CEOManager.Instance.OnChanged -= Refresh;
        _subscribed = false;
    }

    void OnClickUpgrade()
    {
        if (CEOManager.Instance == null) return;
        if (!CEOManager.Instance.TryUpgrade(out _)) return;
        // OnChanged → Refresh 자동 호출
    }

    void OnClickReset()
    {
        if (CEOManager.Instance == null) return;
        if (ConfirmUI.Instance == null)
        {
            CEOManager.Instance.ResetAll();
            return;
        }
        ConfirmUI.Instance.Show(
            "현재 이 조각을 리셋하시겠습니까?",
            onConfirm: () => CEOManager.Instance.ResetAll(),
            onCancel:  () => { },
            confirmText: "예",
            cancelText:  "아니오"
        );
    }

    public void Refresh()
    {
        var mgr = CEOManager.Instance;
        if (mgr == null) return;

        // 단계 숫자
        if (planningStageText != null) planningStageText.text = mgr.PlanningStage.ToString();
        if (developStageText  != null) developStageText.text  = mgr.DevelopStage.ToString();
        if (artStageText      != null) artStageText.text      = mgr.ArtStage.ToString();

        // 슬라이더 (단계 / 10 — Slider value 는 0~1)
        if (planningSlider != null) planningSlider.value = mgr.PlanningStage / 10f;
        if (developSlider  != null) developSlider.value  = mgr.DevelopStage  / 10f;
        if (artSlider      != null) artSlider.value      = mgr.ArtStage      / 10f;

        // 강화 확률 (소수점 반올림)
        var probs = mgr.GetProbabilities();
        if (planningProbText != null) planningProbText.text = $"강화 확률 {Mathf.RoundToInt(probs[0] * 100f)}%";
        if (developProbText  != null) developProbText.text  = $"강화 확률 {Mathf.RoundToInt(probs[1] * 100f)}%";
        if (artProbText      != null) artProbText.text      = $"강화 확률 {Mathf.RoundToInt(probs[2] * 100f)}%";

        // 현재 스탯 (우측 패널)
        if (planningStatText != null) planningStatText.text = mgr.GetPlanning().ToString();
        if (developStatText  != null) developStatText.text  = mgr.GetDevelop().ToString();
        if (artStatText      != null) artStatText.text      = mgr.GetArt().ToString();

        // 강화 버튼 활성/비활성
        if (upgradeButton != null) upgradeButton.interactable = mgr.CanUpgrade();
    }
}
