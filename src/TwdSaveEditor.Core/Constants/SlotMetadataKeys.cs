namespace TwdSaveEditor.Core.Constants;

public static class SlotMetadataKeys
{
    public const string LatestSerial = "Latest Serial";
    public const string LatestSave = "Latest Save";
    public const string EpisodeInProgress = "Episode in Progress";
    public const string SlotName = "Slot Name";
    public const string Progress = "Progress";

    public static string CompletedEpisode(int episode) => $"Completed Episode {episode}";
}
