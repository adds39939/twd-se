namespace TwdSaveEditor.Core.Model;

public sealed record EventLogData(ulong Type, List<EventLogValue> Values);
