namespace TwdSaveEditor.Core.Model;

public sealed class EventLogPage
{
    public int Version { get; set; }

    public ulong SessionId { get; set; }

    public string FlushedName { get; set; } = string.Empty;

    public List<EventLogEvent> Events { get; set; } = [];

    public List<VersionEntry> VersionEntries { get; set; } = [];

    public uint MaxEventId => Events.Count == 0 ? 0 : Events.Max(entry => entry.Id);
}
