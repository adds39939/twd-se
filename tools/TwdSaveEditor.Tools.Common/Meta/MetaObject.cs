namespace TwdSaveEditor.Tools.Common.Meta;

public sealed record MetaObject(string Type, List<KeyValuePair<string, MetaNode>> Members) : MetaNode
{
    public MetaNode? Find(string name) => Members.Where(member => member.Key == name).Select(member => member.Value).FirstOrDefault();

    public uint FindUInt32(string name) => Find(name) is MetaScalar { Value: uint value } ? value : 0;
}
