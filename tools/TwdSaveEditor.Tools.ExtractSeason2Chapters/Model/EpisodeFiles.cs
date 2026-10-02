namespace TwdSaveEditor.Tools.ExtractSeason2Chapters.Model;

public sealed record EpisodeFiles(string Scripts, string Extracted)
{
    private const int EpisodeBase = 200;

    public static EpisodeFiles For(string dataDirectory, int episode)
    {
        var archive = $"WDC_pc_WalkingDead{EpisodeBase + episode}_data";
        return new EpisodeFiles(Path.Combine(dataDirectory, "lua", archive), Path.Combine(dataDirectory, "extracted", archive));
    }
}
