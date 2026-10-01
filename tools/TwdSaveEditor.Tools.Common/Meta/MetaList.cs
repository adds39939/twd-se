namespace TwdSaveEditor.Tools.Common.Meta;

public sealed record MetaList(List<MetaNode> Items) : MetaNode
{
    public IEnumerable<string> Strings => Items.OfType<MetaScalar>().Select(item => item.Text).OfType<string>();
}
