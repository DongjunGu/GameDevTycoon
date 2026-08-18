using System.Collections.Generic;
using UnityEngine;
using BackEnd;
using LitJson;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    // 튜토리얼 런에서만 첫 화면에 뜨는 퀘스트 / 그 외(일반) 런에서만 뜨는 첫 퀘스트.
    // tutorial_quest_002는 tutorial_quest_001의 unlockAfter 체인으로만 공개되므로 별도 분기가
    // 필요 없다 — 001이 튜토리얼 런에서만 보이고 완료되니, 002도 자연히 튜토리얼 런에서만 열린다.
    const string TutorialFirstQuestId    = "tutorial_quest_001";
    const string NonTutorialFirstQuestId = "quest_revenue_50000";

    private List<QuestData> _quests = new();

    // 차트 isVisible(defaultVisible)을 기준으로, 튜토리얼/비튜토리얼 전용 첫 퀘스트만 런 종류에 따라
    // 강제로 뒤집는다. 나머지 퀘스트는 차트 값 그대로.
    static bool ResolveTutorialVisibility(string questId, bool defaultVisible, bool isTutorial)
    {
        if (questId == TutorialFirstQuestId) return isTutorial;
        if (questId == NonTutorialFirstQuestId) return !isTutorial;
        return defaultVisible;
    }

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
                        string questId = row["questId"]?.ToString();
                        string typeStr = row["type"]?.ToString();
                        if (!System.Enum.TryParse(typeStr, out QuestType questType)) continue;

                        bool isMain = SafeInt(row, "isMainQuest", 0) == 1;
                        string unlockAfter = row.ContainsKey("unlockAfter") ? row["unlockAfter"]?.ToString() : "";
                        bool chartVisible = SafeInt(row, "isVisible", 0) == 1;
                        bool isTutorial = RunStateManager.Instance != null && RunStateManager.Instance.IsTutorial;
                        bool resolvedVisible = ResolveTutorialVisibility(questId, chartVisible, isTutorial);
                    _quests.Add(new QuestData
                        {
                            questId     = questId,
                            title       = row["title"]?.ToString(),
                            description = row["description"]?.ToString(),
                            type        = questType,
                            targetValue = SafeInt(row, "targetValue", 0),
                            rewardGold  = SafeInt(row, "rewardGold", 0),
                            isMainQuest = isMain,
                            unlockAfter = unlockAfter,
                            isVisible   = resolvedVisible, // 차트 isVisible + 튜토리얼/비튜토리얼 분기
                            defaultIsVisible = chartVisible, // 리셋 시 기준값은 차트 원본(분기는 ResetForNewRun에서 다시 적용)
                        });
                    }
                }

                Debug.Log($"Quest 차트 로드 완료: {_quests.Count}개");
                LoadUserProgress(onComplete);
            });
        });
    }

    // 새 런 시작 — UserQuest 테이블 모든 row 삭제 + 퀘스트 진행 상태 초기화.
    // tutorial: 이번에 시작하는 런이 튜토리얼인지 — NewRunInitializer가 RunState.tutorial 저장 "전"에
    // 이 함수를 호출하므로(RunStateManager.Instance.IsTutorial이 아직 "이전" 런 값), 반드시 인자로
    // 직접 받아야 한다(LoadQuests()의 RunStateManager.Instance.IsTutorial 참조와는 다름).
    public void ResetForNewRun(System.Action onComplete = null, bool tutorial = false)
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
            q.isVisible = ResolveTutorialVisibility(q.questId, q.defaultIsVisible, tutorial);
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
                    quest.isCompleted  = SafeBool(row, "isCompleted");
                    quest.isRewarded   = SafeBool(row, "isRewarded");
                    quest.isVisible    = SafeBool(row, "isVisible");
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

    // 뒤끝 SDK가 bool 컬럼을 "True"/"true"/"1"로 섞어서 내려줄 수 있어(RunStateManager.ParseBool과
    // 동일한 이유), 대문자 "True" 완전일치만 보던 예전 코드는 재접속 시 저장된 행을 다시 읽을 때
    // isVisible/isCompleted/isRewarded가 전부 false로 잘못 파싱돼 QuestUI에서 사라지는 버그가 있었다.
    static bool SafeBool(JsonData row, string key)
    {
        if (!row.ContainsKey(key)) return false;
        var s = row[key]?.ToString();
        return s == "True" || s == "true" || s == "1";
    }
}