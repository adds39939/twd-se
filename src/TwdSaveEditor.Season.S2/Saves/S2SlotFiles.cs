using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.DialogLog;

namespace TwdSaveEditor.Season.S2.Saves;

public static class S2SlotFiles
{
    public const string StorageSuffix = DialogLogFiles.StorageSuffix;
    public const string PageExtension = DialogLogFiles.PageExtension;
    public const string BundleExtension = DialogLogFiles.BundleExtension;
    public const string AutosaveName = DialogLogFiles.AutosaveName;
    public const string CheckpointName = "checkpoint";

    public static readonly ulong LogicGameProperties = DialogLogFiles.RuntimeProperties("logic_game");
    public static readonly ulong SaveLoadProperties = DialogLogFiles.RuntimeProperties("logic_saveload");
    public static readonly ulong ScriptProperties = DialogLogFiles.RuntimeProperties("logic_script");
    public static readonly ulong InventoryProperties = DialogLogFiles.RuntimeProperties("logic_inventory");

    private const string EpisodePrefix = "WalkingDead20";
    private const string CreditsPrefix = "ShowEndCredits";
    private const string NextEpisodePrefix = "EpisodeCompleteShowEpisode";
    private const string FinishedProgress = "finished";

    public static bool IsSlotBundle(string fileName) => DialogLogFiles.IsSlotBundle(fileName);

    public static string Prefix(string slotFileName) => DialogLogFiles.Prefix(slotFileName);

    public static string StorageName(string slotFileName) => DialogLogFiles.StorageName(slotFileName);

    public static string SaveName(string slotFileName, string save) => DialogLogFiles.SaveName(slotFileName, save);

    public static bool IsPage(string slotFileName, string fileName) => DialogLogFiles.IsPage(slotFileName, fileName);

    public static bool IsSave(string slotFileName, string fileName) => DialogLogFiles.IsSave(slotFileName, fileName);

    public static string EpisodeId(int episode) => EpisodePrefix + episode;

    public static string TrackerContainer(int persistentEpisode) => $"ChoiceTracker - {persistentEpisode}";

    public static PropertySet NewRuntimeProperties() => DialogLogFiles.NewRuntimeProperties();

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
