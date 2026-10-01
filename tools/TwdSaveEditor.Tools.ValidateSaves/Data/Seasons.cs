namespace TwdSaveEditor.Tools.ValidateSaves.Data;

public static class Seasons
{
    public const string Season3 = "S3";
    public const string Season4 = "S4";
    public const string Michonne = "Michonne";

    public static readonly SeasonInfo[] All =
    [
        new("S1", "WDC_pc_ProjectSeason1_data.ttarch2", ChoiceFormat.ChoicesProp, "wd1_saveslot2.bundle"),
        new("S2", "WDC_pc_ProjectSeason2_data.ttarch2", ChoiceFormat.ChoicesProp, "wd2_saveslot1.bundle"),
        new(Season3, "WDC_pc_ProjectSeason3_data.ttarch2", ChoiceFormat.EventLog, "wd3_saveslot1.bundle"),
        new(Season4, "WDC_pc_ProjectSeason4_data.ttarch2", ChoiceFormat.ChoiceStats, "wd4_saveslot1.bundle"),
        new(Michonne, "WDC_pc_ProjectSeasonM_data.ttarch2", ChoiceFormat.EventLog, "wdm_saveslot4.bundle"),
    ];
}
