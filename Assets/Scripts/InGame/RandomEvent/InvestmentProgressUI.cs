using UnityEngine;
using TMPro;

public class InvestmentProgressUI : MonoBehaviour
{
    public static InvestmentProgressUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject progressPanel;

    [Header("UI")]
    public TextMeshProUGUI progressText;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        progressPanel.SetActive(false);
    }

    public void Show(string statName, float threshold)
    {
        progressPanel.SetActive(true);
        UpdateProgress(0f, statName, threshold);
    }

    public void UpdateProgress(float current, string statName, float threshold)
    {
        if (!progressPanel.activeSelf) return;
        progressText.text = $"투자 진행중\n목표 {statName}: {Mathf.RoundToInt(current)}/{Mathf.RoundToInt(threshold)}점";
    }

    public void Hide()
    {
        progressPanel.SetActive(false);
    }
}