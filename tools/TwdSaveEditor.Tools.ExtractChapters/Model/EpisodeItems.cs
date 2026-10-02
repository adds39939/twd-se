namespace TwdSaveEditor.Tools.ExtractChapters.Model;

public sealed record EpisodeItems(int Episode, IReadOnlyList<ItemDefinition> Items, IReadOnlyList<ChapterItems> Chapters);
