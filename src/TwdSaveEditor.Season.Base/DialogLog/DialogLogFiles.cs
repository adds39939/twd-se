using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using GameLog = TwdSaveEditor.Core.Model.EventLog;

namespace TwdSaveEditor.Season.Base.DialogLog;

public static class DialogLogFiles
{
    public const string StorageSuffix = "_id.estore";
    public const string PageExtension = ".epage";
    public const string BundleExtension = ".bundle";
    public const string AutosaveName = "autosave";

    private const string PageInfix = "_id_Page";
    private const string RuntimeVisible = "Runtime: Visible";
    private const uint RuntimeFlag = 0x10;

    public static bool IsSlotBundle(string fileName) => !Path.GetFileName(fileName).StartsWith('_');

    public static string Prefix(string slotFileName) => $"_{Path.GetFileNameWithoutExtension(slotFileName)}_";

    public static string StorageName(string slotFileName) => $"_{Path.GetFileNameWithoutExtension(slotFileName)}{StorageSuffix}";

    public static string SaveName(string slotFileName, string save) => $"{Prefix(slotFileName)}{save}{BundleExtension}";

    public static bool IsPage(string slotFileName, string fileName) =>
        fileName.StartsWith($"_{Path.GetFileNameWithoutExtension(slotFileName)}{PageInfix}", StringComparison.OrdinalIgnoreCase)
        && fileName.EndsWith(PageExtension, StringComparison.OrdinalIgnoreCase);

    public static bool IsSave(string slotFileName, string fileName) =>
        fileName.StartsWith(Prefix(slotFileName), StringComparison.OrdinalIgnoreCase)
        && fileName.EndsWith(BundleExtension, StringComparison.OrdinalIgnoreCase);

    public static ulong RuntimeProperties(string agent) => TelltaleHash.ComputeCrc64($"\"{agent}:logic.scene\" Runtime Properties");

    public static PropertySet NewRuntimeProperties()
    {
        var properties = new PropertySet { Flags = RuntimeFlag };
        properties.SetBool(RuntimeVisible, false);
        return properties;
    }

    public static GameLog EnsureLog(SaveSlot slot)
    {
        if (slot.EventLogDamaged)
        {
            throw new InvalidOperationException("The dialog log of this save cannot be read, so it cannot be changed.");
        }

        return slot.EventLog ??= NewLog(slot.FileName);
    }

    public static GameLog NewLog(string slotFileName)
    {
        var name = StorageName(slotFileName);
        var storage = new EventLogStorage
        {
            SessionId = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Name = name,
        };

        return new GameLog(name, storage) { StorageModified = true };
    }
}
