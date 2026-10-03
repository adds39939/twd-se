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

    public bool IsSet(StoryDecision decision) => IsSet(decision, _log.Nodes());

    public static bool IsSet(StoryDecision decision, IReadOnlySet<ulong> nodes) =>
        decision.Options.Any(option => option.Expression.Length > 0 && NodeExpression.Evaluate(option.Expression, nodes));

    public void SetValue(StoryDecision decision, StoryDecisionOption target) =>
        Rewrite(decision, state => ReferenceEquals(Current(decision, state), target), target.Value,
            $"No set of dialog nodes makes {decision.ChoiceKey} read as {target.Value}.");

    public void Clear(StoryDecision decision) =>
        Rewrite(decision, state => !IsSet(decision, state), null,
            $"No set of dialog nodes leaves {decision.ChoiceKey} unset.");

    private void Rewrite(StoryDecision decision, Func<IReadOnlySet<ulong>, bool> accepts, string? value, string impossible)
    {
        var definitions = season.LogicKeys.Where(key => key.Key.Equals(decision.ChoiceKey, StringComparison.OrdinalIgnoreCase)).ToList();
        var involved = decision.Options.Select(option => option.Expression)
            .Concat(definitions.SelectMany(key => key.Values).Select(value => value.Expression))
            .SelectMany(NodeExpression.Nodes)
            .Distinct()
            .ToList();
        if (involved.Count > MaxNodes)
        {
            throw new InvalidOperationException($"The decision {decision.ChoiceKey} depends on too many dialog nodes to change.");
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

            if (!accepts(state))
            {
                continue;
            }

            var cost = (
                definitions.Count(key => !Reads(key, state, value)),
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
            throw new InvalidOperationException(impossible);
        }

        _log.Replace(
            [.. involved.Where(node => current.Contains(node) && !best.Contains(node))],
            [.. involved.Where(node => best.Contains(node) && !current.Contains(node))],
            decision.Episode);
    }

    private static bool Reads(StoryLogicKey key, IReadOnlySet<ulong> nodes, string? value) =>
        Evaluate(key, nodes) switch
        {
            bool flag when value is null => !flag,
            bool flag => bool.TryParse(value, out var wanted) && flag == wanted,
            string text when IsKnownValue(key, value) => text.Equals(value, StringComparison.OrdinalIgnoreCase),
            string text => text.Length == 0,
            _ => true,
        };

    private static bool IsKnownValue(StoryLogicKey key, string? value) =>
        value is not null
        && key.Values.Any(known => known.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
}
