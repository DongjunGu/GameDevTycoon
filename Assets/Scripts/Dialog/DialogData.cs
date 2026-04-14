// =====================================================
// DialogData.cs
// 다이얼로그 시스템 데이터 모델 및 열거형 정의
// =====================================================

public enum SpeakerType { Player, NPC, System, None }
public enum TextSpeed { Normal, Fast, Slow }
public enum ConditionType { None, Gold, Level }
public enum ResultType { None, GoldChange, OpenHiring, SalaryUpdate, SalaryReject, SatisfactionChange, SalaryAccept, SalaryFreeze }

[System.Serializable]
public class DialogNodeData
{
    // 기본 식별
    public string dialogId;
    public string dialogGroupId;
    public int order;

    // 화자
    public SpeakerType speakerType;
    public string speakerId;
    public string speakerName;
    public string speakerPortraitId;

    // 대사
    public string dialogText;
    public TextSpeed textSpeed;

    // 분기
    public bool hasChoice;
    public string nextDialogId; // 비어있으면 종료

    // 연출
    public string portraitEmotion;
    public bool shakeEffect;
    public string bgmChangeId;
    public string sfxId;

    // 진행 방식
    public bool autoAdvance;
    public float autoDelay;
    public bool skippable;
}

[System.Serializable]
public class ChoiceData
{
    public string choiceId;
    public string parentDialogId;
    public string choiceText;
    public string nextDialogId;

    public ConditionType conditionType;
    public int conditionValue;

    public ResultType resultType;

    public int resultValue;
    public bool pauseAfterResult;
}