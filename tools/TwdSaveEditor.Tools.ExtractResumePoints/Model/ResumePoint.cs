namespace TwdSaveEditor.Tools.ExtractResumePoints.Model;

public sealed record ResumePoint(
    string Id,
    string Title,
    string Group,
    string Script,
    string ChapterId,
    bool StartsEpisode,
    IReadOnlyList<Flag> Flags,
    IReadOnlyList<string> Decided,
    SceneEntry? Entry);
