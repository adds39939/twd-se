using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.DialogLog;
using TwdSaveEditor.Season.S3.Saves;

namespace TwdSaveEditor.Season.S3.Decisions;

public sealed class S3DecisionLog(SaveSlot slot)
{
    private const int MaxNodes = 16;

    private readonly S3EventLog _log = new(slot);

    public static S3DecisionOption? Current(S3Decision decision, IReadOnlySet<ulong> nodes) =>
        decision.Options.FirstOrDefault(option => option.Expression.Length > 0 && NodeExpression.Evaluate(option.Expression, nodes))
        ?? decision.Options.FirstOrDefault(option => option.Expression.Length == 0);

    public static object Evaluate(S3LogicKey key, IReadOnlySet<ulong> nodes) => key.Text
        ? key.Values.FirstOrDefault(value => NodeExpression.Evaluate(value.Expression, nodes))?.Value ?? string.Empty
        : NodeExpression.Evaluate(key.Values[0].Expression, nodes);

    public S3DecisionOption? GetOption(S3Decision decision) => Current(decision, _log.Nodes());

    public bool IsSet(S3Decision decision)
    {
        var nodes = _log.Nodes();
        return decision.Options.Any(option => option.Expression.Length > 0 && NodeExpression.Evaluate(option.Expression, nodes));
    }

    public void SetValue(S3Decision decision, S3DecisionOption target)
    {
        var involved = decision.Options.SelectMany(option => NodeExpression.Nodes(option.Expression)).Distinct().ToList();
        if (involved.Count > MaxNodes)
            throw new InvalidOperationException($"The decision {decision.ChoiceKey} depends on too many dialog nodes to set.");

        var current = _log.Nodes();
        var related = S3DecisionCatalog.All
            .Where(other => !ReferenceEquals(other, decision) && other.Options.Any(option => NodeExpression.Nodes(option.Expression).Any(involved.Contains)))
            .Select(other => (Decision: other, Before: Current(other, current)))
            .ToList();

        HashSet<ulong>? best = null;
        (int Others, int Changes, int Present) bestCost = default;
        for (var mask = 0; mask < 1 << involved.Count; mask++)
        {
            var state = new HashSet<ulong>(current);
            for (var bit = 0; bit < involved.Count; bit++)
            {
                if ((mask & (1 << bit)) != 0)
                    state.Add(involved[bit]);
                else
                    state.Remove(involved[bit]);
            }

            if (!ReferenceEquals(Current(decision, state), target))
                continue;

            var cost = (
                related.Count(other => !ReferenceEquals(Current(other.Decision, state), other.Before)),
                involved.Count(node => state.Contains(node) != current.Contains(node)),
                involved.Count(state.Contains));
            if (best == null || cost.CompareTo(bestCost) < 0)
                (best, bestCost) = (state, cost);
        }

        if (best == null)
            throw new InvalidOperationException($"No set of dialog nodes makes {decision.ChoiceKey} read as {target.Value}.");

        _log.Replace(
            [.. involved.Where(node => current.Contains(node) && !best.Contains(node))],
            [.. involved.Where(node => best.Contains(node) && !current.Contains(node))],
            decision.Episode);
    }
}
