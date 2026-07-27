public enum ProjectScale { Small, Medium, Large }
public enum ProjectGenre { RPG, FPS, Arcade, HealingSimulation, Horror, Idle, RTS, VisualNovel, Sports, Puzzle }
public enum ProjectPlatform { Mobile, PC, Nintendo, Console }

[System.Serializable]
public class ProjectData
{
    public ProjectScale scale;
    public ProjectGenre genre;
    public ProjectPlatform platform;

    public static int GetCost(ProjectScale scale) => scale switch
    {
        ProjectScale.Small  => 1000,
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
        ProjectScale.Small  => "소형",
        ProjectScale.Medium => "중형",
        ProjectScale.Large  => "대형",
        _ => ""
    };

    public string ScaleInfoString() => scale switch
    {
        ProjectScale.Small  => $"소형\n추천 인원: {GetRecommendedStaff(ProjectScale.Small)}명  /  개발금: {GetCost(ProjectScale.Small):N0} G",
        ProjectScale.Medium => $"중형\n추천 인원: {GetRecommendedStaff(ProjectScale.Medium)}명  /  개발금: {GetCost(ProjectScale.Medium):N0} G",
        ProjectScale.Large  => $"대형\n추천 인원: {GetRecommendedStaff(ProjectScale.Large)}명  /  개발금: {GetCost(ProjectScale.Large):N0} G",
        _ => ""
    };

    public string GenreToString() => genre switch
    {
        ProjectGenre.RPG              => "RPG",
        ProjectGenre.FPS              => "FPS",
        ProjectGenre.Arcade           => "아케이드",
        ProjectGenre.HealingSimulation => "힐링시뮬레이션",
        ProjectGenre.Horror           => "공포",
        ProjectGenre.Idle             => "방치형",
        ProjectGenre.RTS              => "실시간전략",
        ProjectGenre.VisualNovel      => "미연시",
        ProjectGenre.Sports           => "스포츠",
        ProjectGenre.Puzzle           => "퍼즐",
        _ => ""
    };

    public string PlatformToString() => platform switch
    {
        ProjectPlatform.Mobile   => "모바일",
        ProjectPlatform.PC       => "PC",
        ProjectPlatform.Nintendo => "닌텐도",
        ProjectPlatform.Console  => "플레이스테이션",
        _ => ""
    };
}