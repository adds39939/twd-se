namespace TwdSaveEditor.Tools.ExtractResumePoints.Model;

public sealed record EpisodeFiles(string Scripts, string Extracted)
{
    private const int EpisodesPerSeason = 100;

    public static EpisodeFiles For(string dataDirectory, int season, int episode)
    {
        var archive = $"WDC_pc_WalkingDead{season * EpisodesPerSeason + episode}_data";
        return new EpisodeFiles(Path.Combine(dataDirectory, "lua", archive), Path.Combine(dataDirectory, "extracted", archive));
    }
}
