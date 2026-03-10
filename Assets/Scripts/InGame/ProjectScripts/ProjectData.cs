public enum ProjectScale { Small, Medium, Large }
public enum ProjectGenre { RPG, FPS, Simulation, RhythmGame }
public enum ProjectPlatform { Mobile, PC, Nintendo, Console }

[System.Serializable]
public class ProjectData
{
    public ProjectScale scale;
    public ProjectGenre genre;
    public ProjectPlatform platform;

    public string ScaleToString() => scale switch
    {
        ProjectScale.Small  => "소규모(1인개발)",
        ProjectScale.Medium => "중형(팀)",
        ProjectScale.Large  => "대규모(AAA)",
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