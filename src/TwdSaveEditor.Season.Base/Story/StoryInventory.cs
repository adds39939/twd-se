using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.DialogLog;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.Base.Story;

public sealed class StoryInventory(StorySeason season, StoryResumePoint resume, string owner, IReadOnlyList<ulong> sets, string noItems)
{
    private const string NoSave = "This slot starts at an episode's beginning or is finished, so there's no inventory to edit yet. Set a chapter under Resume Point first.";
    private const string Damaged = "This slot's save can't be read, so the inventory can't be edited.";
    private const string LogDamaged = "The dialog log can't be read, so the inventory can't be edited.";
    private const string Unreadable = "Can't set the inventory: its properties in the save can't be read.";

    public InventoryState GetState(SaveSlot slot)
    {
        var state = resume.GetState(slot);
        if (slot.EventLogDamaged)
        {
            return InventoryState.NotEditable(owner, state.Episode, LogDamaged);
        }

        if (resume.ResumeSave(slot) is not { } save)
        {
            return InventoryState.NotEditable(owner, state.Episode, state.CheckpointDamaged ? Damaged : NoSave);
        }

        var episode = EpisodeOf(save);
        if (season.ItemsOf(episode) is not { Items.Count: > 0 } catalog)
        {
            return InventoryState.NotEditable(owner, episode, noItems);
        }

        if (sets.Any(name => save.FindFile(name) is { } file && !BundleReader.TryParseProperties(file)))
        {
            return InventoryState.NotEditable(owner, episode, Damaged);
        }

        return new InventoryState(
            owner,
            episode,
            null,
            [.. catalog.Items.Select(item => new InventoryItem(item.Id, item.Name))],
            [.. catalog.Items.Where(item => Count(save, item.Id) > 0).Select(item => new HeldItem(item.Id))],
            resume.GeneratedChapter(save, episode) != null);
    }

    public void SetItems(SaveSlot slot, IReadOnlyList<HeldItem> held)
    {
        var save = resume.ResumeSave(slot)
            ?? throw new InvalidOperationException("Cannot set the inventory: the slot has no save of the episode in progress.");

        foreach (var item in season.ItemsOf(EpisodeOf(save))?.Items ?? [])
        {
            var wanted = held.Any(entry => entry.Id == item.Id && entry.Count > 0) ? 1 : 0;
            if (wanted == Math.Min(Count(save, item.Id), 1))
            {
                continue;
            }

            foreach (var name in sets)
            {
                DialogLogSaves.RuntimeProperties(save, name, Unreadable).SetInt(item.Id, wanted);
            }

            save.Modified = true;
        }
    }

    public IReadOnlyList<HeldItem> CarriedItems(SaveSlot slot)
    {
        if (resume.ResumeSave(slot) is not { } save)
        {
            return [];
        }

        var episode = EpisodeOf(save);
        return resume.GeneratedChapter(save, episode) is { } chapter && season.ItemsOf(episode)?.ForChapter(chapter.Id) is { } items
            ? [.. items.Carried.Select(id => new HeldItem(id))]
            : [];
    }

    private static int EpisodeOf(SaveSlot save) => save.Metadata?.GetInt(SaveMetadataKeys.Episode) ?? StorySeason.FirstEpisode;

    private int Count(SaveSlot save, string itemId) =>
        Math.Max(sets.Select(name => DialogLogSaves.FindRuntimeProperties(save, name)?.GetInt(itemId)).FirstOrDefault(count => count != null) ?? 0, 0);
}
