namespace TwdSaveEditor.Core.Model;

public sealed class EventLogStorage
{
    public const int DefaultPageSize = 32768;

    public int Version { get; set; }

    public ulong SessionId { get; set; }

    public List<EventLogPageEntry> Pages { get; set; } = [];

    public string Name { get; set; } = string.Empty;

    public uint LastEventId { get; set; }

    public int PageSize { get; set; } = DefaultPageSize;

    public EventLogPage? CurrentPage { get; set; }

    public List<VersionEntry> VersionEntries { get; set; } = [];
}
