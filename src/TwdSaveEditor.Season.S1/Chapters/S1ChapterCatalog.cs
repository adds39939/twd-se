using TwdSaveEditor.Season.Base.Resources;

namespace TwdSaveEditor.Season.S1.Chapters;

public static class S1ChapterCatalog
{
    private const string ResourceSuffix = "s1.chapters.json";

    private static readonly Lazy<IReadOnlyList<S1EpisodeChapters>> Episodes = new(Load);

    public static IReadOnlyList<S1EpisodeChapters> All => Episodes.Value;

    public static S1EpisodeChapters? ForEpisode(int episode) => All.FirstOrDefault(entry => entry.Episode == episode);

    private static IReadOnlyList<S1EpisodeChapters> Load()
    {
        return EmbeddedSeasonData.Load<List<S1EpisodeChapters>>(typeof(S1ChapterCatalog).Assembly, ResourceSuffix) ?? [];
    }
}
