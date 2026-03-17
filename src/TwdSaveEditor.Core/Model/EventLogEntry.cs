namespace TwdSaveEditor.Core.Model;

/// <summary>
/// Represents a single 42-byte EventLog record from an estore/epage file.
/// Used by S3/Michonne saves to store choice data as "Executing Dialog Node" events.
/// </summary>
public sealed class EventLogEntry
{
    /// <summary>Record size: always 42 bytes.</summary>
    public const int RecordSize = 42;

    /// <summary>CRC64 hash of the event type (e.g., "Executing Dialog Node").</summary>
    public ulong EventTypeHash { get; set; }

    /// <summary>CRC64 hash of the dialog node or tag associated with this event.</summary>
    public ulong NodeHash { get; set; }

    /// <summary>Value type field (1 or 2).</summary>
    public uint ValueType { get; set; }

    /// <summary>Extra data flag byte.</summary>
    public byte ExtraFlag { get; set; }

    /// <summary>Sequential event index (3 bytes).</summary>
    public uint SequenceIndex { get; set; }

    /// <summary>Trailing 2 bytes.</summary>
    public ushort Trailing { get; set; }

    /// <summary>Raw 42-byte record data for round-tripping.</summary>
    public byte[] RawData { get; set; } = [];

    // Well-known event type hashes
    public static class EventTypes
    {
        public const ulong ExecutingDialogNode = 0x625874A31EA13BB1;
        public const ulong DialogChoice = 0x25D62FD9BE53CF73;
        public const ulong BeginEpisode = 0x22B4F702006E4E3A;
        public const ulong EndEpisode = 0xB1BB1124EA852E99;
        public const ulong SaveSerial = 0x48FA4CC44ADE92F3;
    }

    public bool IsDialogNode => EventTypeHash == EventTypes.ExecutingDialogNode;
    public bool IsDialogChoice => EventTypeHash == EventTypes.DialogChoice;
}
