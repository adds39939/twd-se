using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.DialogLog;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S2.Decisions;
using TwdSaveEditor.Season.S2.Saves;

namespace TwdSaveEditor.Season.S2.Inventory;

public sealed class S2Inventory(IS2ResumePoint resume) : IS2Inventory
{
    public const string Owner = "Clementine";
    public const string ItemsKey = "Items - " + Owner;
    public const string ShownSuffix = " - Shown";

    private const string ItemPrefix = "ui_item_";
    private const string HasPrefix = "bHas";
    private const string NoSave = "The inventory is kept in the save the game resumes from. This slot starts an episode from its beginning or is finished, so there is nothing to edit yet. Set a chapter under Resume Point first.";
    private const string Damaged = "The checkpoint of this save cannot be read, so its inventory cannot be edited.";
    private const string Unreadable = "Cannot set the inventory: the save's inventory properties cannot be read.";

    public InventoryState GetState(SaveSlot slot)
    {
        var state = resume.GetState(slot);
        if (resume.ResumeSave(slot) is not { } save)
        {
            return InventoryState.NotEditable(Owner, state.Episode, state.CheckpointDamaged ? Damaged : NoSave);
        }

        var file = save.FindFile(S2SlotFiles.InventoryProperties);
        if (file != null && !BundleReader.TryParseProperties(file))
        {
            return InventoryState.NotEditable(Owner, state.Episode, Damaged);
        }

        var episode = EpisodeOf(save);
        var held = file?.Properties?.GetStrings(ItemsKey) ?? [];
        var known = S2ItemCatalog.ForEpisode(episode)?.Items ?? [];
        var items = known.Select(item => new InventoryItem(item.Id, item.Name))
            .Concat(held.Where(id => known.All(item => item.Id != id)).Select(id => new InventoryItem(id, id)))
            .ToList();

        return new InventoryState(Owner, episode, null, items, [.. held.Select(id => new HeldItem(id))]);
    }

    public void SetItems(SaveSlot slot, IReadOnlyList<HeldItem> held)
    {
        var save = resume.ResumeSave(slot)
            ?? throw new InvalidOperationException("Cannot set the inventory: the slot has no save of the episode in progress.");

        var properties = DialogLogSaves.RuntimeProperties(save, S2SlotFiles.InventoryProperties, Unreadable);
        var previous = properties.GetStrings(ItemsKey) ?? [];
        var items = held.Where(item => item.Count > 0).Select(item => item.Id).Distinct(StringComparer.Ordinal).ToList();
        var game = resume.Properties(save, S2SlotFiles.LogicGameProperties);

        foreach (var removed in previous.Except(items, StringComparer.Ordinal))
        {
            properties.SetBool(removed + ShownSuffix, false);
            if (game?.GetBool(HasKey(removed)) == true)
            {
                game.SetBool(HasKey(removed), false);
            }
        }

        foreach (var item in items)
        {
            properties.SetBool(item + ShownSuffix, true);
        }

        properties.SetStrings(ItemsKey, items);
        save.Modified = true;
    }

    public IReadOnlyList<HeldItem> CarriedItems(SaveSlot slot)
    {
        if (resume.ResumeSave(slot) is not { } save)
        {
            return [];
        }

        var episode = EpisodeOf(save);
        if (S2ItemCatalog.ForEpisode(episode) is not { } catalog
            || resume.Chapter(save, episode) is not { } chapter
            || catalog.ForChapter(chapter.Id) is not { } items)
        {
            return [];
        }

        var log = new S2EventLogEditor(slot);
        bool Decided(string key) =>
            S2DecisionCatalog.All.FirstOrDefault(decision => decision.LogicKey == key) is { } decision && log.FindSeen(decision)?.LogicValue != null;

        var starting = items.FromStart.Where(item => catalog.Starting.Any(entry =>
            entry.Item == item && entry.Requires.All(Decided) && !entry.Unless.Any(Decided)));

        return [.. starting.Concat(items.Carried).Select(id => new HeldItem(id))];
    }

    private static int EpisodeOf(SaveSlot save) =>
        S2SlotFiles.EpisodeNumber(save.Metadata?.GetString(SaveMetadataKeys.Episode)) ?? S2ResumePoint.FirstEpisode;

    private static string HasKey(string itemId)
    {
        var key = S2ItemCatalog.All.Select(episode => episode.Find(itemId)).OfType<S2Item>().FirstOrDefault()?.Key
            ?? (itemId.StartsWith(ItemPrefix, StringComparison.Ordinal) ? itemId[ItemPrefix.Length..] : itemId);
        return HasPrefix + char.ToUpperInvariant(key[0]) + key[1..];
    }
}
