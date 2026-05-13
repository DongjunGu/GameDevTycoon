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

    public void Show(RandomEventData evt)
    {
        _currentEvent = evt;
        titleText.text = evt.title;
        descriptionText.text = evt.description;

        if (systemMessageText != null)
        {
            bool hasSystem = !string.IsNullOrEmpty(evt.systemMessage);
            systemMessageText.text = evt.systemMessage;
            systemMessageText.gameObject.SetActive(hasSystem);
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
        eventPanel.SetActive(false);
        ModalGate.I.Unregister(this);
        _currentEvent?.onApply?.Invoke();
        ProjectSaveManager.Instance.SaveProject();
        GameTimeManager.Instance.SaveGameTime();
        EmployeeManager.Instance.SaveAllEmployees();

        // 개발중 이벤트일 때만 재개
        if (_currentEvent != null &&
            _currentEvent.type != RandomEventType.EmployeeRun &&
            _currentEvent.type != RandomEventType.EmployeeFight &&
            _currentEvent.type != RandomEventType.BadCompany)
        {
            DevelopmentManager.Instance.ResumeFromEvent();
        }
    }
}