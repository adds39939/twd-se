using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.Story;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S4.Story;

namespace TwdSaveEditor.Season.S4.Collectibles;

public sealed class S4Collectibles : IS4Collectibles
{
    public const string Owner = "Clementine";

    private const string NoSlot = "The collectibles are kept in the slot file, which this save does not have.";

    public InventoryState GetState(SaveSlot slot)
    {
        var episode = S4Story.Resume.GetState(slot).Episode;
        if (slot.Metadata is not { } metadata)
            return InventoryState.NotEditable(Owner, episode, NoSlot);

        var items = All().ToList();
        return new InventoryState(
            Owner,
            episode,
            null,
            [.. items.Select(entry => new InventoryItem(entry.Item.Id, $"{entry.Item.Name} (Episode {entry.Episode})"))],
            [.. items.Where(entry => metadata.GetBool(entry.Item.Id) == true).Select(entry => new HeldItem(entry.Item.Id))],
            false);
    }

    public void SetItems(SaveSlot slot, IReadOnlyList<HeldItem> held)
    {
        var metadata = slot.Metadata
            ?? throw new InvalidOperationException("Cannot set the collectibles: the save has no slot metadata.");

        foreach (var (_, item) in All())
        {
            var wanted = held.Any(entry => entry.Id == item.Id && entry.Count > 0);
            if (wanted)
                metadata.SetBool(item.Id, true);
            else
                metadata.Remove(Symbol.FromString(item.Id));
        }
    }

    private static IEnumerable<(int Episode, StoryItem Item)> All() =>
        Enumerable.Range(StorySeason.FirstEpisode, S4Story.Season.LastEpisode)
            .SelectMany(episode => (S4Story.Season.ItemsOf(episode)?.Items ?? []).Select(item => (episode, item)));
}
