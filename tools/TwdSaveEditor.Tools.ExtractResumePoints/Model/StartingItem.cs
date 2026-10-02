namespace TwdSaveEditor.Tools.ExtractResumePoints.Model;

public sealed record StartingItem(string Item, IReadOnlyList<string> Requires, IReadOnlyList<string> Unless);
