using LitJson;

public class CompletedProjectData
{
    public string rowInDate;
    public string projectName;
    public int    scale;
    public int    genre;
    public int    platform;
    public float  planning;
    public float  develop;
    public float  art;
    public float  creativity;
    public float  bug;
    public int    totalUnits;
    public int    totalRevenue;
    public int    year;
    public int    month;
    public int    week;

    public BackEnd.Param ToParam()
    {
        var param = new BackEnd.Param();
        param.Add("projectName", projectName);
        param.Add("scale",       scale);
        param.Add("genre",       genre);
        param.Add("platform",    platform);
        param.Add("planning",    planning);
        param.Add("develop",     develop);
        param.Add("art",         art);
        param.Add("creativity",  creativity);
        param.Add("bug",         bug);
        param.Add("totalUnits",  totalUnits);
        param.Add("totalRevenue",totalRevenue);
        param.Add("year",        year);
        param.Add("month",       month);
        param.Add("week",        week);
        return param;
    }

    public static CompletedProjectData FromServerRow(JsonData row)
    {
        var data = new CompletedProjectData();
        data.rowInDate    = SafeString(row, "inDate",      "");
        data.projectName  = SafeString(row, "projectName", "");
        data.scale        = SafeInt(row,    "scale",       0);
        data.genre        = SafeInt(row,    "genre",       0);
        data.platform     = SafeInt(row,    "platform",    0);
        data.planning     = SafeFloat(row,  "planning",    0f);
        data.develop      = SafeFloat(row,  "develop",     0f);
        data.art          = SafeFloat(row,  "art",         0f);
        data.creativity   = SafeFloat(row,  "creativity",  0f);
        data.bug          = SafeFloat(row,  "bug",         0f);
        data.totalUnits   = SafeInt(row,    "totalUnits",  0);
        data.totalRevenue = SafeInt(row,    "totalRevenue",0);
        data.year         = SafeInt(row,    "year",        2000);
        data.month        = SafeInt(row,    "month",       1);
        data.week         = SafeInt(row,    "week",        1);
        return data;
    }

    static int SafeInt(JsonData row, string key, int fallback)
    {
        try { return int.Parse(row[key].ToString()); }
        catch { return fallback; }
    }

    static float SafeFloat(JsonData row, string key, float fallback)
    {
        try { return float.Parse(row[key].ToString()); }
        catch { return fallback; }
    }

    static string SafeString(JsonData row, string key, string fallback)
    {
        try { return row[key].ToString(); }
        catch { return fallback; }
    }
}