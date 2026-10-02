using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.SaveGames;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S2.Decisions;
using TwdSaveEditor.Season.S2.Saves;

namespace TwdSaveEditor.Season.S2.Inventory;

public static class S2Inventory
{
    public const string Owner = "Clementine";
    public const string ItemsKey = "Items - " + Owner;
    public const string ShownSuffix = " - Shown";

    private const string ItemPrefix = "ui_item_";
    private const string HasPrefix = "bHas";
    private const string NoSave = "The inventory is kept in the save the game resumes from. This slot starts an episode from its beginning or is finished, so there is nothing to edit yet. Set a chapter under Resume Point first.";
    private const string Damaged = "The checkpoint of this save cannot be read, so its inventory cannot be edited.";

    public static InventoryState GetState(SaveSlot slot)
    {
        var resume = S2ResumePoint.GetState(slot);
        if (S2ResumePoint.ResumeSave(slot) is not { } save)
            return InventoryState.NotEditable(Owner, resume.Episode, resume.CheckpointDamaged ? Damaged : NoSave);

        var file = save.FindFile(S2SlotFiles.InventoryProperties);
        if (file != null && !BundleReader.TryParseProperties(file))
            return InventoryState.NotEditable(Owner, resume.Episode, Damaged);

        var episode = EpisodeOf(save);
        var held = file?.Properties?.GetStrings(ItemsKey) ?? [];
        var known = S2ItemCatalog.ForEpisode(episode)?.Items ?? [];
        var items = known.Select(item => new InventoryItem(item.Id, item.Name))
            .Concat(held.Where(id => known.All(item => item.Id != id)).Select(id => new InventoryItem(id, id)))
            .ToList();

        return new InventoryState(Owner, episode, null, items, [.. held]);
    }

    public static void SetItems(SaveSlot slot, IReadOnlyList<string> itemIds)
    {
        var save = S2ResumePoint.ResumeSave(slot)
            ?? throw new InvalidOperationException("Cannot set the inventory: the slot has no save of the episode in progress.");

        var properties = InventoryProperties(save);
        var previous = properties.GetStrings(ItemsKey) ?? [];
        var items = itemIds.Distinct(StringComparer.Ordinal).ToList();
        var game = S2ResumePoint.Properties(save, S2SlotFiles.LogicGameProperties);

        foreach (var removed in previous.Except(items, StringComparer.Ordinal))
        {
            properties.SetBool(removed + ShownSuffix, false);
            if (game?.GetBool(HasKey(removed)) == true)
                game.SetBool(HasKey(removed), false);
        }

        foreach (var item in items)
            properties.SetBool(item + ShownSuffix, true);

        properties.SetStrings(ItemsKey, items);
        save.Modified = true;
    }

    public static IReadOnlyList<string> CarriedItems(SaveSlot slot)
    {
        if (S2ResumePoint.ResumeSave(slot) is not { } save)
            return [];

        var episode = EpisodeOf(save);
        if (S2ItemCatalog.ForEpisode(episode) is not { } catalog
            || S2ResumePoint.Chapter(save, episode) is not { } chapter
            || catalog.ForChapter(chapter.Id) is not { } items)
        {
            return [];
        }

        var log = new S2EventLogEditor(slot);
        bool Decided(string key) =>
            S2DecisionCatalog.All.FirstOrDefault(decision => decision.LogicKey == key) is { } decision && log.FindSeen(decision)?.LogicValue != null;

        var starting = items.FromStart.Where(item => catalog.Starting.Any(entry =>
            entry.Item == item && entry.Requires.All(Decided) && !entry.Unless.Any(Decided)));

        return [.. starting.Concat(items.Carried)];
    }

    private static int EpisodeOf(SaveSlot save) =>
        S2SlotFiles.EpisodeNumber(save.Metadata?.GetString(SaveMetadataKeys.Episode)) ?? S2ResumePoint.FirstEpisode;

    private static string HasKey(string itemId)
    {
        var key = S2ItemCatalog.All.Select(episode => episode.Find(itemId)).OfType<S2Item>().FirstOrDefault()?.Key
            ?? (itemId.StartsWith(ItemPrefix, StringComparison.Ordinal) ? itemId[ItemPrefix.Length..] : itemId);
        return HasPrefix + char.ToUpperInvariant(key[0]) + key[1..];
    }

    private static PropertySet InventoryProperties(SaveSlot save)
    {
        var name = S2SlotFiles.InventoryProperties;
        if (save.FindFile(name) is { } existing)
        {
            return BundleReader.TryParseProperties(existing)
                ? existing.Properties!
                : throw new InvalidOperationException("Cannot set the inventory: the save's inventory properties cannot be read.");
        }

        var properties = S2SlotFiles.NewRuntimeProperties();
        var files = save.Files;
        var position = files.FindIndex(file => file.Name.Length == 0 && file.NameSymbol > name);
        files.Insert(position < 0 ? files.Count : position, new BundleFileEntry
        {
            NameField = new byte[BundleFileEntry.NameFieldSize],
            NameSymbol = name,
            TypeSymbol = TelltaleTypes.PropertySet,
            Data = [],
            Properties = properties,
        });

        if (save.FindFile(BundleFileNames.SaveGame) is { } saveGame)
        {
            var state = SaveGameCodec.Read(saveGame.Data);
            if (!state.RuntimePropertyNames.Contains(name))
            {
                var index = state.RuntimePropertyNames.FindIndex(existingName => existingName > name);
                state.RuntimePropertyNames.Insert(index < 0 ? state.RuntimePropertyNames.Count : index, name);
                saveGame.Data = SaveGameCodec.Write(state);
            }
        }

        return properties;
    }
}
