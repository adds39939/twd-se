namespace TwdSaveEditor.Core.Model;

public sealed class BundleFileEntry
{
    public required string Name { get; init; }
    public required uint Offset { get; init; }
    public required uint Size { get; init; }
    public required ulong Hash1 { get; init; }
    public required ulong Hash2 { get; init; }
}
