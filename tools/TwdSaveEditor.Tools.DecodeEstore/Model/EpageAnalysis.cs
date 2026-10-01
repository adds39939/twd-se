using TwdSaveEditor.Tools.Common.Collections;

namespace TwdSaveEditor.Tools.DecodeEstore.Model;

public sealed record EpageAnalysis(
    string Name,
    byte[] Default,
    int RecordSize,
    int HashOffset,
    List<EventRecord> Records,
    Counter<string> TypeCounts,
    SortedSet<ulong> TypeHashes);
