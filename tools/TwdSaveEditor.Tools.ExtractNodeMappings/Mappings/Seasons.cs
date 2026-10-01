namespace TwdSaveEditor.Tools.ExtractNodeMappings.Mappings;

public static class Seasons
{
    public const string Season3 = "Season 3";
    public const string Michonne = "Michonne";
    public const string Season4 = "Season 4";

    public const string Season3Archive = "WDC_pc_ProjectSeason3_data.ttarch2";

    public static readonly string[] OutputOrder = [Season3, Michonne, Season4];

    public static readonly (string Season, string Archive)[] Archives =
    [
        (Michonne, "WDC_pc_ProjectSeasonM_data.ttarch2"),
        (Season4, "WDC_pc_ProjectSeason4_data.ttarch2"),
    ];
}
