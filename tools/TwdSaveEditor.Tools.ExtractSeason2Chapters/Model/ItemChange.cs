namespace TwdSaveEditor.Tools.ExtractSeason2Chapters.Model;

public sealed record ItemChange(string Item, bool Removes, IReadOnlyList<string> Scripts, string? ChapterId);
