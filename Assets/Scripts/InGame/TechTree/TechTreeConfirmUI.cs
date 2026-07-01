using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TechTreeConfirmUI : MonoBehaviour
{
    public static TechTreeConfirmUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject confirmPanel;

    [Header("UI")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;

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

    // isUnlocked=true 면 포인트 라인 없이 설명만 표시
    public void Show(TechNodeData node, bool isUnlocked, bool canUnlock,
                     System.Action onConfirm, System.Action onCancel = null,
                     string confirmText = "해금", string cancelText = "취소")
    {
        _onConfirm = onConfirm;
        _onCancel  = onCancel;

        if (nameText != null) nameText.text = node.name;

        if (descriptionText != null)
        {
            if (isUnlocked)
            {
                descriptionText.text = node.description;
            }
            else
            {
                int pts = TechTreeManager.Instance != null ? TechTreeManager.Instance.CurrentPoints : 0;
                descriptionText.text =
                    $"{node.description}\n\n" +
                    $"<size=66%><color=#FF6464>필요 포인트: {node.requiredPoints} P</color>\n" +
                    $"<color=#663D45>보유 포인트: {pts} P</color></size>";
            }
        }

        if (confirmButtonText != null) confirmButtonText.text = isUnlocked ? "확인" : confirmText;
        if (cancelButtonText  != null) cancelButtonText.text  = cancelText;
        if (confirmButton     != null) confirmButton.interactable = isUnlocked || canUnlock;

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
