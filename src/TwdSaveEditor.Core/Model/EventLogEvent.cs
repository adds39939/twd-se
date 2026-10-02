using TwdSaveEditor.Core.Constants;

namespace TwdSaveEditor.Core.Model;

public sealed class EventLogEvent
{
    public const int DefaultSeverity = 10;

    public uint Id { get; set; }

    public int MaxSeverity { get; set; } = DefaultSeverity;

    public uint ChildCount { get; set; }

    public List<EventLogData> Data { get; set; } = [];

    public ulong? DialogNode => Find(EventLogEventTypes.ExecutingDialogNode) is { IsSymbol: true } value ? value.Raw : null;

    public long? SaveSerial => Find(EventLogEventTypes.SaveSerial)?.Number is { } serial ? (long)serial : null;

    public static EventLogEvent ForDialogNode(uint id, ulong node) => new()
    {
        Id = id,
        Data = [new EventLogData(EventLogEventTypes.ExecutingDialogNode, [EventLogValue.Symbol(node, DefaultSeverity)])],
    };

    public static EventLogEvent ForSaveSerial(uint id, long serial) => new()
    {
        Id = id,
        Data = [new EventLogData(EventLogEventTypes.SaveSerial, [EventLogValue.Double(serial, DefaultSeverity)])],
    };

    public static EventLogEvent ForNumber(uint id, ulong type, double value) => new()
    {
        Id = id,
        Data = [new EventLogData(type, [EventLogValue.Double(value, DefaultSeverity)])],
    };

    public static EventLogEvent ForSymbol(uint id, ulong type, ulong symbol) => new()
    {
        Id = id,
        Data = [new EventLogData(type, [EventLogValue.Symbol(symbol, DefaultSeverity)])],
    };

    public bool Has(ulong type) => Data.Any(entry => entry.Type == type);

    public double? Number(ulong type) => Find(type)?.Number;

    public void SetDialogNode(ulong node)
    {
        var data = Data.First(entry => entry.Type == EventLogEventTypes.ExecutingDialogNode);
        data.Values[0] = data.Values[0] with { Raw = node };
    }

    private EventLogValue? Find(ulong type)
    {
        var data = Data.FirstOrDefault(entry => entry.Type == type);
        return data == null || data.Values.Count == 0 ? null : data.Values[0];
    }
}
