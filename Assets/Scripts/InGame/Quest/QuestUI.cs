using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
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
        //questPanel.SetActive(false);
    }

    void Start()
    {
        // 정상 플로우(LoadingScene → GameScene)는 이 시점에 이미 BackendManager 로드가 끝나 있어
        // 한 프레임도 안 밀리고 바로 통과한다. 에디터에서 GameScene 을 직접 실행(EditorSceneBootstrap)
        // 할 때만 QuestManager.LoadQuests()가 아직 비동기로 진행 중일 수 있어 — 그 상태에서 바로
        // Refresh()하면 빈 목록으로 표시된 뒤 다시는 갱신되지 않아 "재접속하면 안 보인다"처럼 보였다.
        // GameSceneInitializer가 HasInitializedThisSession을 기다리는 것과 동일한 패턴.
        StartCoroutine(ShowWhenDataReady());
    }

    IEnumerator ShowWhenDataReady()
    {
        while (!BackendManager.HasInitializedThisSession)
            yield return null;
        Show();
    }

    public void Show()
    {
        Refresh();
        questPanel.SetActive(true);
    }

    public void Refresh()
    {
        foreach (Transform child in questListContent)
        {
            // [테스트] 사무실4단계 레이아웃 확인용 고정 표시 아이템 — Refresh 때 삭제되지 않게 예외.
            if (child.name == "QuestItemSimpleforTest2") continue;
            Destroy(child.gameObject);
        }


        var quests = QuestManager.Instance.GetAllQuests()
            .Where(q => q.isVisible && !q.isRewarded) // 수령 완료(=달성·자동수령 포함)는 UI에서 숨김 (뒤끝 행은 유지)
            .OrderBy(q => q.isMainQuest ? 0 : 1)                      // 메인퀘스트 상단 고정
            .ThenBy(q => q.isCompleted ? 1 : 0)                       // 진행중→완료 순
            .ToList();

        foreach (var quest in quests)
        {
            if (!quest.isVisible || quest.isRewarded) continue;
            var item = Instantiate(questItemPrefab, questListContent);

            var descText = item.transform.Find("DescText").GetComponent<TextMeshProUGUI>();
            var progressRow = item.transform.Find("ProgressRow");

            descText.text = quest.title;

            // 진행도(0/50,000 같은 숫자) 노출 전면 비활성화 — 모든 퀘스트 공통.
            progressRow.gameObject.SetActive(false);

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