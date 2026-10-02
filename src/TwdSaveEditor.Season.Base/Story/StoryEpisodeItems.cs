namespace TwdSaveEditor.Season.Base.Story;

public sealed record StoryEpisodeItems(int Episode, IReadOnlyList<StoryItem> Items, IReadOnlyList<StoryChapterItems> Chapters)
{
    public StoryChapterItems? ForChapter(string id) =>
        Chapters.FirstOrDefault(chapter => chapter.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
