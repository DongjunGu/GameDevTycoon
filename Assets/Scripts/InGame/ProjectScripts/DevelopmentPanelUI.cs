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
    public TextMeshProUGUI creativityText;
    public TextMeshProUGUI marketFitText;
    public float GetPlanning() => _planning;
    public float GetDevelop() => _develop;
    public float GetArt() => _art;
    public float GetBug() => _bug;
    public float GetCreativity() => _creativity;

    private float _planning;
    private float _develop;
    private float _art;
    private float _bug;
    private float _creativity;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }
    public void SetValues(float planning, float develop, float art, float bug, float creativity)
    {
        _planning = planning;
        _develop = develop;
        _art = art;
        _bug = bug;
        _creativity = creativity;
        UpdateUI();
    }

    public void ResetValues()
    {
        _planning = 0f;
        _develop = 0f;
        _art = 0f;
        _bug = 0f;
        _creativity = 0f;

        planningText.text = "";
        developText.text = "";
        artText.text = "";
        bugText.text = "";
        creativityText.text = "";
    }
    public void ResetMarketFit()
    {
        marketFitText.text = "";
    }

    public void AddValues(float planning, float develop, float art, float bug, float creativity = 0f)
    {
        _planning += planning;
        _develop += develop;
        _art += art;
        _bug += bug;
        _creativity += creativity;
        UpdateUI();
    }

    public void UpdateUI()
    {
        planningText.text = $"기획: {Mathf.RoundToInt(_planning)}";
        developText.text = $"개발: {Mathf.RoundToInt(_develop)}";
        artText.text = $"아트: {Mathf.RoundToInt(_art)}";
        bugText.text = $"버그: {Mathf.RoundToInt(_bug)}";
        creativityText.text = $"창의성: {Mathf.RoundToInt(_creativity)}";
    }
    public void SetBug(float value)
    {
        _bug = value;
        bugText.text = $"버그: {Mathf.RoundToInt(_bug)}";
    }
    public void UpdateMarketFit(ProjectGenre genre)
    {
        string genreName = genre switch
        {
            ProjectGenre.RPG => "RPG",
            ProjectGenre.FPS => "FPS",
            ProjectGenre.Simulation => "시뮬레이션",
            ProjectGenre.RhythmGame => "리듬게임",
            _ => ""
        };

        marketFitText.text = $"인기 장르: {genreName}";
    }
    public void MultiplyValues(float multiplier)
    {
        _planning *= multiplier;
        _develop *= multiplier;
        _art *= multiplier;
        UpdateUI();
    }
}