using System.Text.Json;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.SaveGames;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.S1.Accessors;
using TwdSaveEditor.Season.S1.Chapters;
using TwdSaveEditor.Season.S1.Persistence;

namespace TwdSaveEditor.Season.S1.Saves;

public sealed class S1CheckpointBuilder : IS1CheckpointBuilder
{
    public const string CheckpointDialogItem = "Checkpoint Dialog Item";

    private const string SaveMetadataParent = "metadata_save_s1.prop";
    private const string DialogFileProperty = "Dialog Agent - File Primary";
    private const uint LocalKeysFlag = 0x100;
    private const int EpisodeBase = 100;

    private static readonly string[] SharedResourceSets = ["MenuSeason1", "ProjectSeason1"];

    public SaveSlot Build(SaveSlot slot, S1EpisodeChapters episode, S1Chapter chapter, int serial, string date)
    {
        var entry = chapter.Entry ?? throw new InvalidOperationException($"Chapter {chapter.Id} starts its episode and needs no checkpoint.");
        var episodeId = S1SlotFiles.EpisodeId(episode.Episode);

        var metadata = new PropertySet { Flags = LocalKeysFlag, ParentSymbols = [Symbol.FromString(SaveMetadataParent)] };
        metadata.SetInt(SaveMetadataKeys.Serial, serial);
        metadata.SetString(SaveMetadataKeys.Date, date);
        metadata.SetString(SaveMetadataKeys.Episode, episodeId);
        metadata.SetString(SaveMetadataKeys.ChapterId, chapter.Id);
        metadata.SetString(SaveMetadataKeys.CheckpointDialog, entry.Dialog?.ToLowerInvariant() ?? string.Empty);
        metadata.SetString(SaveMetadataKeys.CheckpointDialogNode, entry.Node ?? string.Empty);

        var sets = new SortedDictionary<ulong, PropertySet>();
        var game = Logic(sets, S1RuntimeProperties.GameLogicAgent);
        game.SetBool("Timers Enabled", true);
        game.SetBool("Triggers Enabled", true);
        game.SetBool("bUsingJoystick", false);
        game.SetBool("Holding action", false);
        game.SetString("Current Mode", "mode_Main");
        ApplyDecisions(slot, episode, chapter, sets);

        foreach (var flag in chapter.Flags)
            Apply(Properties(sets, flag.Agent, flag.Scene ?? S1RuntimeProperties.LogicScene), flag.Key, flag.Value);

        ApplyDecisionFlags(slot, episode, chapter, sets);

        Logic(sets, S1RuntimeProperties.CheckpointAgent, visible: true).SetString(CheckpointDialogItem, entry.Node ?? string.Empty);

        var saveLoad = Logic(sets, S1RuntimeProperties.SaveLoadAgent);
        saveLoad.SetBool("SaveLoad - Auto Save", false);
        saveLoad.SetString("SaveLoad - Chapter ID", chapter.Id);

        if (entry.Scene != null && entry.Dialog != null)
        {
            var scene = Runtime(visible: true);
            scene.Set(Symbol.FromString(DialogFileProperty), new Symbol(TelltaleTypes.DialogHandle), new SymbolValue(Symbol.FromString(entry.Dialog)));
            sets[S1RuntimeProperties.Name(entry.Scene, entry.Scene)] = scene;
        }

        var save = new SaveGameFile
        {
            LuaDoFile = entry.Script,
            Agents = [],
            RuntimePropertyNames = [.. sets.Keys],
            EnabledDynamicSets = [.. SharedResourceSets.Append(episodeId).Select(TelltaleHash.ComputeCrc64)],
        };

        var bundle = SaveSlotFactory.Create(S1SlotFiles.AutosaveName(slot.FileName));
        bundle.DetectedSeasonKey = slot.DetectedSeasonKey;
        bundle.Files.Add(SaveSlotFactory.CreateFile(BundleFileNames.SaveMetadata, metadata));
        bundle.Files.Add(BundleFileEntry.Create(BundleFileNames.SaveGame, TelltaleTypes.SaveGame, SaveGameCodec.Write(save)));
        bundle.Files.AddRange(sets.Select(entry => new BundleFileEntry
        {
            NameField = new byte[BundleFileEntry.NameFieldSize],
            NameSymbol = entry.Key,
            TypeSymbol = TelltaleTypes.PropertySet,
            Data = [],
            Properties = entry.Value,
        }));

        return bundle;
    }

    private static void ApplyDecisions(SaveSlot slot, S1EpisodeChapters episode, S1Chapter chapter, SortedDictionary<ulong, PropertySet> sets)
    {
        var accessor = new S1ChoiceAccessor(slot);
        var game = Logic(sets, S1RuntimeProperties.GameLogicAgent);
        var current = EpisodeBase + episode.Episode;
        foreach (var choice in S1ChoiceCatalog.All.Where(choice => S1ChoiceCatalog.PersistentEpisode(choice) <= current))
        {
            if (S1ChoiceCatalog.PersistentEpisode(choice) == current && !episode.IsDecided(choice.ChoiceKey, chapter))
                continue;

            if (accessor.GetChoiceValue(choice.ChoiceKey) is { } value)
                game.SetString(choice.ChoiceKey, value);
        }
    }

    private static void ApplyDecisionFlags(SaveSlot slot, S1EpisodeChapters episode, S1Chapter chapter, SortedDictionary<ulong, PropertySet> sets)
    {
        var accessor = new S1ChoiceAccessor(slot);
        foreach (var flag in episode.DecisionFlags.Where(flag => episode.IsDecided(flag, chapter)))
        {
            if (accessor.GetChoiceValue(flag.ChoiceKey) is { } choice && flag.Values.TryGetValue(choice, out var value))
                Apply(Logic(sets, flag.Agent), flag.Key, value);
        }
    }

    private static void Apply(PropertySet properties, string key, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.True or JsonValueKind.False:
                properties.SetBool(key, value.GetBoolean());
                break;
            case JsonValueKind.Number:
                properties.SetInt(key, value.GetInt32());
                break;
            case JsonValueKind.String:
                properties.SetString(key, value.GetString()!);
                break;
        }
    }

    private static PropertySet Logic(SortedDictionary<ulong, PropertySet> sets, string agent, bool visible = false) =>
        Properties(sets, agent, S1RuntimeProperties.LogicScene, visible);

    private static PropertySet Properties(SortedDictionary<ulong, PropertySet> sets, string agent, string scene, bool visible = false)
    {
        var name = S1RuntimeProperties.Name(agent, scene);
        if (!sets.TryGetValue(name, out var properties))
            sets[name] = properties = Runtime(visible);

        return properties;
    }

    private static PropertySet Runtime(bool visible) => S1RuntimeProperties.Create(visible);
}
