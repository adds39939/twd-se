namespace TwdSaveEditor.Tools.ExtractSeason2Chapters.Model;

public sealed record StartingItem(string Item, IReadOnlyList<string> Requires, IReadOnlyList<string> Unless);
