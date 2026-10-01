namespace TwdSaveEditor.Core.Model;

public sealed class VersionEntry(ulong typeCrc, uint versionCrc)
{
    public ulong TypeCrc { get; } = typeCrc;
    public uint VersionCrc { get; } = versionCrc;
}
