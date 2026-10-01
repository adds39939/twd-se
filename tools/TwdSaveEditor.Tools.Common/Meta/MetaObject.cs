namespace TwdSaveEditor.Tools.Common.Meta;

public sealed record MetaObject(string Type, List<KeyValuePair<string, MetaNode>> Members) : MetaNode;
