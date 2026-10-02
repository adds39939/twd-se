namespace TwdSaveEditor.Tools.Common.Seasons;

public sealed record GameSeason(string Argument, string SeasonKey, int Number, int Episodes, string ArchiveTag, string EpisodeTag, string Project, bool MatchesBracedNodes, bool LoadedSceneRunsCheckpoint = false)
{
    public static readonly IReadOnlyList<GameSeason> Known =
    [
        new("2", "s2", 2, 5, "2", string.Empty, "S2", true),
        new("3", "s3", 3, 5, "3", string.Empty, "S3", true),
        new("4", "s4", 4, 4, "4", string.Empty, "S4", true, LoadedSceneRunsCheckpoint: true),
        new("m", "michonne", 1, 3, "M", "M", "Michonne", false),
    ];

    public static string Arguments => string.Join(" | ", Known.Select(season => season.Argument));

    public static GameSeason? Find(string argument) =>
        Known.FirstOrDefault(season => season.Argument.Equals(argument, StringComparison.OrdinalIgnoreCase));

    public string ProjectArchive => $"WDC_pc_ProjectSeason{ArchiveTag}_data";

    public string MenuArchive => $"WDC_pc_MenuSeason{ArchiveTag}_data";

    public string EpisodeArchive(int episode) => $"WDC_pc_WalkingDead{EpisodeTag}{Number}0{episode}_data";

    public string DataDirectory(string repositoryRoot) => Path.Combine(repositoryRoot, "src", $"TwdSaveEditor.Season.{Project}", "Data");

    public string DataFile(string repositoryRoot, string kind) => Path.Combine(DataDirectory(repositoryRoot), $"{SeasonKey}.{kind}.json");
}
