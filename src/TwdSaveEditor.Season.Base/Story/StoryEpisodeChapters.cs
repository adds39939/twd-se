using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Season.Base.Story;

public sealed record StoryEpisodeChapters(int Episode, IReadOnlyList<StoryChapter> Chapters)
{
    public StoryChapter Opening => Chapters[0];

    public StoryChapter? Find(string id) =>
        Chapters.FirstOrDefault(chapter => chapter.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public StoryChapter? Generated(string script, PropertySet? flags) =>
        Chapters
            .Where(chapter => !chapter.StartsEpisode && chapter.Script.Equals(script, StringComparison.OrdinalIgnoreCase))
            .Where(chapter => chapter.Flags.All(flag => flags?.Find(flag.Key) != null))
            .OrderByDescending(chapter => chapter.Flags.Count)
            .ThenByDescending(chapter => chapter.Flags.All(flag => flag.IsSetIn(flags)))
            .FirstOrDefault();
}
