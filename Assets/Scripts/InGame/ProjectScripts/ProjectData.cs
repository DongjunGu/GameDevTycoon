public enum ProjectScale { Small, Medium, Large }
public enum ProjectGenre { RPG, FPS, Simulation, RhythmGame }
public enum ProjectPlatform { Mobile, PC, Nintendo, Console }

[System.Serializable]
public class ProjectData
{
    public ProjectScale scale;
    public ProjectGenre genre;
    public ProjectPlatform platform;

    public static int GetCost(ProjectScale scale) => scale switch
    {
        ProjectScale.Small  => 3000,
        ProjectScale.Medium => 15000,
        ProjectScale.Large  => 100000,
        _ => 0
    };

    public static int GetRecommendedStaff(ProjectScale scale) => scale switch
    {
        ProjectScale.Small  => 2,
        ProjectScale.Medium => 4,
        ProjectScale.Large  => 5,
        _ => 0
    };

    public int Cost => GetCost(scale);

    public string ScaleToString() => scale switch
    {
        ProjectScale.Small  => "소규모(4개월)",
        ProjectScale.Medium => "중형(6개월)",
        ProjectScale.Large  => "대규모(8개월)",
        _ => ""
    };

    public string ScaleInfoString() => scale switch
    {
        ProjectScale.Small  => "소규모(4개월)\n추천 인원: 2명  /  개발금: 3,000G",
        ProjectScale.Medium => "중형(6개월)\n추천 인원: 4명  /  개발금: 15,000G",
        ProjectScale.Large  => "대규모(8개월)\n추천 인원: 5명  /  개발금: 100,000G",
        _ => ""
    };

    public string GenreToString() => genre switch
    {
        ProjectGenre.RPG        => "RPG",
        ProjectGenre.FPS        => "FPS",
        ProjectGenre.Simulation => "시뮬레이션",
        ProjectGenre.RhythmGame => "리듬게임",
        _ => ""
    };

    public string PlatformToString() => platform switch
    {
        ProjectPlatform.Mobile   => "모바일",
        ProjectPlatform.PC       => "PC",
        ProjectPlatform.Nintendo => "닌텐도",
        ProjectPlatform.Console  => "콘솔",
        _ => ""
    };
}