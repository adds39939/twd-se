using System.Numerics;

namespace TwdSaveEditor.Tools.ExtractNodeMappings.EventLog;

public sealed class EpageHashes
{
    public HashSet<ulong> Nodes { get; } = [];
    public HashSet<ulong> Choices { get; } = [];

    public void Add(EpageHashes other)
    {
        Nodes.UnionWith(other.Nodes);
        Choices.UnionWith(other.Choices);
    }

    public bool HasNode(BigInteger? value) => Contains(Nodes, value);

    public bool HasChoice(BigInteger? value) => Contains(Choices, value);

    private static bool Contains(HashSet<ulong> hashes, BigInteger? value) =>
        value is { } number && number <= ulong.MaxValue && hashes.Contains((ulong)number);
}
