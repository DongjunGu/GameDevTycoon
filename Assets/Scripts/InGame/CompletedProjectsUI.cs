using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CompletedProjectsUI : MonoBehaviour
{
    public static CompletedProjectsUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject listPanel;
    public GameObject detailPanel;

    [Header("List")]
    public Transform listContent;
    public GameObject projectListItemPrefab;

    [Header("Detail")]
    public TextMeshProUGUI detailNameText;
    public TextMeshProUGUI detailScaleText;
    public TextMeshProUGUI detailGenreText;
    public TextMeshProUGUI detailPlatformText;
    public TextMeshProUGUI detailPlanningText;
    public TextMeshProUGUI detailDevelopText;
    public TextMeshProUGUI detailArtText;
    public TextMeshProUGUI detailCreativityText;
    public TextMeshProUGUI detailBugText;
    public TextMeshProUGUI detailUnitsText;
    public TextMeshProUGUI detailRevenueText;
    public TextMeshProUGUI detailDateText;
    public TextMeshProUGUI detailQualityScoreText;
    public TextMeshProUGUI detailCriticTotalText;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        listPanel.SetActive(false);
        detailPanel.SetActive(false);
    }

    public void Open()
    {
        GameTimeManager.Instance?.StopTime();
        gameObject.SetActive(true);
        detailPanel.SetActive(false);
        ShowList();
    }

    void ShowList()
    {
        foreach (Transform child in listContent)
            Destroy(child.gameObject);

        var projects = CompletedProjectManager.Instance.completedProjects;

        if (projects.Count == 0)
        {
            var empty = Instantiate(projectListItemPrefab, listContent);
            empty.GetComponentInChildren<TextMeshProUGUI>().text = "완료된 프로젝트가 없습니다.";
            empty.GetComponent<Button>().interactable = false;
        }
        else
        {
            // 최신순 정렬
            var sorted = new List<CompletedProjectData>(projects);

            sorted.Sort((a, b) =>
            {
                if (a.year != b.year) return b.year.CompareTo(a.year);
                if (a.month != b.month) return b.month.CompareTo(a.month);
                return b.week.CompareTo(a.week);
            });
            foreach (var project in sorted)
            {
                var item = Instantiate(projectListItemPrefab, listContent);
                item.GetComponentInChildren<TextMeshProUGUI>().text =
                    $"{project.projectName}  ({project.year}년 {project.month}월 {project.week}주)";

                var captured = project;
                item.GetComponent<Button>().onClick.AddListener(() => ShowDetail(captured));
            }
        }

        listPanel.SetActive(true);
    }

    void ShowDetail(CompletedProjectData data)
    {
        detailNameText.text = data.projectName;
        detailScaleText.text = $"규모: {ScaleToString((ProjectScale)data.scale)}";
        detailGenreText.text = $"장르: {GenreToString((ProjectGenre)data.genre)}";
        detailPlatformText.text = $"플랫폼: {PlatformToString((ProjectPlatform)data.platform)}";
        detailPlanningText.text = $"기획: {Mathf.RoundToInt(data.planning)}";
        detailDevelopText.text = $"개발: {Mathf.RoundToInt(data.develop)}";
        detailArtText.text = $"아트: {Mathf.RoundToInt(data.art)}";
        detailCreativityText.text = $"창의성: {Mathf.RoundToInt(data.creativity)}";
        detailBugText.text = $"버그: {Mathf.RoundToInt(data.bug)}";
        if (detailUnitsText != null) detailUnitsText.gameObject.SetActive(false); // 판매량 표시 제거 — 매출 단일화
        detailRevenueText.text = $"매출: {data.totalRevenue:N0}G";
        detailDateText.text = $"출시일: {data.year}년 {data.month}월 {data.week}주";
        if (detailQualityScoreText != null)
            detailQualityScoreText.text = $"품질 점수: {data.qualityScore:F1}";
        if (detailCriticTotalText != null)
            detailCriticTotalText.text = $"평론가 총점: {data.criticTotalScore}";

        listPanel.SetActive(false);
        detailPanel.SetActive(true);
    }

    public void OnClickBack()
    {
        detailPanel.SetActive(false);
        ShowList();
    }

    public void OnClickClose()
    {
        GameTimeManager.Instance?.StartTime();
        listPanel.SetActive(false);
    }

    string ScaleToString(ProjectScale scale) => scale switch
    {
        ProjectScale.Small => "소규모(1인개발)",
        ProjectScale.Medium => "중형(팀)",
        ProjectScale.Large => "대규모(AAA)",
        _ => ""
    };

    string GenreToString(ProjectGenre genre) => genre switch
    {
        ProjectGenre.RPG              => "RPG",
        ProjectGenre.FPS              => "FPS",
        ProjectGenre.Arcade           => "아케이드",
        ProjectGenre.HealingSimulation => "힐링시뮬레이션",
        ProjectGenre.Horror           => "공포",
        ProjectGenre.Idle             => "방치형",
        ProjectGenre.RTS              => "실시간전략",
        ProjectGenre.VisualNovel      => "미연시",
        ProjectGenre.Sports           => "스포츠",
        _ => ""
    };

    string PlatformToString(ProjectPlatform platform) => platform switch
    {
        ProjectPlatform.Mobile => "모바일",
        ProjectPlatform.PC => "PC",
        ProjectPlatform.Nintendo => "닌텐도",
        ProjectPlatform.Console => "콘솔",
        _ => ""
    };
}