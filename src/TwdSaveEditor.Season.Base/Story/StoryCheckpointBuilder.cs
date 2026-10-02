using System.Text.Json;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.SaveGames;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.DialogLog;

namespace TwdSaveEditor.Season.Base.Story;

public sealed class StoryCheckpointBuilder(StorySeason season)
{
    public const string DeveloperMenuScript = "DebugMenu";
    public const string PreviousScript = "Script - Previous";
    public const string ChapterId = "SaveLoad - Chapter ID";

    private const string AutoSave = "SaveLoad - Auto Save";
    private const string ScriptExtension = ".lua";
    private const uint LocalKeysFlag = 0x100;

    public SaveSlot Build(SaveSlot slot, int episode, StoryChapter chapter, string fileName, int serial, string date)
    {
        var project = season.ProjectName(episode);

        var metadata = new PropertySet { Flags = LocalKeysFlag, ParentSymbols = [Symbol.FromString(season.SaveMetadataParent)] };
        metadata.SetInt(SaveMetadataKeys.Serial, serial);
        metadata.SetInt(SaveMetadataKeys.Episode, episode);
        metadata.SetString(SaveMetadataKeys.Date, date);
        metadata.SetString(SaveMetadataKeys.ChapterId, chapter.ChapterId);
        metadata.SetString(StoryFiles.SavedScript, chapter.Script);
        metadata.SetString(StoryFiles.SavedProject, project);

        var sets = new SortedDictionary<ulong, PropertySet>();
        var saveLoad = Runtime(sets, StoryFiles.SaveLoadProperties);
        saveLoad.SetBool(AutoSave, false);
        if (season.ChapterSaves)
            saveLoad.SetString(ChapterId, chapter.ChapterId);

        if (!chapter.StartsEpisode)
        {
            Runtime(sets, StoryFiles.ScriptProperties).SetString(PreviousScript, DeveloperMenuScript);

            var game = Runtime(sets, StoryFiles.LogicGameProperties);
            ApplyLogicKeys(slot, episode, game);
            foreach (var flag in chapter.Flags)
                Apply(game, flag.Key, flag.Value);
        }

        var save = new SaveGameFile
        {
            LuaDoFile = chapter.Script + ScriptExtension,
            Agents = [],
            RuntimePropertyNames = [.. sets.Keys],
            EnabledDynamicSets = [.. season.SharedResourceSets.Prepend(project).Select(TelltaleHash.ComputeCrc64)],
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

    public void ApplyLogicKeys(SaveSlot slot, int episode, PropertySet game)
    {
        var nodes = new StoryEventLog(slot, season).Nodes();
        foreach (var key in season.LogicKeys.Where(key => key.ReadFrom <= episode))
            Set(game, key, nodes);
    }

    public static void Set(PropertySet game, StoryLogicKey key, IReadOnlySet<ulong> nodes)
    {
        switch (StoryDecisionLog.Evaluate(key, nodes))
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
