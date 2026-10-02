namespace TwdSaveEditor.Tools.ExtractSeason2Chapters.Model;

public sealed record GameChapter(string ChapterId, string Dialog, IReadOnlyList<string> Scripts);
