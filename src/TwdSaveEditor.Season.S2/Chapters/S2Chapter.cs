namespace TwdSaveEditor.Season.S2.Chapters;

public sealed record S2Chapter(
    string Id,
    string Title,
    string Group,
    string Script,
    string ChapterId,
    bool StartsEpisode,
    IReadOnlyList<S2ChapterFlag> Flags,
    IReadOnlyList<string> Decided);
