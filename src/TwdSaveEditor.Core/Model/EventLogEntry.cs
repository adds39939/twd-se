using TwdSaveEditor.Core.Constants;

namespace TwdSaveEditor.Core.Model;

public sealed class EventLogEntry
{
    public const int RecordSize = 42;

    public ulong EventTypeHash { get; set; }

    public ulong NodeHash { get; set; }

    public uint ValueType { get; set; }

    public byte ExtraFlag { get; set; }

    public uint SequenceIndex { get; set; }

    public ushort Trailing { get; set; }

    public byte[] RawData { get; set; } = [];

    public bool IsDialogNode => EventTypeHash == EventLogEventTypes.ExecutingDialogNode;
    public bool IsDialogChoice => EventTypeHash == EventLogEventTypes.DialogChoice;
}
