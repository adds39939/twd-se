namespace TwdSaveEditor.Season.Base.Story;

public sealed record StoryChapter(
    string Id,
    string Title,
    string Group,
    string Script,
    string ChapterId,
    bool StartsEpisode,
    IReadOnlyList<StoryChapterFlag> Flags,
    IReadOnlyList<string> Decided);
