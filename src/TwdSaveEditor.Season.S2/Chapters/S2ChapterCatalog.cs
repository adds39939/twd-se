using TwdSaveEditor.Season.Base.Resources;
using TwdSaveEditor.Season.Base.Story;

namespace TwdSaveEditor.Season.S2.Chapters;

public static class S2ChapterCatalog
{
    private const string ResourceSuffix = "s2.chapters.json";

    private static readonly Lazy<S2ChapterData> Data = new(Load);

    public static IReadOnlyList<StoryEpisodeChapters> All => Data.Value.Episodes;

    public static IReadOnlyList<S2ImportedKeys> ImportedKeys => Data.Value.ImportedKeys;

    public static StoryEpisodeChapters? ForEpisode(int episode) => All.FirstOrDefault(entry => entry.Episode == episode);

    private static S2ChapterData Load()
    {
        return EmbeddedSeasonData.Load<S2ChapterData>(typeof(S2ChapterCatalog).Assembly, ResourceSuffix) ?? new S2ChapterData([], []);
    }
}
