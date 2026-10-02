namespace TwdSaveEditor.Tools.ExtractSeason2Chapters.Model;

public sealed record ChapterItems(string Id, IReadOnlyList<string> Carried, IReadOnlyList<string> FromStart);
