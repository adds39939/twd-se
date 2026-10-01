using TwdSaveEditor.Tools.Common.Hashing;

namespace TwdSaveEditor.Tools.Common.Meta;

public sealed record MetaPropertySet(int Version, uint Flags, uint Size, List<ulong> Parents, List<MetaProperty> Properties, string? Error) : MetaNode
{
    public MetaNode? Find(string key)
    {
        var hash = TelltaleCrc64.Compute(key);
        return Properties.FirstOrDefault(property => property.Key == hash)?.Value;
    }
}
