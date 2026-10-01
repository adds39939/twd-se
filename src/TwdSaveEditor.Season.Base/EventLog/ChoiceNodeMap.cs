using TwdSaveEditor.Core.Hashing;

namespace TwdSaveEditor.Season.Base.EventLog;

public sealed class ChoiceNodeMap
{
    private readonly Dictionary<ulong, (string ChoiceKey, string OptionValue)> _nodes;
    private readonly Dictionary<(string ChoiceKey, string OptionValue), ulong> _reverse;

    public ChoiceNodeMap(IEnumerable<KeyValuePair<ulong, (string ChoiceKey, string OptionValue)>> nodes)
    {
        _nodes = new Dictionary<ulong, (string, string)>();
        _reverse = new Dictionary<(string, string), ulong>();

        foreach (var (hash, choice) in nodes)
        {
            _nodes[hash] = choice;
            _reverse[choice] = hash;
        }
    }

    public static ChoiceNodeMap FromGuids(
        IEnumerable<KeyValuePair<string, (string ChoiceKey, string OptionValue)>> guidNodes)
    {
        return new ChoiceNodeMap(guidNodes.Select(kv =>
            KeyValuePair.Create(TelltaleHash.ComputeCrc64("{" + kv.Key + "}"), kv.Value)));
    }

    public IReadOnlyDictionary<ulong, (string ChoiceKey, string OptionValue)> Nodes => _nodes;

    public (string ChoiceKey, string OptionValue)? DetectChoice(ulong nodeHash)
    {
        if (_nodes.TryGetValue(nodeHash, out var result))
            return result;

        return null;
    }

    public ulong? GetNodeHash(string choiceKey, string optionValue)
    {
        return _reverse.TryGetValue((choiceKey, optionValue), out var hash) ? hash : null;
    }
}
