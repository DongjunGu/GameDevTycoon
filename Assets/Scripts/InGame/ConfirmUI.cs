using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ConfirmUI : MonoBehaviour
{
    public static ConfirmUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject confirmPanel;

    [Header("UI")]
    public TextMeshProUGUI messageText;
    [Header("Buttons")]
    public Button confirmButton;
    public Button cancelButton;

    private System.Action _onConfirm;
    private System.Action _onCancel;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        confirmPanel.SetActive(false);

        confirmButton.onClick.AddListener(OnClickConfirm);
        cancelButton.onClick.AddListener(OnClickCancel);
    }

    public void Show(string message, System.Action onConfirm, System.Action onCancel = null)
    {
        messageText.text = message;
        _onConfirm = onConfirm;
        _onCancel = onCancel;
        confirmPanel.SetActive(true);
    }

    public void OnClickConfirm()
    {
        confirmPanel.SetActive(false);
        _onConfirm?.Invoke();
    }

    public void OnClickCancel()
    {
        confirmPanel.SetActive(false);
        _onCancel?.Invoke();
    }
}