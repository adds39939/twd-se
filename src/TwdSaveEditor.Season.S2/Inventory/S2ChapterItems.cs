namespace TwdSaveEditor.Season.S2.Inventory;

public sealed record S2ChapterItems(string Id, IReadOnlyList<string> Carried, IReadOnlyList<string> FromStart);
