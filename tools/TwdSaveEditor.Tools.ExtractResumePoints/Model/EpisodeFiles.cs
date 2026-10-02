using TwdSaveEditor.Tools.Common.Seasons;

namespace TwdSaveEditor.Tools.ExtractResumePoints.Model;

public sealed record EpisodeFiles(string Scripts, string Extracted)
{
    public static EpisodeFiles For(string dataDirectory, GameSeason season, int episode)
    {
        var archive = season.EpisodeArchive(episode);
        return new EpisodeFiles(Path.Combine(dataDirectory, "lua", archive), Path.Combine(dataDirectory, "extracted", archive));
    }
}
