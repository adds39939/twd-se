namespace TwdSaveEditor.Tools.DecodeEstore.Model;

public sealed record EstoreAnalysis(
    int FileSize,
    int DefaultSize,
    ulong HeaderHash,
    List<PageIndexEntry> Pages,
    int RecordCount);
