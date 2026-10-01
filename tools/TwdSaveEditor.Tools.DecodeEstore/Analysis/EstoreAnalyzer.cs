using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.EventLog;
using TwdSaveEditor.Tools.Common.MetaStreams;
using TwdSaveEditor.Tools.DecodeEstore.Model;

namespace TwdSaveEditor.Tools.DecodeEstore.Analysis;

public static class EstoreAnalyzer
{
    private const int IndexStart = 20;

    public static EstoreAnalysis? Analyze(string path)
    {
        if (!File.Exists(path))
            return null;

        var data = File.ReadAllBytes(path);
        var defaultSection = MetaStreamParser.Parse(data)?.Default ?? [];
        if (defaultSection.Length < IndexStart)
            return null;

        var headerHash = Bytes.U64(defaultSection, 4);
        var entryCount = Bytes.U32(defaultSection, 16);

        var pages = new List<PageIndexEntry>();
        long position = IndexStart;
        for (uint i = 0; i < entryCount; i++)
        {
            if (position + 16 > defaultSection.Length)
                break;

            var entrySize = Bytes.U32(defaultSection, position);
            pages.Add(new PageIndexEntry(Bytes.U64(defaultSection, position + 4), Bytes.U32(defaultSection, position + 12)));
            position += 4 + entrySize;
        }

        var nameMarker = Bytes.IndexOf(defaultSection, ".estore"u8, position);
        var recordsStart = nameMarker >= 0 ? nameMarker + 7 : position;
        var recordCount = Bytes.FindAll(defaultSection, EventLogFormat.RecordStart, recordsStart).Count;

        return new EstoreAnalysis(data.Length, defaultSection.Length, headerHash, pages, recordCount);
    }
}
