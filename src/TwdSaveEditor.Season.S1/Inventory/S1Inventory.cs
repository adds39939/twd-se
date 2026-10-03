using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.SaveGames;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S1.Accessors;
using TwdSaveEditor.Season.S1.Saves;

namespace TwdSaveEditor.Season.S1.Inventory;

public sealed class S1Inventory(IS1ResumePoint resume) : IS1Inventory
{
    private const string MainOwner = "Lee";
    private const string ExtraOwner = "The story's survivor";
    private const string NoSave = "The inventory is kept in the save the game resumes from. This slot starts an episode from its beginning, so there is nothing to edit yet. Set a chapter under Resume Point first.";
    private const string Damaged = "The checkpoint of this save cannot be read, so its inventory cannot be edited.";

    public InventoryState GetState(SaveSlot slot)
    {
        var state = resume.GetState(slot);
        if (ResumeSave(slot) is not { } save)
        {
            return InventoryState.NotEditable(Owner(state.Episode), state.Episode, slot.AutosaveDamaged ? Damaged : NoSave);
        }

        var episode = EpisodeOf(save) ?? state.Episode;
        var items = S1ItemCatalog.ForEpisode(episode)?.Items ?? [];
        if (items.Select(item => item.Agent).Distinct().Any(agent => save.FindFile(S1RuntimeProperties.LogicName(agent)) is { } file && !BundleReader.TryParseProperties(file)))
        {
            return InventoryState.NotEditable(Owner(episode), episode, Damaged);
        }

        return new InventoryState(
            Owner(episode),
            episode,
            null,
            [.. items.Select(item => new InventoryItem(item.Id, item.Name, item.MaxCount))],
            [.. items.Select(item => new HeldItem(item.Id, Count(save, item))).Where(item => item.Count > 0)],
            resume.GeneratedChapter(slot) != null);
    }

    public void SetItems(SaveSlot slot, IReadOnlyList<HeldItem> held, IReadOnlyCollection<string>? untouched = null)
    {
        var save = ResumeSave(slot)
            ?? throw new InvalidOperationException("Cannot set the inventory: the slot has no save of the episode in progress.");

        foreach (var item in S1ItemCatalog.ForEpisode(EpisodeOf(save) ?? 0)?.Items ?? [])
        {
            if (untouched?.Contains(item.Id) == true)
            {
                continue;
            }

            var wanted = Math.Clamp(held.FirstOrDefault(entry => entry.Id == item.Id)?.Count ?? 0, 0, item.MaxCount);
            if (wanted == Count(save, item))
            {
                continue;
            }

            var properties = Properties(save, item.Agent, create: true)!;
            if (item.Flag)
            {
                properties.SetBool(item.Id, wanted > 0);
            }
            else
            {
                properties.SetInt(item.Id, wanted);
            }
        }
    }

    public IReadOnlyList<HeldItem> CarriedItems(SaveSlot slot)
    {
        if (ResumeSave(slot) is not { } save
            || resume.GeneratedChapter(slot) is not { } chapter
            || S1ItemCatalog.ForEpisode(EpisodeOf(save) ?? 0) is not { } catalog
            || catalog.ForChapter(chapter.Id) is not { } items)
        {
            return [];
        }

        var choices = new S1ChoiceAccessor(slot);
        return
        [
            .. items.Carried.Select(catalog.Find).OfType<S1Item>()
                .Select(item => item.ChoiceKey == null ? item : choices.GetChoiceValue(item.ChoiceKey) is { } chosen ? catalog.Find(chosen) : null)
                .OfType<S1Item>()
                .Distinct()
                .Select(item => new HeldItem(item.Id, item.MaxCount)),
        ];
    }

    private static SaveSlot? ResumeSave(SaveSlot slot) =>
        slot.AutosaveDamaged || string.IsNullOrEmpty(slot.Metadata?.GetString(SlotMetadataKeys.LatestSave)) || slot.Autosave?.Metadata == null
            ? null
            : slot.Autosave;

    private static int? EpisodeOf(SaveSlot save) => S1SlotFiles.EpisodeNumber(save.Metadata?.GetString(SaveMetadataKeys.Episode));

    private static string Owner(int episode) => episode == S1ResumePoint.ExtraEpisode ? ExtraOwner : MainOwner;

    private static int Count(SaveSlot save, S1Item item)
    {
        var properties = Properties(save, item.Agent, create: false);
        return item.Flag
            ? properties?.GetBool(item.Id) == true ? 1 : 0
            : Math.Max(properties?.GetInt(item.Id) ?? 0, 0);
    }

    private static PropertySet? Properties(SaveSlot save, string agent, bool create)
    {
        var name = S1RuntimeProperties.LogicName(agent);
        if (save.FindFile(name) is { } existing)
        {
            return BundleReader.TryParseProperties(existing)
                ? existing.Properties
                : throw new InvalidOperationException($"Cannot read the inventory: the save's {agent} properties cannot be read.");
        }

        if (!create)
        {
            return null;
        }

        var properties = S1RuntimeProperties.Create(visible: false);
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
