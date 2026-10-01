namespace TwdSaveEditor.Tools.Common.Meta;

public sealed record MetaBundle(int Version, List<MetaBundleFile> Files) : MetaNode;
