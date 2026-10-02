namespace TwdSaveEditor.Season.S1.Inventory;

public sealed record S1EpisodeItems(int Episode, IReadOnlyList<S1Item> Items, IReadOnlyList<S1ChapterItems> Chapters)
{
    public S1Item? Find(string id) => Items.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public S1ChapterItems? ForChapter(string id) => Chapters.FirstOrDefault(chapter => chapter.Id == id);
}
