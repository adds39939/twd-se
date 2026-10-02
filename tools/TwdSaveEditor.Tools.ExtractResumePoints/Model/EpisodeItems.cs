namespace TwdSaveEditor.Tools.ExtractResumePoints.Model;

public sealed record EpisodeItems(int Episode, IReadOnlyList<Item> Items, IReadOnlyList<StartingItem> Starting, IReadOnlyList<ChapterItems> Chapters);
