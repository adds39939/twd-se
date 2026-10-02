namespace TwdSaveEditor.Season.S2.Inventory;

public sealed record S2StartingItem(string Item, IReadOnlyList<string> Requires, IReadOnlyList<string> Unless);
