namespace TwdSaveEditor.Season.S3.Chapters;

public sealed record S3Chapter(
    string Id,
    string Title,
    string Group,
    string Script,
    bool StartsEpisode,
    IReadOnlyList<S3ChapterFlag> Flags,
    IReadOnlyList<string> Decided);
