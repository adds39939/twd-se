using TwdSaveEditor.Tools.ExtractSeason2Choices.Model;

namespace TwdSaveEditor.Tools.ExtractSeason2Choices.Choices;

public static class DecisionMerger
{
    private const string EpisodePrefix = "Episode 20";

    public static List<Decision> Merge(List<RandomDecision> random, List<StatChoice> stats, List<LogicKey> logic)
    {
        var decisions = random.Select(FromRandomizer).ToList();

        foreach (var stat in stats)
        {
            var nodes = stat.Options.SelectMany(option => option.Nodes).ToHashSet();
            var decision = decisions.FirstOrDefault(candidate => candidate.AllNodes.Any(nodes.Contains));
            if (decision == null)
                decisions.Add(decision = new Decision { Episode = stat.Episode });

            decision.Stat = stat;
            foreach (var (text, optionNodes) in stat.Options.Where(option => !option.Nodes.All(decision.Requires.Contains)))
            {
                var option = FindOrAdd(decision, optionNodes);
                option.Label = text;
                option.Nodes.AddRange(optionNodes.Except(option.Nodes).ToList());
            }

            foreach (var (text, _) in stat.Options.Where(option => option.Nodes.All(decision.Requires.Contains)))
                Combine(decision, decision.Options.Where(option => option.Label == null).ToList()).Label = text;
        }

        foreach (var key in logic)
        {
            var nodes = key.Values.SelectMany(value => value.Nodes).ToHashSet();
            var decision = decisions.FirstOrDefault(candidate => candidate.Logic == null && candidate.AllNodes.Any(nodes.Contains));
            if (decision == null)
                decisions.Add(decision = new Decision { Episode = EpisodeOf(key.Name) });

            decision.Logic = key;
            decision.Episode = EpisodeOf(key.Name);
            foreach (var (value, valueNodes) in key.Values)
            {
                var matching = decision.Options.Where(option => option.Nodes.Any(valueNodes.Contains)).ToList();
                var option = matching.Count > 0 ? Combine(decision, matching) : FindOrAdd(decision, valueNodes);
                option.LogicValue = value;
                option.Nodes.AddRange(valueNodes.Except(option.Nodes).ToList());
            }

            if (key.IsMap)
                continue;

            var others = decision.Options.Where(option => option.LogicValue == null).ToList();
            if (others.Count == 0)
                decision.Options.Add(new DecisionOption());
            else
                Combine(decision, others);
        }

        return [.. decisions.OrderBy(decision => decision.Episode)];
    }

    private static Decision FromRandomizer(RandomDecision random)
    {
        var decision = new Decision { Episode = random.Episode, RandomizerId = random.Id };
        if (random.Checks.Count >= 2)
        {
            foreach (var check in random.Checks)
            {
                var option = new DecisionOption();
                option.Nodes.AddRange(check.Where(random.Generated.Contains).Concat(check).Distinct());
                decision.Options.Add(option);
            }
        }
        else
        {
            foreach (var node in random.Generated)
            {
                var option = new DecisionOption();
                option.Nodes.Add(node);
                decision.Options.Add(option);
            }
        }

        decision.Requires.AddRange(random.Generated.Count > 0 ? random.Always : []);
        return decision;
    }

    private static DecisionOption Combine(Decision decision, List<DecisionOption> options)
    {
        var first = options[0];
        foreach (var other in options.Skip(1))
        {
            first.Nodes.AddRange(other.Nodes.Except(first.Nodes).ToList());
            first.Label ??= other.Label;
            decision.Options.Remove(other);
        }

        return first;
    }

    private static DecisionOption FindOrAdd(Decision decision, List<string> nodes)
    {
        var option = decision.Options.FirstOrDefault(candidate => candidate.Nodes.Any(nodes.Contains));
        if (option == null)
            decision.Options.Add(option = new DecisionOption());

        return option;
    }

    private static int EpisodeOf(string logicKey) =>
        logicKey.StartsWith(EpisodePrefix, StringComparison.Ordinal) ? logicKey[EpisodePrefix.Length] - '0' : 0;
}
