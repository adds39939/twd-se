namespace TwdSaveEditor.Core.Model;

public sealed class MetaStreamHeader
{
    public const uint MagicMsv5 = 0x4D535635;
    public const uint MagicMsv6 = 0x4D535636;
    public const uint CompressedFlag = 0x80000000;

    public uint Magic { get; set; }

    public uint DefaultSectionSize { get; set; }

    public uint DebugSectionSize { get; set; }

    public uint AsyncSectionSize { get; set; }

    public bool IsDefaultCompressed => (DefaultSectionSize & CompressedFlag) != 0;
    public bool IsDebugCompressed => (DebugSectionSize & CompressedFlag) != 0;
    public bool IsAsyncCompressed => (AsyncSectionSize & CompressedFlag) != 0;

    public uint DefaultDataSize => DefaultSectionSize & ~CompressedFlag;
    public uint DebugDataSize => DebugSectionSize & ~CompressedFlag;
    public uint AsyncDataSize => AsyncSectionSize & ~CompressedFlag;

    public List<VersionEntry> VersionEntries { get; set; } = [];

    public bool IsMsv6 => Magic == MagicMsv6;
}
