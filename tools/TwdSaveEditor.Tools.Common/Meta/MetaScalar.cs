namespace TwdSaveEditor.Tools.Common.Meta;

public sealed record MetaScalar(object Value) : MetaNode
{
    public string? Text => Value as string;
}
