using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Database;

public sealed class PropertyNameDb
{
    private readonly Dictionary<ulong, string> _names = new();

    public string? Resolve(Symbol symbol) =>
        _names.TryGetValue(symbol.Value, out var name) ? name : null;

    public string ResolveOrHex(Symbol symbol) =>
        Resolve(symbol) ?? $"0x{symbol.Value:X16}";

    public void Register(string name)
    {
        var hash = TelltaleHash.ComputeCrc64(name);
        _names[hash] = name;
    }

    public static PropertyNameDb CreateDefault(IEnumerable<string>? additionalNames = null)
    {
        var db = new PropertyNameDb();

        string[] saveProps = [
            SlotMetadataKeys.LatestSerial, SlotMetadataKeys.LatestSave, SlotMetadataKeys.EpisodeInProgress,
            SlotMetadataKeys.SlotName, SlotMetadataKeys.Progress,
            SaveMetadataKeys.Serial, SaveMetadataKeys.Date, SaveMetadataKeys.Episode, SaveMetadataKeys.ChapterId,
            SaveMetadataKeys.CheckpointDialog, SaveMetadataKeys.CheckpointDialogNode,
        ];

        foreach (var name in saveProps)
        {
            db.Register(name);
        }

        foreach (var name in additionalNames ?? [])
        {
            db.Register(name);
        }

        return db;
    }
}
