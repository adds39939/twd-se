using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Season.S2.Saves;

public static class S2SlotFiles
{
    public const string StorageSuffix = "_id.estore";
    public const string PageExtension = ".epage";
    public const string BundleExtension = ".bundle";
    public const string AutosaveName = "autosave";
    public const string CheckpointName = "checkpoint";

    public static readonly ulong LogicGameProperties = RuntimeProperties("logic_game");
    public static readonly ulong SaveLoadProperties = RuntimeProperties("logic_saveload");
    public static readonly ulong ScriptProperties = RuntimeProperties("logic_script");
    public static readonly ulong InventoryProperties = RuntimeProperties("logic_inventory");

    private const string EpisodePrefix = "WalkingDead20";
    private const string CreditsPrefix = "ShowEndCredits";
    private const string NextEpisodePrefix = "EpisodeCompleteShowEpisode";
    private const string FinishedProgress = "finished";
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

    public static string EpisodeId(int episode) => EpisodePrefix + episode;

    public static string TrackerContainer(int persistentEpisode) => $"ChoiceTracker - {persistentEpisode}";

    public static PropertySet NewRuntimeProperties()
    {
        var properties = new PropertySet { Flags = RuntimeFlag };
        properties.SetBool(RuntimeVisible, false);
        return properties;
    }

    private static ulong RuntimeProperties(string agent) => TelltaleHash.ComputeCrc64($"\"{agent}:logic.scene\" Runtime Properties");

    public static int? EpisodeNumber(string? episodeId) => Number(episodeId, EpisodePrefix);

    public static int? CompletedEpisodes(string? progress, int lastEpisode) =>
        FinishedProgress.Equals(progress, StringComparison.OrdinalIgnoreCase)
            ? lastEpisode
            : Number(progress, CreditsPrefix) ?? Number(progress, NextEpisodePrefix) - 1;

    private static int? Number(string? text, string prefix) =>
        text != null
        && text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        && int.TryParse(text[prefix.Length..], out var number)
            ? number
            : null;
}
