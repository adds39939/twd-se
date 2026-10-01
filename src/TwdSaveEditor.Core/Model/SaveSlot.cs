namespace TwdSaveEditor.Core.Model;

public sealed class SaveSlot
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }

    public required MetaStreamHeader OuterHeader { get; init; }

    public required List<BundleFileEntry> FileTable { get; init; }

    public PropertySet? Metadata { get; set; }

    public PropertySet? Choices { get; set; }

    public PropertySet? ChoiceStats { get; set; }

    public string ChoicesFileName { get; init; } = "choices.prop";

    public byte[]? RawChoiceStatsFile { get; init; }

    public byte[]? RawBundleData { get; init; }

    public byte[]? RawMetadataFile { get; init; }

    public byte[]? RawChoicesFile { get; init; }

    public Dictionary<string, byte[]>? RawInnerFiles { get; init; }

    public string? EStorePath { get; set; }

    public List<string>? EPagePaths { get; set; }

    public List<EventLogEntry>? PendingEventLogEntries { get; set; }

    public List<EventLogEntry>? LoadedEventLogEntries { get; set; }

    public Dictionary<string, byte[]>? LoadedEventLogFiles { get; set; }

    public string? DetectedSeasonKey { get; set; }

    public bool EpisodeChanged { get; set; }
}
