using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class MarketingItemUI : MonoBehaviour
{
    public TextMeshProUGUI platformText;
    public TextMeshProUGUI costText;

    private int _cost;
    private System.Action _onClick;

    public void Setup(string platformName, int cost, System.Action onClick)
    {
        platformText.text = platformName;
        costText.text     = $"{cost:N0}원";
        _cost             = cost;
        _onClick          = onClick;
    }

    public void OnClickItem()
    {
        _onClick?.Invoke();
    }
}