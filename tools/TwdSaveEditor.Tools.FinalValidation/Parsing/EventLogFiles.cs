using TwdSaveEditor.Tools.Common.EventLog;
using TwdSaveEditor.Tools.Common.MetaStreams;

namespace TwdSaveEditor.Tools.FinalValidation.Parsing;

public static class EventLogFiles
{
    public static (int Total, List<EventLogRecord> DialogNodes) Read(IEnumerable<string> paths, List<string> details, bool reportMissing)
    {
        var total = 0;
        var dialogNodes = new List<EventLogRecord>();

        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                if (reportMissing)
                    details.Add($"File not found: {Path.GetFileName(path)}");
                continue;
            }

            if (MetaStreamParser.Parse(File.ReadAllBytes(path)) is not { } parsed)
                continue;

            var records = EventLogFormat.ReadRecords(parsed.Default);
            details.Add($"\n{Path.GetFileName(path)}: {records.Count} records");
            total += records.Count;
            dialogNodes.AddRange(records.Where(record => record.EventType == EventLogFormat.ExecutingDialogNode));
        }

        details.Add($"\nTotal records: {total}");
        details.Add($"Dialog node records: {dialogNodes.Count}");
        return (total, dialogNodes);
    }
}
