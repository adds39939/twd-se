namespace TwdSaveEditor.Core.Model;

public sealed class MetaStreamHeader
{
    public const uint MagicMsv5 = 0x4D535635;
    public const uint MagicMsv6 = 0x4D535636;

    public uint Magic { get; set; }
    public uint DefaultSectionSize { get; set; }
    public uint DebugSectionSize { get; set; }
    public uint AsyncSectionSize { get; set; }

    public bool IsDefaultCompressed => (DefaultSectionSize & 0x80000000) != 0;
    public bool IsDebugCompressed => (DebugSectionSize & 0x80000000) != 0;
    public bool IsAsyncCompressed => (AsyncSectionSize & 0x80000000) != 0;

    public uint DefaultDataSize => DefaultSectionSize & 0x7FFFFFFF;
    public uint DebugDataSize => DebugSectionSize & 0x7FFFFFFF;
    public uint AsyncDataSize => AsyncSectionSize & 0x7FFFFFFF;

    public List<VersionEntry> VersionEntries { get; set; } = [];

    public bool IsMsv6 => Magic == MagicMsv6;
}
