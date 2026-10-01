namespace TwdSaveEditor.Tools.Common.Props;

public sealed record ParsedPropertySet(uint Version, uint Flags, List<ParsedTypeGroup> Groups);
