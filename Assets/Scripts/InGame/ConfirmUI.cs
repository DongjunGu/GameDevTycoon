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
    public TextMeshProUGUI confirmButtonText;
    public TextMeshProUGUI cancelButtonText;

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

    // confirmInteractable=false 면 확인 버튼을 표시는 하되 비활성(터치 불가)으로 띄운다. 매 호출마다 리셋되므로 기본값 true 콜러는 영향 없음.
    public void Show(string message, System.Action onConfirm, System.Action onCancel = null,
                     string confirmText = "확인", string cancelText = "취소", bool confirmInteractable = true)
    {
        GameTimeManager.Instance?.StopTime();
        ModalGate.I.Register(this);
        messageText.text       = message;
        _onConfirm             = onConfirm;
        _onCancel              = onCancel;
        if (confirmButtonText != null) confirmButtonText.text = confirmText;
        if (cancelButtonText  != null) cancelButtonText.text  = cancelText;
        if (confirmButton     != null) confirmButton.interactable = confirmInteractable;
        confirmPanel.SetActive(true);
    }

    public void OnClickConfirm()
    {
        confirmPanel.SetActive(false);
        GameTimeManager.Instance?.StartTime();
        ModalGate.I.Unregister(this);
        _onConfirm?.Invoke();
    }

    public void OnClickCancel()
    {
        confirmPanel.SetActive(false);
        GameTimeManager.Instance?.StartTime();
        ModalGate.I.Unregister(this);
        _onCancel?.Invoke();
    }
}