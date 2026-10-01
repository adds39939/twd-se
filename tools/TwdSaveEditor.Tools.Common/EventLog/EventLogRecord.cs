namespace TwdSaveEditor.Tools.Common.EventLog;

public readonly record struct EventLogRecord(ulong EventType, ulong NodeHash);
