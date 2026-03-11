using UnityEngine;
using TMPro;

public class MarketingNoticeUI : MonoBehaviour
{
    public static MarketingNoticeUI Instance { get; private set; }

    public GameObject noticePanel;
    public TextMeshProUGUI noticeText;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        noticePanel.SetActive(false);
    }

    public void Show(string name)
    {
        noticeText.text = $"마케팅 수행!\n{name}";
        noticePanel.SetActive(true);
    }

    public void OnClickConfirm()
    {
        noticePanel.SetActive(false);
    }
}