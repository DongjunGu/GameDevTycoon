using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ProjectSetupUI : MonoBehaviour
{
    public static ProjectSetupUI Instance { get; private set; }

    // ── 패널 ──────────────────────────────────
    [Header("Panels")]
    public GameObject startPanel;       // 프로젝트 시작 버튼
    public GameObject scalePanel;       // 규모 선택
    public GameObject genrePanel;       // 장르 선택
    public GameObject platformPanel;    // 플랫폼 선택
    public GameObject summaryPanel;     // 선택 요약
    public GameObject confirmPanel;     // 최종 확정

    // ── 요약 패널 버튼 ────────────────────────
    [Header("Summary")]
    public TextMeshProUGUI summaryScaleText;
    public TextMeshProUGUI summaryGenreText;
    public TextMeshProUGUI summaryPlatformText;
    public Button summaryScaleButton;
    public Button summaryGenreButton;
    public Button summaryPlatformButton;

    // ── 최종 확정 패널 ────────────────────────
    [Header("Confirm")]
    public TextMeshProUGUI confirmText;
    public static ProjectScale SelectedScale { get; set; }
    public static ProjectGenre SelectedGenre { get; set; }
    public static ProjectPlatform SelectedPlatform { get; set; }

    // ── 내부 상태 ─────────────────────────────
    private ProjectData _projectData = new();
    private enum Step { Scale, Genre, Platform }
    private Step _editStep; // 요약에서 재선택 시 돌아갈 곳
    private bool _isEditing = false;
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        ShowPanel(startPanel);
    }

    // ── 패널 전환 ─────────────────────────────
    void ShowPanel(GameObject target)
    {
        //startPanel.SetActive(false);
        scalePanel.SetActive(false);
        genrePanel.SetActive(false);
        platformPanel.SetActive(false);
        summaryPanel.SetActive(false);
        confirmPanel.SetActive(false);

        target.SetActive(true);
    }

    // ══════════════════════════════════════════
    // 버튼 이벤트
    // ══════════════════════════════════════════

    // 프로젝트 시작
    public void OnClickProjectStart()
    {
        var stage = DevelopmentManager.Instance.CurrentStage;
        if (DevelopmentManager.Instance.IsStarted &&
            (stage == ProjectStage.Developing || stage == ProjectStage.BugFixing || stage == ProjectStage.Marketing))
        {
            AlertUI.Instance.Show("진행중인 프로젝트가 있습니다.");
            return;
        }
        ShowPanel(scalePanel);
    }

    // ── 규모 선택 ─────────────────────────────
    public void OnClickScale(int scale)
    {
        _projectData.scale = (ProjectScale)scale;

        if (_isEditing)
        {
            _isEditing = false;
            ShowSummary();
            return;
        }

        ShowPanel(genrePanel);
    }

    // ── 장르 선택 ─────────────────────────────
    public void OnClickGenre(int genre)
    {
        _projectData.genre = (ProjectGenre)genre;

        if (_isEditing)
        {
            _isEditing = false;
            ShowSummary();
            return;
        }

        ShowPanel(platformPanel);
    }

    // ── 플랫폼 선택 ───────────────────────────
    public void OnClickPlatform(int platform)
    {
        _projectData.platform = (ProjectPlatform)platform;
        _isEditing = false;
        ShowSummary();
    }

    // ── 요약 표시 ─────────────────────────────
    void ShowSummary()
    {
        summaryScaleText.text = _projectData.ScaleToString();
        summaryGenreText.text = _projectData.GenreToString();
        summaryPlatformText.text = _projectData.PlatformToString();

        summaryScaleButton.onClick.RemoveAllListeners();
        summaryScaleButton.onClick.AddListener(() =>
        {
            _isEditing = true; // ← 재선택 진입 시 true
            ShowPanel(scalePanel);
        });

        summaryGenreButton.onClick.RemoveAllListeners();
        summaryGenreButton.onClick.AddListener(() =>
        {
            _isEditing = true;
            ShowPanel(genrePanel);
        });

        summaryPlatformButton.onClick.RemoveAllListeners();
        summaryPlatformButton.onClick.AddListener(() =>
        {
            _isEditing = true;
            ShowPanel(platformPanel);
        });

        ShowPanel(summaryPanel);
    }

    // ── 확인 버튼 ─────────────────────────────
    public void OnClickConfirm()
    {
        confirmText.text =
            $"규모: {_projectData.ScaleToString()}\n" +
            $"장르: {_projectData.GenreToString()}\n" +
            $"플랫폼: {_projectData.PlatformToString()}";

        ShowPanel(confirmPanel);
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(
    confirmPanel.GetComponent<RectTransform>());
    }
    public void OnClickBackToSummary()
    {
        ShowSummary();
    }

    public void OnClickClose()
    {
        _projectData = new ProjectData();
        _isEditing = false;

        scalePanel.SetActive(false);
        genrePanel.SetActive(false);
        platformPanel.SetActive(false);
        summaryPanel.SetActive(false);
        confirmPanel.SetActive(false);
        startPanel.SetActive(true);
    }

    // ── 개발 시작 ─────────────────────────────
    public void OnClickStartDevelopment()
    {
        SelectedScale = _projectData.scale;
        SelectedGenre = _projectData.genre;
        SelectedPlatform = _projectData.platform;

        if (DevelopmentManager.Instance.CurrentStage == ProjectStage.Sales)
            SalesUI.Instance.NotifyNewProjectStarted();

        Debug.Log($"개발 시작! {_projectData.ScaleToString()} / {_projectData.GenreToString()} / {_projectData.PlatformToString()}");
        DevelopmentManager.Instance.StartDevelopment();
        confirmPanel.SetActive(false);
    }
}