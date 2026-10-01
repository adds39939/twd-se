using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Season.Base.EventLog;

internal sealed class TrackedEventLogEntry
{
    public required EventLogEntry Entry { get; init; }

    public required string SourcePath { get; init; }

    public required int RecordIndex { get; init; }
}
