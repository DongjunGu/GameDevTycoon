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
        detailUnitsText.text = $"판매량: {data.totalUnits:N0}개";
        detailRevenueText.text = $"매출: {data.totalRevenue:N0}G";
        detailDateText.text = $"출시일: {data.year}년 {data.month}월 {data.week}주";

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
        ProjectGenre.RPG => "RPG",
        ProjectGenre.FPS => "FPS",
        ProjectGenre.Simulation => "시뮬레이션",
        ProjectGenre.RhythmGame => "리듬게임",
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