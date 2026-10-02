namespace TwdSaveEditor.Season.Common.Model;

public sealed record InventoryState(
    string Owner,
    int Episode,
    string? Unavailable,
    IReadOnlyList<InventoryItem> Items,
    IReadOnlyList<HeldItem> Held,
    bool CarriedItemsKnown = true)
{
    public bool Editable => Unavailable == null;

    public int CountOf(string itemId) => Held.FirstOrDefault(item => item.Id == itemId)?.Count ?? 0;

    public static InventoryState NotEditable(string owner, int episode, string reason) => new(owner, episode, reason, [], [], false);
}
