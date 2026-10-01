namespace TwdSaveEditor.Season.S1.Saves;

public static class S1SlotFiles
{
    public const ulong LogicGameProperties = 0x1D3802238E8CE045;

    private const string EpisodePrefix = "WalkingDead10";

    public static string AutosaveName(string slotFileName) =>
        $"_{Path.GetFileNameWithoutExtension(slotFileName)}_autosave.bundle";

    public static bool IsSlotBundle(string fileName) => !Path.GetFileName(fileName).StartsWith('_');

    public static string EpisodeId(int episode) => EpisodePrefix + episode;

    public static int? EpisodeNumber(string? episodeId) =>
        episodeId != null
        && episodeId.StartsWith(EpisodePrefix, StringComparison.OrdinalIgnoreCase)
        && int.TryParse(episodeId[EpisodePrefix.Length..], out var episode)
            ? episode
            : null;
}
