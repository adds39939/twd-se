namespace TwdSaveEditor.Season.S3.Inventory;

public sealed record S3EpisodeItems(int Episode, IReadOnlyList<S3Item> Items, IReadOnlyList<S3ChapterItems> Chapters)
{
    public S3ChapterItems? ForChapter(string id) =>
        Chapters.FirstOrDefault(chapter => chapter.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
