using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.S1.Persistence;
using TwdSaveEditor.Season.S1.Saves;
using TwdSaveEditor.Core.Binary.SaveGames;

namespace TwdSaveEditor.Tools.BuildCheckpoint.Checkpoints;

public static class CheckpointBuilder
{
    private const string SaveMetadataParent = "metadata_save_s1.prop";
    private const uint LocalKeysFlag = 0x100;
    private const uint RuntimeFlag = 0x10;
    private const int EpisodeBase = 100;
    private const string CheckpointDialogItem = "Checkpoint Dialog Item";

    public static SaveSlot Strip(SaveSlot autosave, string fileName, bool keepAgents, IEnumerable<ulong> extraFiles)
    {
        var source = SaveGameCodec.Read(autosave.FindFile(BundleFileNames.SaveGame)!.Data);
        var kept = LogicProperties.All.Concat(extraFiles).Distinct().Where(symbol => autosave.FindFile(symbol) != null).ToList();

        var save = new SaveGameFile
        {
            LuaDoFile = source.LuaDoFile,
            Agents = keepAgents ? source.Agents : [],
            RuntimePropertyNames = kept,
            EnabledDynamicSets = source.EnabledDynamicSets,
            VersionEntries = source.VersionEntries,
        };

        var files = new List<BundleFileEntry>
        {
            Copy(autosave.FindFile(BundleFileNames.SaveMetadata)!),
            SaveGameEntry(autosave.FindFile(BundleFileNames.SaveGame)!.NameField, save),
        };
        files.AddRange(kept.Order().Select(symbol => Copy(autosave.FindFile(symbol)!)));

        return Bundle(fileName, files);
    }

    public static CheckpointDefinition Describe(SaveSlot autosave)
    {
        var metadata = autosave.Metadata!;
        var save = SaveGameCodec.Read(autosave.FindFile(BundleFileNames.SaveGame)!.Data);
        var episodeId = metadata.GetString(SaveMetadataKeys.Episode)!;

        return new CheckpointDefinition(
            EpisodeBase + (S1SlotFiles.EpisodeNumber(episodeId) ?? 1),
            episodeId,
            save.LuaDoFile,
            metadata.GetString(SaveMetadataKeys.ChapterId)!,
            Properties(autosave, LogicProperties.Checkpoint).GetString(CheckpointDialogItem)!,
            metadata.GetString(SaveMetadataKeys.CheckpointDialog)!,
            metadata.GetString(SaveMetadataKeys.CheckpointDialogNode)!,
            save.EnabledDynamicSets);
    }

    public static PropertySet EpisodeFlags(SaveSlot autosave) => Properties(autosave, LogicProperties.Game);

    public static SaveSlot Generate(SaveSlot slot, string fileName, CheckpointDefinition checkpoint, string date, PropertySet? episodeFlags)
    {
        var metadata = new PropertySet { Flags = LocalKeysFlag, ParentSymbols = [Symbol.FromString(SaveMetadataParent)] };
        metadata.SetInt(SaveMetadataKeys.Serial, slot.Metadata!.GetInt(SlotMetadataKeys.LatestSerial) ?? 1);
        metadata.SetString(SaveMetadataKeys.Date, date);
        metadata.SetString(SaveMetadataKeys.Episode, checkpoint.EpisodeId);
        metadata.SetString(SaveMetadataKeys.ChapterId, checkpoint.ChapterId);
        metadata.SetString(SaveMetadataKeys.CheckpointDialog, checkpoint.DialogFile);
        metadata.SetString(SaveMetadataKeys.CheckpointDialogNode, checkpoint.DialogNode);

        var game = Runtime(visible: false);
        game.SetBool("Timers Enabled", true);
        game.SetBool("Triggers Enabled", true);
        game.SetBool("bUsingJoystick", false);
        game.SetBool("Holding action", false);
        game.SetString("Current Mode", "mode_Main");

        foreach (var group in episodeFlags?.TypeGroups ?? [])
        {
            foreach (var flag in group.Properties)
                game.Set(flag.KeySymbol, group.TypeSymbol, flag.Value);
        }

        foreach (var choice in S1ChoiceCatalog.Before(checkpoint.PersistentEpisode))
        {
            var value = slot.Metadata.GetString(PersistentKeys.SlotKey(S1ChoiceCatalog.PersistentEpisode(choice), choice.ChoiceKey));
            if (!string.IsNullOrEmpty(value))
                game.SetString(choice.ChoiceKey, value);
        }

        var checkpointLogic = Runtime(visible: true);
        checkpointLogic.SetString(CheckpointDialogItem, checkpoint.DialogItem);

        var saveLoad = Runtime(visible: false);
        saveLoad.SetBool("SaveLoad - Auto Save", false);
        saveLoad.SetString("SaveLoad - Chapter ID", checkpoint.ChapterId);

        var logic = new SortedDictionary<ulong, PropertySet>
        {
            [LogicProperties.Game] = game,
            [LogicProperties.Checkpoint] = checkpointLogic,
            [LogicProperties.SaveLoad] = saveLoad,
        };

        var save = new SaveGameFile
        {
            LuaDoFile = checkpoint.Script,
            Agents = [],
            RuntimePropertyNames = [.. logic.Keys],
            EnabledDynamicSets = [.. checkpoint.ResourceSets],
        };

        var files = new List<BundleFileEntry>
        {
            SaveSlotFactory.CreateFile(BundleFileNames.SaveMetadata, metadata),
            SaveGameEntry(BundleFileEntry.Create(BundleFileNames.SaveGame, TelltaleTypes.SaveGame, []).NameField, save),
        };
        files.AddRange(logic.Select(entry => new BundleFileEntry
        {
            NameField = new byte[BundleFileEntry.NameFieldSize],
            NameSymbol = entry.Key,
            TypeSymbol = TelltaleTypes.PropertySet,
            Data = [],
            Properties = entry.Value,
        }));

        return Bundle(fileName, files);
    }

    private static PropertySet Properties(SaveSlot autosave, ulong symbol)
    {
        var file = autosave.FindFile(symbol) ?? throw new InvalidDataException($"The autosave has no file {symbol:X16}.");
        if (!BundleReader.TryParseProperties(file))
            throw new InvalidDataException($"File {symbol:X16} is not a readable property set.");

        return file.Properties!;
    }

    private static PropertySet Runtime(bool visible)
    {
        var properties = new PropertySet { Flags = RuntimeFlag };
        properties.SetInt("Transient Visibility Proxy", -1);
        properties.SetBool("Runtime: Visible", visible);
        return properties;
    }

    private static BundleFileEntry SaveGameEntry(byte[] nameField, SaveGameFile save) => new()
    {
        NameField = nameField,
        NameSymbol = TelltaleHash.ComputeCrc64(BundleFileNames.SaveGame),
        TypeSymbol = TelltaleTypes.SaveGame,
        Data = SaveGameCodec.Write(save),
    };

    private static BundleFileEntry Copy(BundleFileEntry file) => new()
    {
        NameField = file.NameField,
        NameSymbol = file.NameSymbol,
        TypeSymbol = file.TypeSymbol,
        Data = file.Data,
    };

    private static SaveSlot Bundle(string fileName, List<BundleFileEntry> files)
    {
        var bundle = SaveSlotFactory.Create(fileName);
        bundle.Files.AddRange(files);
        return bundle;
    }
}
