namespace TwdSaveEditor.Tools.ExtractSeason2Chapters.Model;

public sealed record EpisodeItems(int Episode, IReadOnlyList<Item> Items, IReadOnlyList<StartingItem> Starting, IReadOnlyList<ChapterItems> Chapters);
