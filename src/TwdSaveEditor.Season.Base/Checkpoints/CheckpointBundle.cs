using System.Text.Json;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.SaveGames;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.DialogLog;

namespace TwdSaveEditor.Season.Base.Checkpoints;

public static class CheckpointBundle
{
    public static SaveSlot Create(SaveSlot slot, string fileName, PropertySet metadata, string script, IEnumerable<string> resourceSets, SortedDictionary<ulong, PropertySet> sets)
    {
        var save = new SaveGameFile
        {
            LuaDoFile = script,
            Agents = [],
            RuntimePropertyNames = [.. sets.Keys],
            EnabledDynamicSets = [.. resourceSets.Select(TelltaleHash.ComputeCrc64)],
        };

        var bundle = SaveSlotFactory.Create(fileName);
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

    public static PropertySet Runtime(SortedDictionary<ulong, PropertySet> sets, ulong name)
    {
        if (!sets.TryGetValue(name, out var properties))
        {
            sets[name] = properties = DialogLogFiles.NewRuntimeProperties();
        }

        return properties;
    }

    public static void Apply(PropertySet properties, string key, JsonElement value)
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
}
