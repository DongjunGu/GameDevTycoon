using System.Collections.Generic;
using UnityEngine;

public class GenreFatigueManager : MonoBehaviour
{
    public static GenreFatigueManager Instance { get; private set; }

    private Dictionary<ProjectGenre, int> _fatigue = new();
    private ProjectGenre? _lastGenre = null;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (ProjectGenre g in System.Enum.GetValues(typeof(ProjectGenre)))
            _fatigue[g] = 0;
    }

    public int GetFatigue(ProjectGenre genre)
    {
        return _fatigue.TryGetValue(genre, out int v) ? v : 0;
    }

    public void UpdateFatigue(ProjectGenre genre)
    {
        // 선택한 장르 외 모든 장르 피로도 -1
        foreach (ProjectGenre g in System.Enum.GetValues(typeof(ProjectGenre)))
            if (g != genre)
                _fatigue[g] = Mathf.Max(0, _fatigue[g] - 1);

        // 직전과 같은 장르면 +2
        if (_lastGenre == genre)
            _fatigue[genre] = Mathf.Min(3, _fatigue[genre] + 2);

        _lastGenre = genre;
    }

    // 새 런 시작 — 모든 장르 피로도 0 초기화
    public void ResetForNewRun()
    {
        foreach (ProjectGenre g in System.Enum.GetValues(typeof(ProjectGenre)))
            _fatigue[g] = 0;
        _lastGenre = null;
    }

    public void RebuildFromHistory(List<CompletedProjectData> projects)
    {
        foreach (ProjectGenre g in System.Enum.GetValues(typeof(ProjectGenre)))
            _fatigue[g] = 0;
        _lastGenre = null;

        foreach (var p in projects)
            UpdateFatigue((ProjectGenre)p.genre);

        foreach (ProjectGenre g in System.Enum.GetValues(typeof(ProjectGenre)))
            if (_fatigue[g] > 0)
                Debug.Log($"[피로도] {g}: {_fatigue[g]}");
    }
}
