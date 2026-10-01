namespace TwdSaveEditor.Tools.Common.Meta;

public sealed record MetaBundleFile(string Name, ulong NameSymbol, ulong TypeSymbol, string? Type, uint Offset, uint Size, MetaDocument? Content);
