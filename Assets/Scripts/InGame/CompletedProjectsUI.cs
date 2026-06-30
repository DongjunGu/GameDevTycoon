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
    public TextMeshProUGUI detailRevenueText;
    public TextMeshProUGUI detailCriticTotalText;   // 평점 (점수 박스 안의 큰 숫자, 라벨은 별도 정적 텍스트)
    public TextMeshProUGUI detailBestRankText;      // 최고 순위

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
            empty.transform.Find("NameText").GetComponent<TextMeshProUGUI>().text = "완료된 프로젝트가 없습니다.";
            empty.transform.Find("RevenueText").GetComponent<TextMeshProUGUI>().text = "";
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
                item.transform.Find("NameText").GetComponent<TextMeshProUGUI>().text = project.projectName;
                item.transform.Find("RevenueText").GetComponent<TextMeshProUGUI>().text = $"{project.totalRevenue:N0}G";

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
        detailRevenueText.text = $"매출: {data.totalRevenue:N0}G";
        if (detailCriticTotalText != null)
            detailCriticTotalText.text = $"{data.criticTotalScore}";
        if (detailBestRankText != null)
            detailBestRankText.text = data.bestRank > 0 ? $"최고 순위: {data.bestRank}위" : "최고 순위: 순위권 밖";

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
        ProjectScale.Small => "소규모",
        ProjectScale.Medium => "중형",
        ProjectScale.Large => "대형",
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
        ProjectPlatform.Console => "플레이스테이션",
        _ => ""
    };
}