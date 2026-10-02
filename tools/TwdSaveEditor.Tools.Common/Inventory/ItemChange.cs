namespace TwdSaveEditor.Tools.Common.Inventory;

public sealed record ItemChange(string Item, bool Removes, IReadOnlyList<string> Scripts, string? ChapterId);
