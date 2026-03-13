using System.Collections.Generic;
using UnityEngine;
using BackEnd;
using LitJson;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    private List<QuestData> _quests = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void LoadQuests(System.Action onComplete = null)
    {
        Backend.CDN.Content.Table.Get(tableBro =>
        {
            if (!tableBro.IsSuccess())
            {
                Debug.LogError($"차트 테이블 조회 실패: {tableBro}");
                onComplete?.Invoke();
                return;
            }

            var tableList = tableBro.GetContentTableItemList();
            if (tableList.Count == 0)
            {
                Debug.LogError("차트 테이블 비어있음");
                onComplete?.Invoke();
                return;
            }

            Backend.CDN.Content.Get(tableList, null, contentBro =>
            {
                if (!contentBro.IsSuccess())
                {
                    Debug.LogError($"차트 내용 조회 실패: {contentBro}");
                    onComplete?.Invoke();
                    return;
                }

                _quests.Clear();

                var dic = contentBro.GetContentDictionarySortByChartId();
                foreach (var key in dic.Keys)
                {
                    var item = dic[key];
                    if (item.chartName != "Quest") continue;
                    if (string.IsNullOrEmpty(item.contentString)) continue;

                    JsonData rows = LitJson.JsonMapper.ToObject(item.contentString);
                    foreach (JsonData row in rows)
                    {
                        string typeStr = row["type"]?.ToString();
                        if (!System.Enum.TryParse(typeStr, out QuestType questType)) continue;

                        _quests.Add(new QuestData
                        {
                            questId     = row["questId"]?.ToString(),
                            title       = row["title"]?.ToString(),
                            description = row["description"]?.ToString(),
                            type        = questType,
                            targetValue = SafeInt(row, "targetValue", 0),
                            rewardGold  = SafeInt(row, "rewardGold", 0),
                            isVisible   = false, // 기본 숨김
                        });
                    }
                }

                Debug.Log($"Quest 차트 로드 완료: {_quests.Count}개");
                LoadUserProgress(onComplete);
            });
        });
    }

    void LoadUserProgress(System.Action onComplete)
    {
        Backend.GameData.GetMyData("UserQuest", new Where(), bro =>
        {
            if (bro.IsSuccess())
            {
                var rows = bro.FlattenRows();
                foreach (JsonData row in rows)
                {
                    string questId = row["questId"]?.ToString();
                    var quest = _quests.Find(q => q.questId == questId);
                    if (quest == null) continue;

                    quest.currentValue = SafeInt(row, "currentValue", 0);
                    quest.isCompleted  = row["isCompleted"]?.ToString() == "True";
                    quest.isRewarded   = row["isRewarded"]?.ToString() == "True";
                    quest.isVisible    = row["isVisible"]?.ToString() == "True";
                    quest.rowInDate    = row["inDate"]?.ToString();
                }
            }

            Debug.Log("UserQuest 진행상황 로드 완료");
            onComplete?.Invoke();
        });
    }

    // 퀘스트 공개 (특정 액션 발생 시 호출)
    public void UnlockQuest(string questId)
    {
        var quest = _quests.Find(q => q.questId == questId);
        if (quest == null)
        {
            Debug.LogError($"퀘스트 없음: {questId}");
            return;
        }

        if (quest.isVisible) return; // 이미 공개됨

        quest.isVisible = true;
        SaveQuest(quest);

        Debug.Log($"퀘스트 공개: {quest.title}");
        QuestUI.Instance?.Refresh();
    }

    public void UpdateProgress(QuestType type, int value)
    {
        bool anyUpdated = false;

        foreach (var quest in _quests)
        {
            if (quest.type != type) continue;
            if (!quest.isVisible) continue;   // 공개된 퀘스트만 진행
            if (quest.isCompleted) continue;

            quest.currentValue += value;

            if (quest.currentValue >= quest.targetValue)
            {
                quest.currentValue = quest.targetValue;
                quest.isCompleted  = true;
                Debug.Log($"퀘스트 완료: {quest.title}");
                OnQuestCompleted(quest);
            }

            SaveQuest(quest);
            anyUpdated = true;
        }

        if (anyUpdated)
            QuestUI.Instance?.Refresh();
    }

    void OnQuestCompleted(QuestData quest)
    {
        AlertUI.Instance.Show(
            $"퀘스트 완료!\n{quest.title}\n보상: {quest.rewardGold:N0}G",
            () => QuestUI.Instance?.Show()
        );
    }

    public void ClaimReward(QuestData quest)
    {
        if (quest.isRewarded) return;
        quest.isRewarded = true;

        MoneyManager.Instance.AddGold(quest.rewardGold);
        Debug.Log($"보상 지급: {quest.rewardGold}G");

        SaveQuest(quest);
        QuestUI.Instance?.Refresh();
    }

    void SaveQuest(QuestData quest)
    {
        var param = new Param();
        param.Add("questId",      quest.questId);
        param.Add("currentValue", quest.currentValue);
        param.Add("isCompleted",  quest.isCompleted);
        param.Add("isRewarded",   quest.isRewarded);
        param.Add("isVisible",    quest.isVisible);

        if (!string.IsNullOrEmpty(quest.rowInDate))
        {
            Backend.GameData.UpdateV2("UserQuest", quest.rowInDate, Backend.UserInDate, param, bro =>
            {
                if (!bro.IsSuccess())
                    Debug.LogError($"퀘스트 업데이트 실패: {bro}");
            });
        }
        else
        {
            Backend.GameData.Insert("UserQuest", param, bro =>
            {
                if (bro.IsSuccess())
                {
                    quest.rowInDate = bro.GetInDate();
                    Debug.Log($"퀘스트 Insert 완료: {quest.questId}");
                }
                else
                {
                    Debug.LogError($"퀘스트 Insert 실패: {bro}");
                }
            });
        }
    }

    public List<QuestData> GetAllQuests() => _quests;

    int SafeInt(JsonData row, string key, int fallback)
    {
        if (row.ContainsKey(key) && int.TryParse(row[key]?.ToString(), out int val))
            return val;
        return fallback;
    }
}