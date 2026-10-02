namespace TwdSaveEditor.Season.S2.Inventory;

public sealed record S2EpisodeItems(int Episode, IReadOnlyList<S2Item> Items, IReadOnlyList<S2StartingItem> Starting, IReadOnlyList<S2ChapterItems> Chapters)
{
    public S2Item? Find(string id) => Items.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public S2ChapterItems? ForChapter(string id) =>
        Chapters.FirstOrDefault(chapter => chapter.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
