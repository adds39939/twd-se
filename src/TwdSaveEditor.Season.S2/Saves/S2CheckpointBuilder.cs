using System.Text.Json;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.SaveGames;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.S2.Chapters;
using TwdSaveEditor.Season.S2.Decisions;

namespace TwdSaveEditor.Season.S2.Saves;

public static class S2CheckpointBuilder
{
    public const string DeveloperMenuScript = "DebugMenu";
    public const string PreviousScript = "Script - Previous";
    public const string SavedScript = "Saved Game Script";
    public const string ChapterId = "SaveLoad - Chapter ID";

    private const string SaveMetadataParent = "metadata_save_s2.prop";
    private const string AutoSave = "SaveLoad - Auto Save";
    private const string ScriptExtension = ".lua";
    private const uint LocalKeysFlag = 0x100;

    private static readonly string[] SharedResourceSets = ["MenuSeason2", "ProjectSeason2"];

    public static SaveSlot Build(SaveSlot slot, S2EpisodeChapters episode, S2Chapter chapter, string fileName, int serial, string date)
    {
        var episodeId = S2SlotFiles.EpisodeId(episode.Episode);

        var metadata = new PropertySet { Flags = LocalKeysFlag, ParentSymbols = [Symbol.FromString(SaveMetadataParent)] };
        metadata.SetInt(SaveMetadataKeys.Serial, serial);
        metadata.SetString(SaveMetadataKeys.Date, date);
        metadata.SetString(SaveMetadataKeys.Episode, episodeId);
        metadata.SetString(SaveMetadataKeys.ChapterId, chapter.ChapterId);
        metadata.SetString(SavedScript, chapter.Script);

        var sets = new SortedDictionary<ulong, PropertySet>();

        var saveLoad = Runtime(sets, S2SlotFiles.SaveLoadProperties);
        saveLoad.SetBool(AutoSave, false);
        saveLoad.SetString(ChapterId, chapter.ChapterId);

        if (!chapter.StartsEpisode)
        {
            Runtime(sets, S2SlotFiles.ScriptProperties).SetString(PreviousScript, DeveloperMenuScript);

            var game = Runtime(sets, S2SlotFiles.LogicGameProperties);
            ApplyImportedChoices(slot, game);
            ApplyEarlierDecisions(slot, episode.Episode, game);
            foreach (var flag in chapter.Flags)
                Apply(game, flag.Key, flag.Value);
        }

        var save = new SaveGameFile
        {
            LuaDoFile = chapter.Script + ScriptExtension,
            Agents = [],
            RuntimePropertyNames = [.. sets.Keys],
            EnabledDynamicSets = [.. SharedResourceSets.Prepend(episodeId).Select(TelltaleHash.ComputeCrc64)],
        };

        var bundle = SaveSlotFactory.Create(fileName);
        bundle.DetectedSeasonKey = slot.DetectedSeasonKey;
        bundle.Modified = true;
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

    private static void ApplyImportedChoices(SaveSlot slot, PropertySet game)
    {
        foreach (var imported in S2ChapterCatalog.ImportedKeys)
        {
            foreach (var key in imported.Keys)
            {
                if (slot.Choices?.GetString(key) is not { } value)
                    continue;

                var name = $"Episode {imported.Episode} - {key}";
                if (bool.TryParse(value, out var flag))
                    game.SetBool(name, flag);
                else
                    game.SetString(name, value);
            }
        }
    }

    private static void ApplyEarlierDecisions(SaveSlot slot, int episode, PropertySet game)
    {
        var log = new S2EventLogEditor(slot);
        foreach (var decision in S2DecisionCatalog.All.Where(decision => decision.Episode < episode && decision.LogicKey != null))
        {
            var option = log.FindSeen(decision);
            if (decision.LogicIsText)
                game.SetString(decision.LogicKey!, option?.LogicValue ?? string.Empty);
            else
                game.SetBool(decision.LogicKey!, option?.LogicValue != null);
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

    private static PropertySet Runtime(SortedDictionary<ulong, PropertySet> sets, ulong name)
    {
        if (!sets.TryGetValue(name, out var properties))
            sets[name] = properties = S2SlotFiles.NewRuntimeProperties();

        return properties;
    }
}
