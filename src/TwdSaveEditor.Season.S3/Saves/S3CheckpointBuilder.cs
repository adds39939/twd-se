using System.Text.Json;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.SaveGames;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.DialogLog;
using TwdSaveEditor.Season.S3.Chapters;
using TwdSaveEditor.Season.S3.Decisions;

namespace TwdSaveEditor.Season.S3.Saves;

public static class S3CheckpointBuilder
{
    public const string DeveloperMenuScript = "DebugMenu";
    public const string PreviousScript = "Script - Previous";

    private const string SaveMetadataParent = "metadata_save_s3.prop";
    private const string AutoSave = "SaveLoad - Auto Save";
    private const string ScriptExtension = ".lua";
    private const uint LocalKeysFlag = 0x100;

    private static readonly string[] SharedResourceSets = ["UISeason3", "MenuSeason3", "ProjectSeason3"];

    public static SaveSlot Build(SaveSlot slot, int episode, S3Chapter chapter, int serial, string date)
    {
        var project = S3SlotFiles.ProjectName(episode);

        var metadata = new PropertySet { Flags = LocalKeysFlag, ParentSymbols = [Symbol.FromString(SaveMetadataParent)] };
        metadata.SetInt(SaveMetadataKeys.Serial, serial);
        metadata.SetInt(SaveMetadataKeys.Episode, episode);
        metadata.SetString(SaveMetadataKeys.Date, date);
        metadata.SetString(SaveMetadataKeys.ChapterId, string.Empty);
        metadata.SetString(S3SlotFiles.SavedScript, chapter.Script);
        metadata.SetString(S3SlotFiles.SavedProject, project);

        var sets = new SortedDictionary<ulong, PropertySet>();
        Runtime(sets, S3SlotFiles.SaveLoadProperties).SetBool(AutoSave, false);
        Runtime(sets, S3SlotFiles.ScriptProperties).SetString(PreviousScript, DeveloperMenuScript);

        var game = Runtime(sets, S3SlotFiles.LogicGameProperties);
        ApplyLogicKeys(slot, episode, game);
        foreach (var flag in chapter.Flags)
            Apply(game, flag.Key, flag.Value);

        var save = new SaveGameFile
        {
            LuaDoFile = chapter.Script + ScriptExtension,
            Agents = [],
            RuntimePropertyNames = [.. sets.Keys],
            EnabledDynamicSets = [.. SharedResourceSets.Prepend(project).Select(TelltaleHash.ComputeCrc64)],
        };

        var bundle = SaveSlotFactory.Create(S3SlotFiles.AutosaveName(slot.FileName));
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

    public static void ApplyLogicKeys(SaveSlot slot, int episode, PropertySet game)
    {
        var nodes = new S3EventLog(slot).Nodes();
        foreach (var key in S3DecisionCatalog.LogicKeys.Where(key => key.ReadFrom <= episode))
            Set(game, key, nodes);
    }

    public static void Set(PropertySet game, S3LogicKey key, IReadOnlySet<ulong> nodes)
    {
        switch (S3DecisionLog.Evaluate(key, nodes))
        {
            case bool flag:
                game.SetBool(key.Key, flag);
                break;
            case string text:
                game.SetString(key.Key, text);
                break;
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
            sets[name] = properties = DialogLogFiles.NewRuntimeProperties();

        return properties;
    }
}
