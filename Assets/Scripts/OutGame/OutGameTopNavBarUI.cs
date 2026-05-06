using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 아웃게임 상단바: 골드/다이아 표시 + 우편함/설정 버튼
public class OutGameTopNavBarUI : MonoBehaviour
{
    [Header("Currency Texts")]
    public TMP_Text goldText;
    public TMP_Text diamondText;

    [Header("Buttons")]
    public Button mailboxButton;
    public Button settingsButton;

    [Header("Panels (버튼 클릭 시 열림)")]
    public GameObject mailboxPanel;
    public GameObject settingsPanel;

    [Header("Format")]
    [Tooltip("재화 표시 포맷 (string.Format용 {0})")]
    private string goldFormat    = "골드: {0:N0}";
    private string diamondFormat = "다이아: {0:N0}";

    void OnEnable()
    {
        if (OutGameCurrencyManager.Instance != null)
            OutGameCurrencyManager.Instance.OnChanged += Refresh;

        if (mailboxButton != null)
        {
            mailboxButton.onClick.RemoveListener(OnMailboxClicked);
            mailboxButton.onClick.AddListener(OnMailboxClicked);
        }
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OnSettingsClicked);
            settingsButton.onClick.AddListener(OnSettingsClicked);
        }

        if (mailboxPanel  != null) mailboxPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        Refresh();
    }

    void OnDisable()
    {
        if (OutGameCurrencyManager.Instance != null)
            OutGameCurrencyManager.Instance.OnChanged -= Refresh;

        if (mailboxButton  != null) mailboxButton.onClick.RemoveListener(OnMailboxClicked);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettingsClicked);
    }

    public void Refresh()
    {
        var cm = OutGameCurrencyManager.Instance;
        if (goldText != null)
            goldText.text = string.Format(goldFormat, cm != null ? cm.Gold : 0);
        if (diamondText != null)
            diamondText.text = string.Format(diamondFormat, cm != null ? cm.Diamond : 0);
    }

    void OnMailboxClicked()
    {
        if (mailboxPanel == null) return;
        bool on = !mailboxPanel.activeSelf;
        mailboxPanel.SetActive(on);
        if (on) mailboxPanel.transform.SetAsLastSibling();
    }

    void OnSettingsClicked()
    {
        if (settingsPanel == null) return;
        bool on = !settingsPanel.activeSelf;
        settingsPanel.SetActive(on);
        if (on) settingsPanel.transform.SetAsLastSibling();
    }
}
