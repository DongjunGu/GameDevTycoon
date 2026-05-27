using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RandomEventUI : MonoBehaviour
{
    public static RandomEventUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject eventPanel;

    [Header("UI")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI systemMessageText;
    public Image portraitImage;

    private RandomEventData _currentEvent;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        eventPanel.SetActive(false);
    }

    // 다른 모달(AlertUI / 다른 이벤트 패널)이 떠 있으면 닫힐 때까지 대기 후 표시 — 동시 표시 방지.
    public void Show(RandomEventData evt)
    {
        ModalGate.I.WhenFree(() => DisplayInternal(evt));
    }

    void DisplayInternal(RandomEventData evt)
    {
        _currentEvent = evt;
        titleText.text = evt.title;
        descriptionText.text = evt.description;

        if (systemMessageText != null)
        {
            bool hasSystem = !string.IsNullOrEmpty(evt.systemMessage);
            systemMessageText.text = evt.systemMessage;
            systemMessageText.gameObject.SetActive(hasSystem || evt.keepSystemMessageActive);
        }

        if (portraitImage != null)
        {
            Sprite portrait = !string.IsNullOrEmpty(evt.portraitId)
                ? Resources.Load<Sprite>($"Portraits/{evt.portraitId}")
                : null;
            portraitImage.sprite = portrait;
            portraitImage.gameObject.SetActive(portrait != null);
        }

        eventPanel.SetActive(true);
        ModalGate.I.Register(this);
    }

    // 확인 버튼
    public void OnClickConfirm()
    {
        // Unregister 시 대기 큐의 다음 모달이 즉시 표시되며 _currentEvent 가 바뀔 수 있으므로 먼저 캡처.
        var evt = _currentEvent;

        eventPanel.SetActive(false);
        ModalGate.I.Unregister(this);
        evt?.onApply?.Invoke();
        ProjectSaveManager.Instance.SaveProject();
        GameTimeManager.Instance.SaveGameTime();
        EmployeeManager.Instance.SaveAllEmployees();

        // 개발중 이벤트일 때만 재개
        if (evt != null &&
            evt.type != RandomEventType.EmployeeRun &&
            evt.type != RandomEventType.EmployeeFight &&
            evt.type != RandomEventType.BadCompany &&
            evt.type != RandomEventType.Recruit)
        {
            DevelopmentManager.Instance.ResumeFromEvent();
        }
    }
}