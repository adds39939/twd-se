namespace TwdSaveEditor.Core.Model;

public sealed class SaveGameFile
{
    public const int AgentSize = 61;

    public required string LuaDoFile { get; init; }

    public required List<byte[]> Agents { get; init; }

    public required List<ulong> RuntimePropertyNames { get; init; }

    public required List<ulong> EnabledDynamicSets { get; init; }

    public List<VersionEntry> VersionEntries { get; init; } = [];
}
