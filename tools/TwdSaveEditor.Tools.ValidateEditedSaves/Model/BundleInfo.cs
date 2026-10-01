using TwdSaveEditor.Tools.Common.Bundles;

namespace TwdSaveEditor.Tools.ValidateEditedSaves.Model;

public sealed record BundleInfo(
    int Size,
    uint DebugSize,
    string DebugHex,
    bool AsyncCompressed,
    uint FileCount,
    List<BundleEntry> Entries,
    Dictionary<string, InnerFile> Files);
