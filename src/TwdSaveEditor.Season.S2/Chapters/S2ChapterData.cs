using TwdSaveEditor.Season.Base.Story;

namespace TwdSaveEditor.Season.S2.Chapters;

public sealed record S2ChapterData(IReadOnlyList<S2ImportedKeys> ImportedKeys, IReadOnlyList<StoryEpisodeChapters> Episodes);
