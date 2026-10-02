namespace TwdSaveEditor.Season.Common.Model;

public sealed record InventoryState(string Owner, int Episode, string? Unavailable, IReadOnlyList<InventoryItem> Items, IReadOnlyList<string> Held)
{
    public bool Editable => Unavailable == null;

    public static InventoryState NotEditable(string owner, int episode, string reason) => new(owner, episode, reason, [], []);
}
