namespace TwdSaveEditor.Tools.ExtractResumePoints.Model;

public sealed record GameChapter(string ChapterId, string Dialog, IReadOnlyList<string> Scripts);
