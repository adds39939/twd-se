namespace TwdSaveEditor.Tools.ExtractResumePoints.Model;

public sealed record ChapterItems(string Id, IReadOnlyList<string> Carried, IReadOnlyList<string> FromStart);
