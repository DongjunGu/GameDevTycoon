using UnityEngine;
using TMPro;

public class DevelopmentPanelUI : MonoBehaviour
{
    public static DevelopmentPanelUI Instance { get; private set; }

    [Header("UI")]
    public TextMeshProUGUI planningText;
    public TextMeshProUGUI developText;
    public TextMeshProUGUI artText;
    public TextMeshProUGUI bugText;
    public float GetBug() => _bug;

    private float _planning;
    private float _develop;
    private float _art;
    private float _bug;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void ResetValues()
    {
        _planning = 0f;
        _develop  = 0f;
        _art      = 0f;
    _bug      = 0f;        
        UpdateUI();
    }

    public void AddValues(float planning, float develop, float art, float bug = 0f)
    {
        _planning += planning;
        _develop  += develop;
        _art      += art;
        _bug      += bug;
        UpdateUI();
    }

    void UpdateUI()
    {
        planningText.text = $"기획: {Mathf.RoundToInt(_planning)}";
        developText.text  = $"개발: {Mathf.RoundToInt(_develop)}";
        artText.text      = $"아트: {Mathf.RoundToInt(_art)}";
        bugText.text      = $"버그: {Mathf.RoundToInt(_bug)}";
    }
    public void SetBug(float value)
{
    _bug = value;
    bugText.text = $"버그: {Mathf.RoundToInt(_bug)}";
}
}