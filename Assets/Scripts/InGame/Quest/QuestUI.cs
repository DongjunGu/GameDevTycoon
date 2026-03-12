using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuestUI : MonoBehaviour
{
    public static QuestUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject questPanel;
    [Header("Scroll")]
    public RectTransform questListContent; // Scroll View Content
    public GameObject questItemPrefab;     // 퀘스트 아이템 프리팹
    [ContextMenu("퀘스트 테스트")]
    public void TestShow()
    {
        Show();
    }
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        questPanel.SetActive(false);
    }

    public void Show()
    {
        Refresh();
        questPanel.SetActive(true);
    }

    public void Refresh()
    {
        foreach (Transform child in questListContent)
            Destroy(child.gameObject);

        var quests = QuestManager.Instance.GetAllQuests();

        foreach (var quest in quests)
        {
            var item = Instantiate(questItemPrefab, questListContent);

            // TopRow
            var titleText = item.transform.Find("TopRow/QuestInfo/TitleText").GetComponent<TextMeshProUGUI>();
            var descText = item.transform.Find("TopRow/QuestInfo/DescText").GetComponent<TextMeshProUGUI>();
            var rewardText = item.transform.Find("TopRow/Reward/RewardText").GetComponent<TextMeshProUGUI>();

            // ProgressArea
            var progressLabel = item.transform.Find("ProgressArea/ProgressRow/ProgressLabel").GetComponent<TextMeshProUGUI>();
            var progressValue = item.transform.Find("ProgressArea/ProgressRow/ProgressValueText").GetComponent<TextMeshProUGUI>();
            var progressSlider = item.transform.Find("ProgressArea/ProgressSlider").GetComponent<Slider>();

            // 값 세팅
            titleText.text = quest.title;
            descText.text = quest.description;
            rewardText.text = $"{quest.rewardGold:N0}G";

            progressLabel.text = "진행도";
            progressValue.text = $"{quest.currentValue:N0} / {quest.targetValue:N0}";
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.value = quest.Progress;

            // 배경 이미지
            var itemImage = item.GetComponent<Image>();

            // 상태별 색상
            if (quest.isRewarded)
            {
                // 수령완료 → 흐리게
                itemImage.color = new Color(0.3f, 0.3f, 0.3f, 0.3f);
            }
            else if (quest.isCompleted)
            {
                // 완료 (수령 전) → 초록
                itemImage.color = new Color(0.2f, 0.8f, 0.4f, 0.3f);

                // 아이템 버튼으로 클릭 가능
                var btn = item.GetComponent<Button>();
                if (btn == null) btn = item.AddComponent<Button>();

                var capturedQuest = quest;
                btn.onClick.AddListener(() =>
                {
                    QuestManager.Instance.ClaimReward(capturedQuest);
                    Refresh();
                });
            }
            else
            {
                // 진행중 → 기본
                itemImage.color = new Color(1f, 1f, 1f, 0.05f);
            }
        }
    }

    public void OnClickClose()
    {
        questPanel.SetActive(false);
    }
}