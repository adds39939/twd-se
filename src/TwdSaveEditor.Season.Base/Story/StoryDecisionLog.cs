using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.DialogLog;

namespace TwdSaveEditor.Season.Base.Story;

public sealed class StoryDecisionLog(SaveSlot slot, StorySeason season)
{
    private const int MaxNodes = 16;

    private readonly StoryEventLog _log = new(slot, season);

    public static StoryDecisionOption? Current(StoryDecision decision, IReadOnlySet<ulong> nodes) =>
        decision.Options.FirstOrDefault(option => option.Expression.Length > 0 && NodeExpression.Evaluate(option.Expression, nodes))
        ?? decision.Options.FirstOrDefault(option => option.Expression.Length == 0);

    public static object Evaluate(StoryLogicKey key, IReadOnlySet<ulong> nodes) => key.Text
        ? key.Values.FirstOrDefault(value => NodeExpression.Evaluate(value.Expression, nodes))?.Value ?? string.Empty
        : NodeExpression.Evaluate(key.Values[0].Expression, nodes);

    public StoryDecisionOption? GetOption(StoryDecision decision) => Current(decision, _log.Nodes());

    public bool IsSet(StoryDecision decision)
    {
        var nodes = _log.Nodes();
        return decision.Options.Any(option => option.Expression.Length > 0 && NodeExpression.Evaluate(option.Expression, nodes));
    }

    public void SetValue(StoryDecision decision, StoryDecisionOption target)
    {
        var definitions = season.LogicKeys.Where(key => key.Key.Equals(decision.ChoiceKey, StringComparison.OrdinalIgnoreCase)).ToList();
        var involved = decision.Options.Select(option => option.Expression)
            .Concat(definitions.SelectMany(key => key.Values).Select(value => value.Expression))
            .SelectMany(NodeExpression.Nodes)
            .Distinct()
            .ToList();
        if (involved.Count > MaxNodes)
        {
            throw new InvalidOperationException($"The decision {decision.ChoiceKey} depends on too many dialog nodes to set.");
        }

        var current = _log.Nodes();
        var related = season.Decisions
            .Where(other => !ReferenceEquals(other, decision) && other.Options.Any(option => NodeExpression.Nodes(option.Expression).Any(involved.Contains)))
            .Select(other => (Decision: other, Before: Current(other, current)))
            .ToList();

        HashSet<ulong>? best = null;
        (int Disagreeing, int Others, int Changes, int Present) bestCost = default;
        for (var mask = 0; mask < 1 << involved.Count; mask++)
        {
            var state = new HashSet<ulong>(current);
            for (var bit = 0; bit < involved.Count; bit++)
            {
                if ((mask & (1 << bit)) != 0)
                {
                    state.Add(involved[bit]);
                }
                else
                {
                    state.Remove(involved[bit]);
                }
            }

            if (!ReferenceEquals(Current(decision, state), target))
            {
                continue;
            }

            var cost = (
                definitions.Count(key => !Reads(key, state, target.Value)),
                related.Count(other => !ReferenceEquals(Current(other.Decision, state), other.Before)),
                involved.Count(node => state.Contains(node) != current.Contains(node)),
                involved.Count(state.Contains));
            if (best == null || cost.CompareTo(bestCost) < 0)
            {
                (best, bestCost) = (state, cost);
            }
        }

        if (best == null)
        {
            throw new InvalidOperationException($"No set of dialog nodes makes {decision.ChoiceKey} read as {target.Value}.");
        }

        _log.Replace(
            [.. involved.Where(node => current.Contains(node) && !best.Contains(node))],
            [.. involved.Where(node => best.Contains(node) && !current.Contains(node))],
            decision.Episode);
    }

    private static bool Reads(StoryLogicKey key, IReadOnlySet<ulong> nodes, string value) => Evaluate(key, nodes) switch
    {
        bool flag => bool.TryParse(value, out var wanted) && flag == wanted,
        string text => key.Values.Any(known => known.Value.Equals(value, StringComparison.OrdinalIgnoreCase))
            ? text.Equals(value, StringComparison.OrdinalIgnoreCase)
            : text.Length == 0,
        _ => true,
    };
}
