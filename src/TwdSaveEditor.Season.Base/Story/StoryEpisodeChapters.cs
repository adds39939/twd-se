namespace TwdSaveEditor.Season.Base.Story;

public sealed record StoryEpisodeChapters(int Episode, IReadOnlyList<StoryChapter> Chapters)
{
    public StoryChapter Opening => Chapters[0];

    public StoryChapter? Find(string id) =>
        Chapters.FirstOrDefault(chapter => chapter.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
