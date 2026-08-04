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
        DontDestroyOnLoad(gameObject);
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

                        bool isMain = SafeInt(row, "isMainQuest", 0) == 1;
                        string unlockAfter = row.ContainsKey("unlockAfter") ? row["unlockAfter"]?.ToString() : "";
                        bool chartVisible = SafeInt(row, "isVisible", 0) == 1;
                    _quests.Add(new QuestData
                        {
                            questId     = row["questId"]?.ToString(),
                            title       = row["title"]?.ToString(),
                            description = row["description"]?.ToString(),
                            type        = questType,
                            targetValue = SafeInt(row, "targetValue", 0),
                            rewardGold  = SafeInt(row, "rewardGold", 0),
                            isMainQuest = isMain,
                            unlockAfter = unlockAfter,
                            isVisible   = chartVisible, // 차트 isVisible 컬럼으로 직접 제어
                            defaultIsVisible = chartVisible,
                        });
                    }
                }

                Debug.Log($"Quest 차트 로드 완료: {_quests.Count}개");
                LoadUserProgress(onComplete);
            });
        });
    }

    // 새 런 시작 — UserQuest 테이블 모든 row 삭제 + 퀘스트 진행 상태 초기화
    public void ResetForNewRun(System.Action onComplete = null)
    {
        // rowInDate 를 별도 문자열 리스트로 스냅샷 — 아래에서 q.rowInDate=null 로 초기화하면
        // _quests 와 같은 객체 참조라 캡처가 손상되기 때문
        var toDeleteIds = new List<string>();
        foreach (var q in _quests)
            if (!string.IsNullOrEmpty(q.rowInDate)) toDeleteIds.Add(q.rowInDate);

        // 메모리 초기화: 차트 정의(_quests 자체)는 유지, 진행 상태만 클리어
        foreach (var q in _quests)
        {
            q.currentValue = 0;
            q.isCompleted = false;
            q.isRewarded = false;
            q.isVisible = q.defaultIsVisible;
            q.rowInDate = null;
        }

        if (toDeleteIds.Count == 0) { onComplete?.Invoke(); return; }

        int pending = toDeleteIds.Count;
        foreach (var inDate in toDeleteIds)
        {
            Backend.GameData.DeleteV2("UserQuest", inDate, Backend.UserInDate, bro =>
            {
                if (!bro.IsSuccess()) Debug.LogError($"[Reset] UserQuest delete 실패: {bro}");
                pending--;
                if (pending == 0) onComplete?.Invoke();
            });
        }
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

        // 루프 시작 전 스냅샷 — 루프 중 isVisible 변경(체인 해금)으로 인한 중복 처리 방지
        var targets = _quests.FindAll(q => q.type == type && q.isVisible && !q.isCompleted);

        foreach (var quest in targets)
        {

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
        if (quest.isMainQuest)
        {
            quest.isRewarded = true; // 보상 없이 자동 완료 처리
            UnlockChainedMainQuests(quest.questId);
        }
        else
        {
            // 자동 수령: 보상 지급 + isRewarded=true → UI에서 즉시 사라짐 (뒤끝 행은 유지)
            MoneyManager.Instance.AddGold(quest.rewardGold);
            quest.isRewarded = true;
        }
    }

    void UnlockChainedMainQuests(string completedQuestId)
    {
        bool anyUnlocked = false;
        foreach (var q in _quests)
        {
            if (q.unlockAfter == completedQuestId && !q.isVisible)
            {
                q.isVisible = true;
                SaveQuest(q);
                anyUnlocked = true;
            }
        }
        if (anyUnlocked)
            QuestUI.Instance?.Refresh();
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
        GameTimeManager.Instance?.SaveGameTime();
        MoneyManager.Instance?.SaveMoney();
        ProjectSaveManager.Instance?.SaveProject();

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