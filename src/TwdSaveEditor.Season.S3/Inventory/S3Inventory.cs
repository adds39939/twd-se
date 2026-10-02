using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.DialogLog;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S3.Saves;

namespace TwdSaveEditor.Season.S3.Inventory;

public static class S3Inventory
{
    public const string Owner = "Javier";

    private const string NoSave = "The inventory is kept in the save the game resumes from. This slot starts an episode from its beginning or is finished, so there is nothing to edit yet. Set a chapter under Resume Point first.";
    private const string Damaged = "The save of this slot cannot be read, so its inventory cannot be edited.";
    private const string NoItems = "Javier carries no items in this episode; the season only has items in Episodes 1 and 2.";
    private const string Unreadable = "Cannot set the inventory: the save's inventory properties cannot be read.";

    private static readonly ulong[] Sets = [S3SlotFiles.OwnerInventoryProperties, S3SlotFiles.InventoryProperties];

    public static InventoryState GetState(SaveSlot slot)
    {
        var resume = S3ResumePoint.GetState(slot);
        if (S3ResumePoint.ResumeSave(slot) is not { } save)
            return InventoryState.NotEditable(Owner, resume.Episode, resume.CheckpointDamaged ? Damaged : NoSave);

        var episode = EpisodeOf(save);
        if (S3ItemCatalog.ForEpisode(episode) is not { Items.Count: > 0 } catalog)
            return InventoryState.NotEditable(Owner, episode, NoItems);

        if (Sets.Any(name => save.FindFile(name) is { } file && !BundleReader.TryParseProperties(file)))
            return InventoryState.NotEditable(Owner, episode, Damaged);

        return new InventoryState(
            Owner,
            episode,
            null,
            [.. catalog.Items.Select(item => new InventoryItem(item.Id, item.Name))],
            [.. catalog.Items.Where(item => Count(save, item.Id) > 0).Select(item => new HeldItem(item.Id))],
            S3ResumePoint.GeneratedChapter(save, episode) != null);
    }

    public static void SetItems(SaveSlot slot, IReadOnlyList<HeldItem> held)
    {
        var save = S3ResumePoint.ResumeSave(slot)
            ?? throw new InvalidOperationException("Cannot set the inventory: the slot has no save of the episode in progress.");

        foreach (var item in S3ItemCatalog.ForEpisode(EpisodeOf(save))?.Items ?? [])
        {
            var wanted = held.Any(entry => entry.Id == item.Id && entry.Count > 0) ? 1 : 0;
            if (wanted == Math.Min(Count(save, item.Id), 1))
                continue;

            foreach (var name in Sets)
                DialogLogSaves.RuntimeProperties(save, name, Unreadable).SetInt(item.Id, wanted);

            save.Modified = true;
        }
    }

    public static IReadOnlyList<HeldItem> CarriedItems(SaveSlot slot)
    {
        if (S3ResumePoint.ResumeSave(slot) is not { } save)
            return [];

        var episode = EpisodeOf(save);
        return S3ResumePoint.GeneratedChapter(save, episode) is { } chapter && S3ItemCatalog.ForEpisode(episode)?.ForChapter(chapter.Id) is { } items
            ? [.. items.Carried.Select(id => new HeldItem(id))]
            : [];
    }

    private static int EpisodeOf(SaveSlot save) => save.Metadata?.GetInt(SaveMetadataKeys.Episode) ?? S3SlotFiles.FirstEpisode;

    private static int Count(SaveSlot save, string itemId) =>
        Math.Max(Sets.Select(name => S3ResumePoint.Properties(save, name)?.GetInt(itemId)).FirstOrDefault(count => count != null) ?? 0, 0);
}
