using TwdSaveEditor.Tools.Common.Binary;

namespace TwdSaveEditor.Tools.Common.EventLog;

public static class EventLogFormat
{
    public const int RecordSize = 42;
    public const int EventTypeOffset = 16;
    public const int NodeHashOffset = 29;

    public const ulong ExecutingDialogNode = 0x625874A31EA13BB1;
    public const ulong DialogChoice = 0x25D62FD9BE53CF73;
    public const ulong BeginEpisode = 0x22B4F702006E4E3A;
    public const ulong EndEpisode = 0xB1BB1124EA852E99;
    public const ulong SaveSerial = 0x48FA4CC44ADE92F3;

    private const uint RecordVersion = 0x0A;
    private const uint RecordPayloadSize = 0x22;

    public static ReadOnlySpan<byte> RecordStart => [0x0A, 0x00, 0x00, 0x00, 0x22, 0x00, 0x00, 0x00];

    public static ReadOnlySpan<byte> RecordHeader =>
    [
        0x0A, 0x00, 0x00, 0x00, 0x22, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    ];

    public static List<EventLogRecord> ReadRecords(ReadOnlySpan<byte> section)
    {
        var records = new List<EventLogRecord>();
        var position = Bytes.IndexOf(section, RecordHeader);
        if (position < 0)
            return records;

        while (position + RecordSize <= section.Length)
        {
            if (Bytes.U32(section, position) != RecordVersion || Bytes.U32(section, position + 4) != RecordPayloadSize)
                break;

            records.Add(new EventLogRecord(
                Bytes.U64(section, position + EventTypeOffset),
                Bytes.U64(section, position + NodeHashOffset)));
            position += RecordSize;
        }

        return records;
    }
}
